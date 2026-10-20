namespace BackendArchitect.Distributed.Messaging.Queues;

public enum ReceiveMode
{
    PeekLock,            // at-least-once: the message is LENT, and comes back if you never complete it
    ReceiveAndDelete     // at-most-once: gone the moment it is handed over
}

public sealed record QueuedMessage(string MessageId, string Body, int DeliveryCount);

public sealed record DeadLetteredMessage(string MessageId, string Body, string Reason, string Description);

// A queue is a to-do list: one consumer takes an item and it is gone. The broker - not the consumer -
// remembers what has been done, which is why a consumer can die and be replaced with no bookkeeping.
//
// The lock is the whole trick. While a consumer holds a message it is invisible to everyone else, but
// it is STILL IN THE QUEUE. If the consumer dies the lock simply expires and the message returns. No
// heartbeat, no cleanup job.
//
//            ACTIVE ──receive──► LOCKED ──Complete()──► deleted
//              ▲                   │
//              └── Abandon() ──────┤
//              └── lock expires ───┤
//                                  └── DeadLetter() / too many deliveries ──► DLQ
//
// Time is a virtual tick rather than a clock, so every scenario below is deterministic - the same
// injected-clock trick used by the circuit breaker in Reliability/Resilience.
public sealed class MessageQueue
{
    private readonly record struct Lease(QueuedMessage Message, long ExpiresAtTick, string ConsumerId);

    private readonly Queue<QueuedMessage> _active = new();
    private readonly Dictionary<string, Lease> _locked = [];
    private readonly List<DeadLetteredMessage> _deadLetters = [];
    private readonly long _lockDurationTicks;
    private readonly int _maxDeliveryCount;

    private long _tick;

    public MessageQueue(long lockDurationTicks, int maxDeliveryCount)
    {
        _lockDurationTicks = lockDurationTicks;
        _maxDeliveryCount = maxDeliveryCount;
    }

    public int ActiveCount => _active.Count;
    public int LockedCount => _locked.Count;
    public IReadOnlyList<DeadLetteredMessage> DeadLetterQueue => _deadLetters;

    /// <summary>Total times a message has been handed to a consumer - redeliveries included.</summary>
    public int Deliveries { get; private set; }

    public void Send(string messageId, string body) => _active.Enqueue(new QueuedMessage(messageId, body, 0));

    /// <summary>Advances virtual time, which is what lets a lock expire.</summary>
    public void Advance(long ticks)
    {
        _tick += ticks;
        ReleaseExpiredLocks();
    }

    public QueuedMessage? Receive(string consumerId, ReceiveMode mode = ReceiveMode.PeekLock)
    {
        ReleaseExpiredLocks();

        if (!_active.TryDequeue(out var message))
            return null;

        Deliveries++;
        message = message with { DeliveryCount = message.DeliveryCount + 1 };

        if (mode == ReceiveMode.PeekLock)
            _locked[message.MessageId] = new Lease(message, _tick + _lockDurationTicks, consumerId);

        return message;
    }

    /// <summary>Succeeds only while this consumer still holds the lock.</summary>
    public bool Complete(string messageId)
    {
        ReleaseExpiredLocks();
        return _locked.Remove(messageId);
    }

    /// <summary>Hands it straight back for an immediate retry - a transient failure.</summary>
    public void Abandon(string messageId)
    {
        if (_locked.Remove(messageId, out var lease))
            ReturnOrDeadLetter(lease.Message, "MaxDeliveryCountExceeded", "abandoned too many times");
    }

    /// <summary>A permanent failure: get it out of the way instead of burning the retry budget.</summary>
    public void DeadLetter(string messageId, string reason, string description)
    {
        if (_locked.Remove(messageId, out var lease))
            _deadLetters.Add(new DeadLetteredMessage(lease.Message.MessageId, lease.Message.Body, reason, description));
    }

    /// <summary>Pushes the lock out while the handler is still working.</summary>
    public bool RenewLock(string messageId)
    {
        if (!_locked.TryGetValue(messageId, out var lease))
            return false;

        _locked[messageId] = lease with { ExpiresAtTick = _tick + _lockDurationTicks };
        return true;
    }

    private void ReleaseExpiredLocks()
    {
        var expired = _locked.Where(entry => entry.Value.ExpiresAtTick <= _tick).Select(entry => entry.Key).ToArray();

        foreach (var messageId in expired)
        {
            var lease = _locked[messageId];
            _locked.Remove(messageId);
            ReturnOrDeadLetter(lease.Message, "MaxDeliveryCountExceeded", $"lock expired {lease.Message.DeliveryCount} time(s)");
        }
    }

    private void ReturnOrDeadLetter(QueuedMessage message, string reason, string description)
    {
        if (message.DeliveryCount >= _maxDeliveryCount)
            _deadLetters.Add(new DeadLetteredMessage(message.MessageId, message.Body, reason, description));
        else
            _active.Enqueue(message);
    }
}

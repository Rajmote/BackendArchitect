namespace BackendArchitect.Distributed.Cqrs;

public enum ReadAfterWriteStrategy
{
    QueryTheReadStore,        // the default, and the one that loses the user's own order
    ReturnFromTheCommand,     // free, and covers most cases
    WaitForTheProjection,     // a session token: correct, and costs latency
    SynchronousProjection     // consistent - and gives back the independence you paid for
}

public sealed record ReadAfterWriteReport(string Strategy, int Writes, int MissingImmediatelyAfter, int ProjectionWaits);

public sealed record ProjectorReport(string Scenario, int EventsPublished, int EventsProjected, int Lag, bool ReadStoreHealthy);

// Level 3 in miniature: a write store, a stream of events, a projector, and a read store.
//
// The bill arrives immediately: between the write and the projection the read store legitimately does
// not have the order. This is §8.1's read-your-writes problem, and it is the NORMAL path - a user's
// next action always follows their last one.
public sealed class OrderProjectionSystem
{
    private readonly Dictionary<int, string> _writeStore = [];
    private readonly Dictionary<int, string> _readStore = [];
    private readonly Queue<(int OrderId, string Body)> _pendingEvents = new();
    private readonly int _projectionDelay;

    private bool _projectorStopped;

    public OrderProjectionSystem(int projectionDelay) => _projectionDelay = projectionDelay;

    public int Lag => _pendingEvents.Count;

    /// <summary>The projector is a consumer group: a poison event stops it advancing at all.</summary>
    public void StopProjector() => _projectorStopped = true;

    public string Write(int orderId, string body)
    {
        _writeStore[orderId] = body;
        _pendingEvents.Enqueue((orderId, body));
        Project();
        return body;
    }

    private void Project()
    {
        if (_projectorStopped)
            return;

        while (_pendingEvents.Count > _projectionDelay)
        {
            var (orderId, body) = _pendingEvents.Dequeue();
            _readStore[orderId] = body;
        }
    }

    public void DrainProjection()
    {
        if (_projectorStopped)
            return;

        while (_pendingEvents.Count > 0)
        {
            var (orderId, body) = _pendingEvents.Dequeue();
            _readStore[orderId] = body;
        }
    }

    public string? QueryReadStore(int orderId) => _readStore.GetValueOrDefault(orderId);

    public static ReadAfterWriteReport PlaceThenView(ReadAfterWriteStrategy strategy, int writes, int projectionDelay)
    {
        var system = new OrderProjectionSystem(strategy == ReadAfterWriteStrategy.SynchronousProjection ? 0 : projectionDelay);
        var missing = 0;
        var waits = 0;

        for (var orderId = 0; orderId < writes; orderId++)
        {
            var written = system.Write(orderId, $"order-{orderId}");

            string? shown = strategy switch
            {
                // The write side already HAS the order. Asking the read store about data you just
                // wrote is the mistake.
                ReadAfterWriteStrategy.ReturnFromTheCommand => written,

                ReadAfterWriteStrategy.WaitForTheProjection => WaitThenRead(system, orderId, ref waits),

                _ => system.QueryReadStore(orderId)
            };

            if (shown is null)
                missing++;
        }

        return new ReadAfterWriteReport(Describe(strategy), writes, missing, waits);
    }

    private static string? WaitThenRead(OrderProjectionSystem system, int orderId, ref int waits)
    {
        if (system.QueryReadStore(orderId) is { } immediate)
            return immediate;

        waits++;
        system.DrainProjection();
        return system.QueryReadStore(orderId);
    }

    /// <summary>
    /// A stalled projector is invisible to every health check: the read store is up, fast and
    /// answering 200 - with Saturday's data.
    /// </summary>
    public static ProjectorReport ProjectorStalls(bool stopProjector, int events)
    {
        var system = new OrderProjectionSystem(projectionDelay: 0);

        for (var orderId = 0; orderId < events; orderId++)
        {
            if (stopProjector && orderId == events / 2)
                system.StopProjector();

            system.Write(orderId, $"order-{orderId}");
        }

        return new ProjectorReport(
            stopProjector ? "projector stalled on a poison event" : "projector healthy",
            events,
            events - system.Lag,
            system.Lag,
            ReadStoreHealthy: true);          // it is up, fast, and wrong
    }

    private static string Describe(ReadAfterWriteStrategy strategy) => strategy switch
    {
        ReadAfterWriteStrategy.QueryTheReadStore => "query the read store",
        ReadAfterWriteStrategy.ReturnFromTheCommand => "return from the command",
        ReadAfterWriteStrategy.WaitForTheProjection => "wait for the projection",
        _ => "synchronous projection"
    };
}

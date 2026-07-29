namespace BackendArchitect.Databases.Cosmos.RequestUnits;

// Models provisioned throughput: you buy a number of RU per SECOND, and Cosmos enforces it every
// second. Ask for more than you provisioned and the excess is rejected with 429 Too Many Requests
// (the SDK then retries it, which is why throttling usually shows up as latency, not errors).
public sealed class ThroughputBudget
{
    private readonly double _ruPerSecond;
    private double _remainingThisSecond;

    public ThroughputBudget(double ruPerSecond)
    {
        _ruPerSecond = ruPerSecond;
        _remainingThisSecond = ruPerSecond;
    }

    /// <summary>Total RU actually spent (throttled requests consume nothing).</summary>
    public double Consumed { get; private set; }

    /// <summary>How many requests were rejected with 429.</summary>
    public int Throttled { get; private set; }

    public double RemainingThisSecond => _remainingThisSecond;

    /// <summary>Attempt an operation. False = 429 Too Many Requests.</summary>
    public bool TryConsume(double requestCharge)
    {
        if (requestCharge > _remainingThisSecond)
        {
            Throttled++;
            return false;
        }

        _remainingThisSecond -= requestCharge;
        Consumed += requestCharge;
        return true;
    }

    /// <summary>A new second begins — the budget refills.</summary>
    public void NextSecond() => _remainingThisSecond = _ruPerSecond;
}

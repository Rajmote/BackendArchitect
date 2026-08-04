namespace BackendArchitect.Apis.Grpc.Contracts;

// gRPC deadlines propagate as a SHRINKING BUDGET across the whole call chain, rather than each service
// starting a fresh timer. That is what prevents "zombie work": services burning CPU and database
// connections on a request whose caller has already given up.
//
//   independent 2s timeouts:  A(2s) -> B(2s) -> C(2s)  = up to 6s, and B/C keep working after A quits
//   propagated 2s deadline:   A(2.0s) -> B(1.6s) -> C(1.2s) = 2s total, everyone stops together
public sealed record DeadlineBudget(double RemainingSeconds)
{
    public bool IsExpired => RemainingSeconds <= 0;

    /// <summary>Time spent locally before making the next hop is deducted from what's passed on.</summary>
    public DeadlineBudget AfterSpending(double seconds) =>
        new(Math.Max(0, RemainingSeconds - seconds));

    /// <summary>Should this service even begin work that is expected to take this long?</summary>
    public bool CanAfford(double estimatedSeconds) =>
        !IsExpired && RemainingSeconds >= estimatedSeconds;
}

public sealed record HopResult(string Service, double BudgetOnArrival, bool DidWork, string Outcome);

public static class CallChain
{
    /// <summary>
    /// Walk a chain of services, each consuming some time, under one propagated deadline.
    /// A service whose budget has run out fails fast instead of doing work nobody is waiting for.
    /// </summary>
    public static List<HopResult> Propagate(double deadlineSeconds, params (string Service, double Cost)[] hops)
    {
        var budget = new DeadlineBudget(deadlineSeconds);
        var results = new List<HopResult>();

        foreach (var (service, cost) in hops)
        {
            if (budget.IsExpired)
            {
                results.Add(new HopResult(service, budget.RemainingSeconds, false, "DEADLINE_EXCEEDED - skipped, no zombie work"));
                continue;
            }

            var arrival = budget.RemainingSeconds;
            budget = budget.AfterSpending(cost);
            var outcome = budget.IsExpired ? "DEADLINE_EXCEEDED while working" : "OK";
            results.Add(new HopResult(service, arrival, true, outcome));
        }

        return results;
    }

    /// <summary>Without propagation every service restarts the clock, so the worst case is the sum.</summary>
    public static double WorstCaseWithIndependentTimeouts(double perServiceTimeout, int serviceCount) =>
        perServiceTimeout * serviceCount;
}

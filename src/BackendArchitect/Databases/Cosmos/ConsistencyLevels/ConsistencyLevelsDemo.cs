using System.Globalization;

namespace BackendArchitect.Databases.Cosmos.Consistency;

// Example runner: five writes committed, the replica has only caught up on three — then the SAME read
// is served under each consistency level, showing exactly what staleness each one allows.
public class ConsistencyLevelsDemo
{
    public void Run()
    {
        var ic = CultureInfo.InvariantCulture;
        var store = new ReplicatedStore();

        int myWriteToken = 0;
        for (var v = 1; v <= 5; v++)
            myWriteToken = store.Write($"v{v}");   // I wrote all five; my session token is 5

        store.Replicate(3);                        // the replica has only v1..v3

        Console.WriteLine($"Primary has v1..v5; replica has caught up to {store.LatestOnReplica} (lag {store.ReplicationLag})");
        Console.WriteLine($"  {"level",-20}{"author reads",14}{"other user reads",18}{"read cost",11}");

        foreach (var level in Enum.GetValues<CosmosConsistency>())
        {
            var author = store.Read(level, sessionToken: myWriteToken);  // the person who wrote v5
            var other = store.Read(level, sessionToken: 0);              // a different user's session
            var cost = ReplicatedStore.ReadCostMultiplier(level).ToString("0.0", ic) + "x";

            Console.WriteLine($"  {level,-20}{author,14}{other,18}{cost,11}");
        }

        Console.WriteLine();
        Console.WriteLine("  Session: the AUTHOR sees v5 (read your own writes) while others still see v3");
        Console.WriteLine("  -> that is why Session is the default: it fixes 'I posted it but can't see it'");
        Console.WriteLine("     without paying Strong's latency and 2x read cost.");
    }
}

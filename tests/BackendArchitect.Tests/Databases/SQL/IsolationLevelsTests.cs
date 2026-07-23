using BackendArchitect.Databases.Sql.IsolationLevels;

namespace BackendArchitect.Tests.Databases.Sql;

// Databases · SQL · Isolation levels — the "serialized" booth must never oversell under concurrency.
// (We don't unit-test the UNSERIALIZED oversell: a race is timing-dependent and would be flaky.
//  Its bug is shown in IsolationLevelsDemo instead.)
public class IsolationLevelsTests
{
    [Fact]
    public void Serialized_NeverOversells_WithOneTicket()
    {
        var booth = new TicketBooth(available: 1, serialized: true);

        Parallel.For(0, 50, _ => booth.TryBook());

        Assert.Equal(1, booth.Sold);
        Assert.Equal(0, booth.Available);
    }

    [Fact]
    public void Serialized_SellsExactlyCapacity_WhenDemandExceedsIt()
    {
        var booth = new TicketBooth(available: 5, serialized: true);

        Parallel.For(0, 50, _ => booth.TryBook());

        Assert.Equal(5, booth.Sold);
        Assert.Equal(0, booth.Available);
    }

    [Fact]
    public void TryBook_ReturnsFalse_WhenSoldOut()
    {
        var booth = new TicketBooth(available: 1, serialized: true);

        Assert.True(booth.TryBook());  // last ticket
        Assert.False(booth.TryBook()); // sold out
    }
}

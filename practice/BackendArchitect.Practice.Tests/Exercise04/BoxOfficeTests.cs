using BackendArchitect.Practice.Exercise04;

namespace BackendArchitect.Practice.Tests.Exercise04;

// Exercise 04 — three starter tests showing the target shape.
//
// YOUR JOB: make these pass, then add tests for:
//   * no oversell   — 200 threads, capacity 10, exactly 10 succeed
//   * the invariant — SeatsSold + SeatsAvailable == capacity after the race
//   * distinct seats — every successful reservation got a different seat number
//   * one seat map  — 200 threads hitting an unseen screening build it exactly once
//
// 🌟 And VERIFY THE TESTS CAN FAIL: remove your synchronisation and confirm they go red. A concurrency
//    test that has never failed proves nothing.
public class BoxOfficeTests
{
    private const int Capacity = 10;

    private static BoxOffice NewBoxOffice(int capacity = Capacity) =>
        new(capacity, screeningId =>
        {
            Thread.Sleep(20);                       // building a seat map is expensive
            return new SeatMap(screeningId, capacity);
        });

    [Fact]
    public void ItReservesTheFirstSeat()
    {
        var boxOffice = NewBoxOffice();

        var result = boxOffice.Reserve("screening-1", "customer-1");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.SeatNumber);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ItRefusesToOversellWithoutThrowing()
    {
        var boxOffice = NewBoxOffice();

        for (var i = 0; i < Capacity; i++)
            Assert.True(boxOffice.Reserve("screening-1", $"customer-{i}").Succeeded);

        var overflow = boxOffice.Reserve("screening-1", "customer-late");

        Assert.False(overflow.Succeeded);
        Assert.Null(overflow.SeatNumber);
        Assert.NotNull(overflow.Error);
    }

    [Fact]
    public void ItBuildsTheSeatMapOnceForRepeatedReservations()
    {
        var boxOffice = NewBoxOffice();

        boxOffice.Reserve("screening-1", "customer-1");
        boxOffice.Reserve("screening-1", "customer-2");
        boxOffice.Reserve("screening-1", "customer-3");

        Assert.Equal(1, boxOffice.SeatMapsBuilt);
    }

    // TODO (you): no oversell — 200 real Threads + a Barrier against capacity 10, exactly 10 succeed.
    //             Hint: collect the results into an array indexed by thread, then count.

    // TODO (you): the invariant — SeatsSold + SeatsAvailable == capacity once the race has finished.

    // TODO (you): distinct seats — every successful reservation has a different SeatNumber in 1..10.
    //             Hint: results.Where(r => r.Succeeded).Select(r => r.SeatNumber).Distinct().Count()

    // TODO (you): one seat map — 200 threads reserving an unseen screening at the same instant must
    //             still leave SeatMapsBuilt == 1.
}

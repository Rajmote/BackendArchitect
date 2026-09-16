namespace BackendArchitect.Practice.Exercise04;

public sealed record SeatMap(string ScreeningId, int Capacity);

public sealed record ReservationResult(bool Succeeded, int? SeatNumber, string? Error)
{
    public static ReservationResult Reserved(int seatNumber) => new(true, seatNumber, null);
    public static ReservationResult SoldOut() => new(false, null, "sold out");
}

// Exercise 04 — see practice/Exercise04-RaceConditions.md.
//
// A cinema box office taking concurrent reservations. Three things must hold no matter how many
// threads arrive at once:
//   1. never oversell          - at most `capacity` reservations succeed
//   2. never lose a booking    - SeatsSold + SeatsAvailable == capacity, always
//   3. one seat map per screening, however many threads ask for it first
//
// `buildSeatMap` is deliberately slow and deliberately injected, so your tests can count how many
// times it ran.
public sealed class BoxOffice
{
    private readonly int _capacity;
    private readonly Func<string, SeatMap> _buildSeatMap;

    public BoxOffice(int capacity, Func<string, SeatMap> buildSeatMap)
    {
        _capacity = capacity;
        _buildSeatMap = buildSeatMap;
    }

    /// <summary>
    /// Reserves one seat for the screening, returning the seat number. Seats are numbered 1..capacity
    /// and no two reservations may share one. When the screening is full, return a FAILED result -
    /// never throw.
    /// </summary>
    public ReservationResult Reserve(string screeningId, string customerId) =>
        throw new NotImplementedException("Exercise 04: see practice/Exercise04-RaceConditions.md");

    public int SeatsSold =>
        throw new NotImplementedException("Exercise 04: see practice/Exercise04-RaceConditions.md");

    public int SeatsAvailable =>
        throw new NotImplementedException("Exercise 04: see practice/Exercise04-RaceConditions.md");

    /// <summary>How many times <c>buildSeatMap</c> actually ran. Requirement 5 says exactly one.</summary>
    public int SeatMapsBuilt =>
        throw new NotImplementedException("Exercise 04: see practice/Exercise04-RaceConditions.md");
}

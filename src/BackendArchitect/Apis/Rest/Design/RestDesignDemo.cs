namespace BackendArchitect.Apis.Rest.Design;

// Example runner: URL review, which changes force a new version, and the expand -> migrate -> contract
// pattern that lets a rename ship without breaking anyone.
public class RestDesignDemo
{
    public void Run()
    {
        Console.WriteLine("URL review:");
        foreach (var url in new[]
                 {
                     "/api/getCustomerOrders",
                     "/orders/open",
                     "/customers/10/orders",
                     "/orders?status=open&page=1&pageSize=20",
                 })
        {
            var review = ResourceUrl.Review(url);
            Console.WriteLine($"  {(review.IsRestful ? "OK " : "BAD")} {url,-42} {review.Reason}");
        }

        Console.WriteLine();
        Console.WriteLine("Does this change need a new version?");
        foreach (var change in Enum.GetValues<ChangeKind>())
            Console.WriteLine($"  {change,-28} {(ApiChange.RequiresNewVersion(change) ? "BREAKING -> new version" : "additive -> no version needed")}");

        Console.WriteLine();
        Console.WriteLine("Renaming 'phone' to 'phoneNumber' without breaking v1 clients:");
        var api = new VersionedOrderApi();

        var v1Before = api.GetCustomerV1();
        Console.WriteLine($"  v1 client reads 'phone'          -> {TolerantReader.ReadPhone(v1Before, "phone")}");

        var v2 = api.GetCustomerV2();
        Console.WriteLine($"  v1 client against the v2 shape   -> {TolerantReader.ReadPhone(v2, "phone") ?? "NULL  <- silent breakage"}");

        var migrating = api.GetCustomerV1(duringMigration: true);
        Console.WriteLine($"  EXPAND phase: serve both names   -> phone={TolerantReader.ReadPhone(migrating, "phone")}, " +
                          $"phoneNumber={TolerantReader.ReadPhone(migrating, "phoneNumber")}");
        Console.WriteLine("  -> old and new clients both work; remove 'phone' only once usage hits zero");

        Console.WriteLine();
        Console.WriteLine("v1 responses advertise their own retirement:");
        foreach (var (header, value) in VersionedOrderApi.DeprecationHeadersForV1())
            Console.WriteLine($"  {header}: {value}");
    }
}

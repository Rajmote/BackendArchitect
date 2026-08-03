using BackendArchitect.Apis.Rest.Design;

namespace BackendArchitect.Tests.Apis;

// APIs · REST · Design & versioning — URL rules, breaking-change classification, expand→contract.
public class RestDesignTests
{
    [Theory]
    [InlineData("/api/getCustomerOrders")]   // verb in the path
    [InlineData("/api/createOrder")]
    [InlineData("/orders/open")]             // filter in the path (and collides with /orders/{id})
    [InlineData("/orders/closed")]
    public void Review_RejectsVerbsAndFiltersInThePath(string url)
    {
        Assert.False(ResourceUrl.Review(url).IsRestful);
    }

    [Theory]
    [InlineData("/orders")]
    [InlineData("/orders/5")]
    [InlineData("/customers/10/orders")]
    [InlineData("/orders?status=open&page=1&pageSize=20")]   // filtering belongs here
    public void Review_AcceptsResourceUrls(string url)
    {
        Assert.True(ResourceUrl.Review(url).IsRestful);
    }

    [Fact]
    public void Review_RejectsDeeplyNestedUrls()
    {
        Assert.False(ResourceUrl.Review("/customers/10/orders/5/items/3/tags").IsRestful);
    }

    [Theory]
    [InlineData(ChangeKind.AddOptionalResponseField)]
    [InlineData(ChangeKind.AddEndpoint)]
    [InlineData(ChangeKind.AddOptionalQueryParameter)]
    public void AdditiveChanges_AreNotBreaking_AndNeedNoNewVersion(ChangeKind change)
    {
        Assert.False(ApiChange.IsBreaking(change));
        Assert.False(ApiChange.RequiresNewVersion(change));
    }

    [Theory]
    [InlineData(ChangeKind.RemoveResponseField)]
    [InlineData(ChangeKind.RenameResponseField)]
    [InlineData(ChangeKind.ChangeFieldType)]
    [InlineData(ChangeKind.MakeOptionalFieldRequired)]
    [InlineData(ChangeKind.ChangeStatusCodeSemantics)]
    public void RemovingOrTighteningChanges_AreBreaking(ChangeKind change)
    {
        Assert.True(ApiChange.IsBreaking(change));
        Assert.True(ApiChange.RequiresNewVersion(change));
    }

    [Fact]
    public void AddingAField_DoesNotDisturbAnExistingV1Client()
    {
        var api = new VersionedOrderApi();

        var body = api.GetCustomerV1();

        Assert.Equal("0612345678", TolerantReader.ReadPhone(body, "phone"));  // still there
        Assert.True(body.ContainsKey("loyaltyPoints"));                        // new field, harmless
    }

    [Fact]
    public void ARename_SilentlyBreaksAnOldClient() // returns null, not an error — the dangerous kind
    {
        var api = new VersionedOrderApi();

        var v2Body = api.GetCustomerV2();

        Assert.Null(TolerantReader.ReadPhone(v2Body, "phone"));
        Assert.Equal("0612345678", TolerantReader.ReadPhone(v2Body, "phoneNumber"));
    }

    [Fact]
    public void ExpandPhase_ServesBothNames_SoNeitherClientBreaks()
    {
        var api = new VersionedOrderApi();

        var body = api.GetCustomerV1(duringMigration: true);

        Assert.Equal("0612345678", TolerantReader.ReadPhone(body, "phone"));        // old client
        Assert.Equal("0612345678", TolerantReader.ReadPhone(body, "phoneNumber"));  // new client
    }

    [Fact]
    public void V1Responses_AdvertiseTheirSunset()
    {
        var headers = VersionedOrderApi.DeprecationHeadersForV1();

        Assert.Equal("true", headers["Deprecation"]);
        Assert.Contains("2027", headers["Sunset"]);
        Assert.Contains("successor-version", headers["Link"]);
    }
}

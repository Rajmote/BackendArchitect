namespace BackendArchitect.Apis.Rest.Design;

public sealed record UrlReview(string Url, bool IsRestful, string Reason);

// Reviews a URL against the REST resource-modelling rules we learned:
//   * the URL names a THING; the HTTP method names the action -> no verbs in the path
//   * collections are plural
//   * filtering/sorting/paging goes in the QUERY STRING, not in path segments
//     (a filter in the path also collides with the id route: /orders/open vs /orders/5)
public static class ResourceUrl
{
    private static readonly string[] VerbsThatShouldNotBeInAPath =
        ["get", "create", "update", "delete", "fetch", "list", "remove", "add", "set"];

    // Filter-ish words that belong in the query string because they describe a subset, not a resource.
    private static readonly string[] FilterWordsInPath =
        ["open", "closed", "active", "recent", "pending", "archived"];

    public static UrlReview Review(string url)
    {
        var path = url.Split('?')[0].Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var lower = segment.ToLowerInvariant();

            if (VerbsThatShouldNotBeInAPath.Any(verb => lower.StartsWith(verb) && lower.Length > verb.Length))
                return new UrlReview(url, false, $"'{segment}' puts a verb in the path — the HTTP method is the verb");

            if (FilterWordsInPath.Contains(lower))
                return new UrlReview(url, false, $"'{segment}' is a filter — use a query string (it also collides with the id route)");
        }

        if (segments.Length > 4)
            return new UrlReview(url, false, "nested too deeply — once a child has its own id, address it directly");

        return new UrlReview(url, true, "names resources; the method supplies the action");
    }
}

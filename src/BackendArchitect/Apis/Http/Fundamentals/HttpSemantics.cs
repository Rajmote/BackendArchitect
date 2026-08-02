namespace BackendArchitect.Apis.Http.Fundamentals;

public enum HttpMethodKind { Get, Head, Post, Put, Patch, Delete, Options }

// The two properties that drive almost every HTTP design decision:
//
//   * SAFE       — does not change server state (read-only). Machines you don't control (crawlers,
//                  prefetchers, link-preview bots, scanners) call safe methods UNINVITED, so putting a
//                  state change behind GET means software you've never heard of will trigger it.
//   * IDEMPOTENT — calling it N times has the same effect as calling it once. This is what makes an
//                  automatic retry safe.
public static class HttpSemantics
{
    public static bool IsSafe(HttpMethodKind method) => method switch
    {
        HttpMethodKind.Get or HttpMethodKind.Head or HttpMethodKind.Options => true,
        _ => false,
    };

    public static bool IsIdempotent(HttpMethodKind method) => method switch
    {
        // every safe method is also idempotent...
        HttpMethodKind.Get or HttpMethodKind.Head or HttpMethodKind.Options => true,
        // ...and these change state but converge on the same result when repeated
        HttpMethodKind.Put or HttpMethodKind.Delete => true,
        // POST creates a NEW thing each time; PATCH depends on the payload, so assume not
        HttpMethodKind.Post or HttpMethodKind.Patch => false,
        _ => false,
    };
}

// Which status codes are worth retrying.
public static class RetryPolicy
{
    /// <summary>Is this status transient — i.e. might the same request succeed later?</summary>
    public static bool IsTransient(int statusCode) => statusCode switch
    {
        408 => true,          // request timeout
        429 => true,          // too many requests — a 4xx that IS retryable (back off first)
        501 => false,         // not implemented — a 5xx that will NOT fix itself
        >= 500 and < 600 => true,
        _ => false,           // other 4xx: the request itself is wrong, retrying changes nothing
    };

    /// <summary>
    /// Retry safety needs BOTH conditions: a transient status AND an operation that can be repeated
    /// without duplicating an effect. A 503 on POST /payments is still dangerous — unless the request
    /// carries an idempotency key, which makes the handler idempotent.
    /// </summary>
    public static bool ShouldRetry(int statusCode, HttpMethodKind method, bool hasIdempotencyKey = false) =>
        IsTransient(statusCode) && (HttpSemantics.IsIdempotent(method) || hasIdempotencyKey);
}

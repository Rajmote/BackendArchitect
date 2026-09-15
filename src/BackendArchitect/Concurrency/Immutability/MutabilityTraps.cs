using System.Collections.Immutable;

namespace BackendArchitect.Concurrency.Immutability;

public sealed record TrapReport(string Trap, string Before, string After, bool Leaked);

// Four things that LOOK immutable and are not, plus the one that genuinely is.
//
// The single rule underneath all of them:
//   readonly, init, record and `with` all protect the ARROW.
//   Only the target's own type protects the TARGET.
public static class MutabilityTraps
{
    // Trap 1: readonly freezes the field, not the object it points at.
    public sealed class Configuration
    {
        public readonly Dictionary<string, string> Settings = new();
    }

    public sealed record FrozenConfiguration(ImmutableDictionary<string, string> Settings);

    // Trap 2: a record is only as immutable as its property TYPES.
    public sealed record Basket(int Id, List<string> Items);

    public sealed record FrozenBasket(int Id, ImmutableArray<string> Items);

    // Trap 4: value equality over a property that can still change.
    public sealed record MutableKey
    {
        public int Id { get; set; }
    }

    public static TrapReport ReadonlyFieldIsNotImmutable()
    {
        var config = new Configuration();
        config.Settings["timeout"] = "30";
        var before = Describe(config.Settings);

        config.Settings["timeout"] = "5";          // compiles; only reassigning the FIELD is blocked
        config.Settings["retries"] = "3";

        return new TrapReport("readonly Dictionary field", before, Describe(config.Settings), Leaked: true);
    }

    public static TrapReport RecordWithMutableListIsNotImmutable()
    {
        var basket = new Basket(1, ["Latte"]);
        var before = Describe(basket.Items);

        basket.Items.Add("Muffin");                // `init` blocks replacing the list, not filling it

        return new TrapReport("record + List<string>", before, Describe(basket.Items), Leaked: true);
    }

    // Trap 3: IReadOnlyList<T> is a WINDOW onto the live list, not a photograph of it.
    public static TrapReport ReadOnlyViewIsNotASnapshot()
    {
        var orders = new List<string> { "order-1", "order-2" };
        IReadOnlyList<string> view = orders;
        var before = $"view has {view.Count}";

        orders.Add("order-3");                     // the caller's "read-only" list just grew

        return new TrapReport("IReadOnlyList<T> view", before, $"view has {view.Count}", Leaked: true);
    }

    /// <summary>The runtime type is still List&lt;T&gt;, so the interface can simply be cast away.</summary>
    public static TrapReport ReadOnlyViewCanBeCastBack()
    {
        var orders = new List<string> { "order-1" };
        IReadOnlyList<string> published = orders;
        var before = Describe(published);

        var castBack = (List<string>)published;    // encapsulation was never there
        castBack.Add("injected");

        return new TrapReport("IReadOnlyList<T> cast back", before, Describe(published), Leaked: true);
    }

    /// <summary>`with` copies each property VALUE across - and a reference is a value.</summary>
    public static TrapReport WithIsAShallowCopy()
    {
        var original = new Basket(1, ["Latte"]);
        var copy = original with { Id = 2 };
        var before = Describe(original.Items);

        copy.Items.Add("Muffin");                  // one list, shared by both records

        return new TrapReport("`with` + List<string>", before, Describe(original.Items), Leaked: true);
    }

    /// <summary>Same shallow copy, now harmless: there is nothing mutable left to share.</summary>
    public static TrapReport DeepImmutabilityHoldsTheLine()
    {
        var original = new FrozenBasket(1, ["Latte"]);
        var copy = original with { Id = 2 };
        var before = Describe(original.Items);

        var bigger = copy.Items.Add("Muffin");     // returns a NEW array; nothing was mutated
        _ = copy with { Items = bigger };

        return new TrapReport("`with` + ImmutableArray", before, Describe(original.Items), Leaked: false);
    }

    /// <summary>
    /// A record whose hash depends on a field the holder can still change becomes UNREACHABLE in a
    /// dictionary - the entry stays in memory, in the wrong bucket, forever.
    /// </summary>
    public static TrapReport MutableKeyIsLostInADictionary()
    {
        var key = new MutableKey { Id = 1 };
        var cache = new Dictionary<MutableKey, string> { [key] = "receipt-1" };
        var before = $"found={cache.ContainsKey(key)}, count={cache.Count}";

        key.Id = 2;                                // the hash code has changed under the dictionary

        return new TrapReport("mutable record as a key", before,
            $"found={cache.ContainsKey(key)}, count={cache.Count}", Leaked: !cache.ContainsKey(key));
    }

    /// <summary>
    /// The same experiment with a List property. A record hashes its fields with
    /// EqualityComparer&lt;T&gt;.Default, and for List&lt;T&gt; that is REFERENCE-based - so mutating
    /// the list does not move the entry. The trap is narrower than it first looks.
    /// </summary>
    public static TrapReport ListPropertyDoesNotMoveTheKey()
    {
        var key = new Basket(1, ["Latte"]);
        var cache = new Dictionary<Basket, string> { [key] = "receipt-1" };
        var before = $"found={cache.ContainsKey(key)}, count={cache.Count}";

        key.Items.Add("Muffin");

        return new TrapReport("record + List as a key", before,
            $"found={cache.ContainsKey(key)}, count={cache.Count}", Leaked: !cache.ContainsKey(key));
    }

    private static string Describe(IEnumerable<string> items) => $"[{string.Join(", ", items)}]";

    private static string Describe(IReadOnlyDictionary<string, string> map) =>
        $"[{string.Join(", ", map.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"))}]";
}

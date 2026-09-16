using System.Collections.Immutable;

namespace BackendArchitect.Practice.Exercise05;

public sealed record PriceChange(string Sku, decimal Price);

/// <summary>
/// A frozen view of the catalogue. Version and Prices travel TOGETHER in one object on purpose - two
/// separate fields would let a reader catch the new version with the old prices.
/// </summary>
public sealed record PriceSnapshot(int Version, ImmutableDictionary<string, decimal> Prices);

// Exercise 05 — see practice/Exercise05-Immutability.md.
//
// A pricing catalogue read on every request and republished a few times an hour. Replace the
// lock-on-every-read design with copy-on-write, so that READERS NEVER BLOCK.
public sealed class PriceCatalogue
{
    /// <summary>Must take no lock. Returns null when the SKU is unknown.</summary>
    public decimal? PriceOf(string sku) =>
        throw new NotImplementedException("Exercise 05: see practice/Exercise05-Immutability.md");

    /// <summary>A snapshot handed out here must never change, however many publishes follow.</summary>
    public PriceSnapshot Current =>
        throw new NotImplementedException("Exercise 05: see practice/Exercise05-Immutability.md");

    public int Version =>
        throw new NotImplementedException("Exercise 05: see practice/Exercise05-Immutability.md");

    /// <summary>
    /// Applies the whole batch atomically - no reader may ever observe half of it - and raises the
    /// version by exactly one, even when several threads publish at the same moment.
    /// </summary>
    public void Publish(IEnumerable<PriceChange> changes) =>
        throw new NotImplementedException("Exercise 05: see practice/Exercise05-Immutability.md");
}

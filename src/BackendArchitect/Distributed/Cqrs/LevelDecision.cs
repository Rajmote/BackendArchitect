namespace BackendArchitect.Distributed.Cqrs;

public sealed record UseCase(
    string Name,
    bool NeedsDifferentTechnology,
    bool StalenessAcceptable,
    int ReadToWriteRatio,
    bool QueryIsMerelySlow);

public sealed record LevelVerdict(string UseCase, int Level, string Because);

// CQRS is applied PER USE CASE, not per system. "We're a CQRS shop" is the same mistake as
// "we're an AP shop" in §8.1.
//
// The discriminator that matters: level 3 is justified by a different TECHNOLOGY or a different
// SCALING PROFILE - never by "the query is slow". Slow is an optimisation problem (index, flat DTO,
// materialized view, cache). "SQL fundamentally cannot rank or fuzzy-match" is an architecture one.
public static class LevelDecision
{
    public static LevelVerdict Decide(UseCase useCase)
    {
        if (useCase.NeedsDifferentTechnology && useCase.StalenessAcceptable)
            return new LevelVerdict(useCase.Name, 3, "needs a different kind of store, and staleness is acceptable");

        if (useCase.NeedsDifferentTechnology)
            return new LevelVerdict(useCase.Name, 2, "needs a different store but cannot tolerate staleness - solve it in the write store");

        if (useCase.ReadToWriteRatio >= 100 && useCase.StalenessAcceptable)
            return new LevelVerdict(useCase.Name, 3, "lopsided read load worth scaling independently");

        return new LevelVerdict(useCase.Name, 2,
            useCase.QueryIsMerelySlow
                ? "the query is slow - index, flat DTO or materialized view, not a second datastore"
                : "a separate read model in the same database is enough");
    }

    /// <summary>One system, several use cases, several answers.</summary>
    public static IReadOnlyList<LevelVerdict> DecideAll() =>
    [
        Decide(new UseCase("order list page", NeedsDifferentTechnology: false, StalenessAcceptable: false,
            ReadToWriteRatio: 40, QueryIsMerelySlow: true)),

        Decide(new UseCase("order detail page", NeedsDifferentTechnology: false, StalenessAcceptable: false,
            ReadToWriteRatio: 20, QueryIsMerelySlow: false)),

        Decide(new UseCase("full-text search", NeedsDifferentTechnology: true, StalenessAcceptable: true,
            ReadToWriteRatio: 500, QueryIsMerelySlow: false)),

        Decide(new UseCase("analytics dashboard", NeedsDifferentTechnology: false, StalenessAcceptable: true,
            ReadToWriteRatio: 300, QueryIsMerelySlow: false)),

        Decide(new UseCase("account balance", NeedsDifferentTechnology: true, StalenessAcceptable: false,
            ReadToWriteRatio: 200, QueryIsMerelySlow: false))
    ];
}

namespace BackendArchitect.Apis.Rest.Design;

public enum ChangeKind
{
    AddOptionalResponseField,
    AddEndpoint,
    AddOptionalQueryParameter,
    RemoveResponseField,
    RenameResponseField,
    ChangeFieldType,
    MakeOptionalFieldRequired,
    ChangeStatusCodeSemantics,
}

// Classifies an API change as breaking or not. The rule of thumb:
//   ADDING something optional is free; REMOVING, RENAMING, RETYPING or TIGHTENING is breaking.
//
// "Free" assumes clients are TOLERANT READERS — they ignore fields they don't recognise. That
// expectation belongs in your published API guidelines, because it's what allows years of additive
// evolution without ever cutting a v2.
public static class ApiChange
{
    public static bool IsBreaking(ChangeKind change) => change switch
    {
        ChangeKind.AddOptionalResponseField => false,
        ChangeKind.AddEndpoint => false,
        ChangeKind.AddOptionalQueryParameter => false,

        ChangeKind.RemoveResponseField => true,
        ChangeKind.RenameResponseField => true,          // = remove + add; often fails SILENTLY
        ChangeKind.ChangeFieldType => true,
        ChangeKind.MakeOptionalFieldRequired => true,
        ChangeKind.ChangeStatusCodeSemantics => true,

        _ => true,   // unknown change: assume breaking
    };

    /// <summary>A new version is only warranted by a breaking change.</summary>
    public static bool RequiresNewVersion(ChangeKind change) => IsBreaking(change);
}

using System.Collections.Immutable;
using BackendArchitect.Concurrency.Immutability;

namespace BackendArchitect.Tests.Concurrency;

public class ImmutabilityTests
{
    [Fact]
    public void Readonly_field_does_not_stop_the_dictionary_changing()
    {
        Assert.True(MutabilityTraps.ReadonlyFieldIsNotImmutable().Leaked);
    }

    [Fact]
    public void Record_with_a_list_property_is_still_mutable()
    {
        Assert.True(MutabilityTraps.RecordWithMutableListIsNotImmutable().Leaked);
    }

    [Fact]
    public void Read_only_view_changes_underneath_its_holder()
    {
        var trap = MutabilityTraps.ReadOnlyViewIsNotASnapshot();

        Assert.Equal("view has 2", trap.Before);
        Assert.Equal("view has 3", trap.After);
    }

    [Fact]
    public void Read_only_view_can_be_cast_back_to_the_underlying_list()
    {
        Assert.True(MutabilityTraps.ReadOnlyViewCanBeCastBack().Leaked);
    }

    [Fact]
    public void With_copies_the_reference_not_the_list()
    {
        var trap = MutabilityTraps.WithIsAShallowCopy();

        Assert.Equal("[Latte]", trap.Before);
        Assert.Equal("[Latte, Muffin]", trap.After);
    }

    [Fact]
    public void Deep_immutability_survives_the_same_shallow_copy()
    {
        var trap = MutabilityTraps.DeepImmutabilityHoldsTheLine();

        Assert.False(trap.Leaked);
        Assert.Equal(trap.Before, trap.After);
    }

    [Fact]
    public void Mutating_a_key_property_makes_the_entry_unreachable()
    {
        var trap = MutabilityTraps.MutableKeyIsLostInADictionary();

        Assert.True(trap.Leaked, "changing Id moves the record to a different hash bucket");
    }

    [Fact]
    public void Mutating_a_list_property_does_not_move_the_key()
    {
        // A record hashes its fields with EqualityComparer<T>.Default, which for List<T> is
        // reference-based - so the list's CONTENTS never reach the hash code.
        var trap = MutabilityTraps.ListPropertyDoesNotMoveTheKey();

        Assert.False(trap.Leaked);
    }

    [Fact]
    public void Records_compare_by_value_over_primitive_properties()
    {
        var one = new MutabilityTraps.MutableKey { Id = 1 };
        var same = new MutabilityTraps.MutableKey { Id = 1 };

        Assert.Equal(one, same);
        Assert.Equal(one.GetHashCode(), same.GetHashCode());
    }

    [Fact]
    public void Record_equality_does_not_reach_into_an_ImmutableArray_property()
    {
        var one = new MutabilityTraps.FrozenBasket(1, ["Latte"]);
        var same = new MutabilityTraps.FrozenBasket(1, ["Latte"]);

        // ImmutableArray<T> compares by the UNDERLYING ARRAY REFERENCE, not by contents. Immutable
        // does not imply structural equality - so two equal-looking baskets are not equal.
        Assert.NotEqual(one, same);
        Assert.False(one.Items.Equals(same.Items));
    }

    [Fact]
    public void Structural_comparison_of_an_ImmutableArray_needs_SequenceEqual()
    {
        var one = new MutabilityTraps.FrozenBasket(1, ["Latte"]);
        var same = new MutabilityTraps.FrozenBasket(1, ["Latte"]);

        Assert.True(one.Items.SequenceEqual(same.Items));
    }

    [Fact]
    public void Sharing_the_same_array_instance_does_make_the_records_equal()
    {
        ImmutableArray<string> shared = ["Latte"];

        var one = new MutabilityTraps.FrozenBasket(1, shared);
        var same = new MutabilityTraps.FrozenBasket(1, shared);

        Assert.Equal(one, same);
    }

    [Fact]
    public void With_leaves_the_original_untouched()
    {
        var original = new MutabilityTraps.FrozenBasket(1, ["Latte"]);

        var renamed = original with { Id = 2 };

        Assert.Equal(1, original.Id);
        Assert.Equal(2, renamed.Id);
    }

    [Fact]
    public void Immutable_collections_return_a_new_value_instead_of_mutating()
    {
        var one = ImmutableArray.Create("Latte");

        one.Add("Muffin");                     // result discarded - like calling name.ToUpper()

        Assert.Single(one);
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("copy-on-write")]
    [InlineData("frozen")]
    public void Every_flag_store_stays_consistent_under_concurrent_reads_and_writes(string kind)
    {
        IFeatureFlags flags = kind switch
        {
            "lock" => new LockedFeatureFlags(),
            "copy-on-write" => new CopyOnWriteFeatureFlags(),
            _ => new FrozenFeatureFlags()
        };

        var run = FeatureFlagBenchmark.Measure(flags, kind, readerCount: 4, readsPerReader: 20_000, writes: 50);

        Assert.Equal(80_000, run.ReadsServed);
        Assert.True(run.EnabledObserved > 0, "readers should see enabled flags, not an empty map");
    }

    [Fact]
    public void Copy_on_write_readers_see_a_snapshot_that_cannot_change()
    {
        var flags = new CopyOnWriteFeatureFlags();
        flags.Set("beta", true);

        var before = flags.IsEnabled("beta");
        flags.Set("beta", false);

        // The value read earlier was a snapshot; re-reading gives the new version.
        Assert.True(before);
        Assert.False(flags.IsEnabled("beta"));
    }
}

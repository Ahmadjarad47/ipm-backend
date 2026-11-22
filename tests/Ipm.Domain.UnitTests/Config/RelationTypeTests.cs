using Ipm.Domain.Config;

namespace Ipm.Domain.UnitTests.Config;

public sealed class RelationTypeTests
{
    [Fact]
    public void R6_the_key_is_normalized_and_a_malformed_key_is_refused()
    {
        var ok = RelationType.Create(" Blocks ", "يحجب", "محجوب بواسطة", true, true, true, []);
        var bad = RelationType.Create("1x", "x", "x", true, false, true, []);

        Assert.Equal("blocks", ok.Value.Key);
        Assert.Equal("RelationType.KeyInvalid", bad.Error.Code);
    }

    [Fact]
    public void R6_the_key_can_be_repeated_but_never_changed()
    {
        var type = RelationType.Create("blocks", "يحجب", "محجوب بواسطة", true, true, true, []).Value;

        Assert.True(type.EnsureKeyUnchanged("BLOCKS").IsSuccess);
        Assert.Equal("RelationType.KeyImmutable", type.EnsureKeyUnchanged("stops").Error.Code);
    }

    [Fact]
    public void R7_an_undirected_type_has_the_same_name_on_both_sides()
    {
        var result = RelationType.Create("relates", "مرتبط بـ", "مرتبط معه", false, false, true, []);

        Assert.Equal("RelationType.InverseNameMustMatch", result.Error.Code);
        Assert.True(RelationType.Create("relates", "مرتبط بـ", "مرتبط بـ", false, false, true, []).IsSuccess);
    }

    [Fact]
    public void R7_only_a_directed_type_can_forbid_cycles()
    {
        var result = RelationType.Create("relates", "مرتبط بـ", "مرتبط بـ", false, true, true, []);

        Assert.Equal("RelationType.AcyclicNeedsDirected", result.Error.Code);
    }

    [Fact]
    public void R8_allowed_pairs_are_deduplicated_and_empty_means_any()
    {
        var pair = new TypePair(Guid.CreateVersion7(), Guid.CreateVersion7());

        var type = RelationType.Create("blocks", "يحجب", "محجوب بواسطة", true, true, true, [pair, pair]).Value;

        Assert.Equal([pair], type.AllowedPairs);
        Assert.Empty(RelationType.Create("duplicates", "يكرر", "مكرر بواسطة", true, true, true, []).Value.AllowedPairs);
    }

    [Fact]
    public void Update_applies_the_same_rules_and_leaves_the_type_unchanged_on_failure()
    {
        var type = RelationType.Create("blocks", "يحجب", "محجوب بواسطة", true, true, true, []).Value;

        var result = type.Update("يحجب", "x", isDirected: false, isAcyclic: false, isActive: false, []);

        Assert.True(result.IsFailure);
        Assert.True(type.IsDirected);
        Assert.True(type.IsActive);
        Assert.Equal("محجوب بواسطة", type.InverseName);
    }
}

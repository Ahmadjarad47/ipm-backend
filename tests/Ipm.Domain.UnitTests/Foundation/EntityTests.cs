using Ipm.Domain.Common;

namespace Ipm.Domain.UnitTests.Foundation;

public sealed class EntityTests
{
    private sealed class Sample : AuditableEntity;

    [Fact]
    public void Ids_are_version_7_uuids_and_unique()
    {
        var a = new Sample();
        var b = new Sample();

        Assert.Equal(7, a.Id.Version);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void New_entity_has_version_zero_and_matches_only_that_version()
    {
        var entity = new Sample();

        Assert.True(entity.HasVersion(0));
        Assert.False(entity.HasVersion(1));
    }
}

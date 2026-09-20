using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Identifiers;

public sealed class MapIdTests
{
    [Fact]
    public void Positive_value_is_accepted()
    {
        var mapId = new MapId(1);

        Assert.Equal(1, mapId.Value);
        Assert.False(mapId.IsEmpty);
        Assert.Equal("1", mapId.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_value_throws_domain_exception(int value)
    {
        Assert.Throws<DomainException>(() => { _ = new MapId(value); });
    }

    [Fact]
    public void Default_is_empty()
    {
        Assert.True(MapId.Empty.IsEmpty);
        Assert.True(default(MapId).IsEmpty);
    }

    [Fact]
    public void Maps_are_equal_by_value()
    {
        Assert.Equal(new MapId(7), new MapId(7));
        Assert.NotEqual(new MapId(7), new MapId(8));
    }
}

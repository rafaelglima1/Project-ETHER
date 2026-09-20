using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Tests.World;

public sealed class WorldPositionTests
{
    [Fact]
    public void Valid_position_is_accepted()
    {
        var position = new WorldPosition(new MapId(3), 10, 20);

        Assert.Equal(3, position.MapId.Value);
        Assert.Equal(10, position.X);
        Assert.Equal(20, position.Y);
    }

    [Fact]
    public void Empty_map_is_rejected()
    {
        Assert.Throws<DomainException>(() => { _ = new WorldPosition(MapId.Empty, 0, 0); });
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Negative_coordinates_are_rejected(int x, int y)
    {
        Assert.Throws<DomainException>(() => { _ = new WorldPosition(new MapId(1), x, y); });
    }

    [Fact]
    public void Positions_are_equal_by_value()
    {
        Assert.Equal(
            new WorldPosition(new MapId(1), 1, 2),
            new WorldPosition(new MapId(1), 1, 2));
    }
}

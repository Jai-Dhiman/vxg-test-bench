using VirtualVxg.Simulator;
using Xunit;

namespace VirtualVxg.Tests;

public class UnitConfigTests
{
    [Fact]
    public void Load_BadSpurUnit_PopulatesAllFields()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "VirtualVxg.Simulator", "configs", "unit-bad-spur.json");

        var config = UnitConfig.Load(path);

        Assert.Equal("VXG-BAD-003", config.UnitId);
        Assert.Equal(99, config.Seed);
        Assert.Null(config.RolloffDbPerGhzAbove);
        Assert.Single(config.Spurs);
        Assert.Equal(8e9, config.Spurs[0].CenterHz);
        Assert.Equal(-1.5, config.Spurs[0].DepthDb);
    }
}

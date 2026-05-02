using VirtualVxg.Simulator;
using Xunit;

namespace VirtualVxg.Tests;

public class DefectEngineTests
{
    [Fact]
    public void FlatUnit_ReturnsRequestedPower_WithinNoiseFloor()
    {
        var config = new UnitConfig(
            UnitId: "good-001",
            Seed: 42,
            NoiseFloorDb: 0.005,
            RolloffDbPerGhzAbove: null,
            Spurs: System.Array.Empty<SpurDefect>());
        var engine = new DefectEngine(config);

        var measured = engine.MeasurePowerAt(frequencyHz: 5e9, requestedDbm: 0.0);

        Assert.InRange(measured, -0.01, 0.01);
    }
}

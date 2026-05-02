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

    [Fact]
    public void RolloffUnit_AbovKnee_AttenuatesPower()
    {
        var config = new UnitConfig(
            UnitId: "rolloff-001",
            Seed: 42,
            NoiseFloorDb: 0.005,
            RolloffDbPerGhzAbove: new RolloffDefect(KneeGhz: 30.0, SlopeDbPerGhz: -1.0),
            Spurs: System.Array.Empty<SpurDefect>());
        var engine = new DefectEngine(config);

        var lowBand = engine.MeasurePowerAt(frequencyHz: 5e9, requestedDbm: 0.0);
        var highBand = engine.MeasurePowerAt(frequencyHz: 40e9, requestedDbm: 0.0);

        Assert.InRange(lowBand, -0.01, 0.01);
        Assert.True(highBand <= -8.0, $"expected <= -8 dB at 40 GHz, got {highBand}");
    }
}

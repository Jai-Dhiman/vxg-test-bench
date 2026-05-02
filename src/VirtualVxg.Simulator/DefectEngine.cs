namespace VirtualVxg.Simulator;

public sealed class DefectEngine
{
    private readonly UnitConfig _config;
    private readonly Random _rng;

    public DefectEngine(UnitConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rng = new Random(config.Seed);
    }

    public double MeasurePowerAt(double frequencyHz, double requestedDbm)
    {
        var noise = (_rng.NextDouble() - 0.5) * 2.0 * _config.NoiseFloorDb;
        return requestedDbm + noise;
    }
}

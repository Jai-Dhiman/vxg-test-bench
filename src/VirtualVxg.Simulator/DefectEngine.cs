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
        var rolloff = ComputeRolloff(frequencyHz);
        return requestedDbm + noise + rolloff;
    }

    private double ComputeRolloff(double frequencyHz)
    {
        if (_config.RolloffDbPerGhzAbove is null) return 0.0;
        var freqGhz = frequencyHz / 1e9;
        var excess = freqGhz - _config.RolloffDbPerGhzAbove.KneeGhz;
        if (excess <= 0) return 0.0;
        return excess * _config.RolloffDbPerGhzAbove.SlopeDbPerGhz;
    }
}

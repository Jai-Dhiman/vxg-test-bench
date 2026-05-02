namespace VirtualVxg.Simulator;

public sealed class InstrumentState
{
    public double FrequencyHz { get; set; } = 1e9;
    public double PowerDbm { get; set; } = 0.0;
    public bool OutputOn { get; set; } = false;
}

using System.Globalization;

namespace VirtualVxg.Simulator;

public sealed class ScpiCommandHandler
{
    private const string IdnReply = "Keysight Technologies,M9484C,SIM-0001,VirtualVxg-0.1.0";

    private readonly InstrumentState _state;
    private readonly DefectEngine _defects;
    private readonly object _lock = new();

    public ScpiCommandHandler(InstrumentState state, DefectEngine defects)
    {
        _state = state;
        _defects = defects;
    }

    public string? Handle(string commandLine)
    {
        lock (_lock)
        {
            var trimmed = commandLine.Trim();
            if (trimmed.Length == 0) return null;

            var parts = trimmed.Split(' ', 2);
            var verb = parts[0].ToUpperInvariant();
            var arg = parts.Length > 1 ? parts[1] : "";

            return verb switch
            {
                "*IDN?" => IdnReply,
                "FREQ" => SetFrequency(arg),
                "FREQ?" => _state.FrequencyHz.ToString("F0", CultureInfo.InvariantCulture),
                "POW" => SetPower(arg),
                "POW?" => _state.PowerDbm.ToString("0.0##", CultureInfo.InvariantCulture),
                "OUTP" => SetOutput(arg),
                "MEAS:POW?" => MeasurePower(),
                _ => "-100,\"Command error\""
            };
        }
    }

    private string? SetFrequency(string arg)
    {
        if (!double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
            return "-100,\"Command error\"";
        _state.FrequencyHz = hz;
        return null;
    }

    private string? SetPower(string arg)
    {
        if (!double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbm))
            return "-100,\"Command error\"";
        _state.PowerDbm = dbm;
        return null;
    }

    private string? SetOutput(string arg)
    {
        var v = arg.Trim().ToUpperInvariant();
        switch (v)
        {
            case "ON":
            case "1":
                _state.OutputOn = true;
                return null;
            case "OFF":
            case "0":
                _state.OutputOn = false;
                return null;
            default:
                return "-100,\"Command error\"";
        }
    }

    private string MeasurePower()
    {
        if (!_state.OutputOn) return "-200.0";
        var measured = _defects.MeasurePowerAt(_state.FrequencyHz, _state.PowerDbm);
        return measured.ToString("0.0####", CultureInfo.InvariantCulture);
    }
}

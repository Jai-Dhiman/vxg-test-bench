using System.Globalization;

namespace VirtualVxg.Simulator;

public sealed class ScpiCommandHandler
{
    private const string IdnReply = "Keysight Technologies,M9484C,SIM-0001,VirtualVxg-0.1.0";

    private readonly InstrumentState _state;
    private readonly DefectEngine _defects;

    public ScpiCommandHandler(InstrumentState state, DefectEngine defects)
    {
        _state = state;
        _defects = defects;
    }

    public string? Handle(string commandLine)
    {
        var trimmed = commandLine.Trim();
        if (trimmed.Length == 0) return null;

        var verb = trimmed.Split(' ', 2)[0].ToUpperInvariant();
        return verb switch
        {
            "*IDN?" => IdnReply,
            _ => "-100,\"Command error\""
        };
    }
}

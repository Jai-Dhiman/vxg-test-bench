using VirtualVxg.Simulator;
using Xunit;

namespace VirtualVxg.Tests;

public class ScpiCommandHandlerTests
{
    private static ScpiCommandHandler MakeHandler() =>
        new(new InstrumentState(), new DefectEngine(new UnitConfig(
            "test", 42, 0.0, null, System.Array.Empty<SpurDefect>())));

    [Fact]
    public void Idn_Query_ReturnsKeysightM9484CIdentifier()
    {
        var handler = MakeHandler();
        var reply = handler.Handle("*IDN?");
        Assert.Equal("Keysight Technologies,M9484C,SIM-0001,VirtualVxg-0.1.0", reply);
    }

    [Fact]
    public void Freq_SetThenQuery_RoundTripsExactValue()
    {
        var handler = MakeHandler();
        handler.Handle("FREQ 2.4e9");
        var reply = handler.Handle("FREQ?");
        Assert.Equal("2400000000", reply);
    }

    [Fact]
    public void Pow_SetThenQuery_RoundTripsExactValue()
    {
        var handler = MakeHandler();
        handler.Handle("POW -10.5");
        var reply = handler.Handle("POW?");
        Assert.Equal("-10.5", reply);
    }
}

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

    [Fact]
    public void MeasPow_OutputOn_ReturnsDefectEnginePower()
    {
        var handler = MakeHandler();
        handler.Handle("FREQ 5e9");
        handler.Handle("POW 0");
        handler.Handle("OUTP ON");
        var reply = handler.Handle("MEAS:POW?");

        var measured = double.Parse(reply!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(measured, -0.01, 0.01);
    }

    [Fact]
    public void MeasPow_OutputOff_ReturnsSentinelBelowNoiseFloor()
    {
        var handler = MakeHandler();
        handler.Handle("OUTP OFF");
        var reply = handler.Handle("MEAS:POW?");
        Assert.Equal("-200.0", reply);
    }

    [Fact]
    public void UnknownVerb_ReturnsScpiCommandError()
    {
        var handler = MakeHandler();
        var reply = handler.Handle("FOOBAR 123");
        Assert.Equal("-100,\"Command error\"", reply);
    }

    [Fact]
    public void EmptyLine_ReturnsNullNoReply()
    {
        var handler = MakeHandler();
        Assert.Null(handler.Handle(""));
        Assert.Null(handler.Handle("   "));
    }
}

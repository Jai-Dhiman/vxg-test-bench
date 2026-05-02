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
}

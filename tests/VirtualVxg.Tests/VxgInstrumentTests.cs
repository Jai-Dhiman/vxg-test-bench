using System.Net.Sockets;
using VirtualVxg.OpenTapPlugin;
using VirtualVxg.Simulator;
using Xunit;

namespace VirtualVxg.Tests;

public class VxgInstrumentTests
{
    [Fact]
    public async Task SetFrequency_SetPower_Measure_RoundTripsWithinTolerance()
    {
        var state = new InstrumentState();
        var defects = new DefectEngine(new UnitConfig(
            "test", 42, 0.005, null, Array.Empty<SpurDefect>()));
        var handler = new ScpiCommandHandler(state, defects);
        var server = new ScpiServer(handler);
        await server.StartAsync(0, CancellationToken.None);

        try
        {
            var instrument = new VxgInstrument
            {
                Host = "127.0.0.1",
                Port = server.BoundPort
            };
            instrument.Open();
            try
            {
                instrument.SetFrequency(5e9);
                instrument.SetPower(0.0);
                instrument.EnableOutput();
                var measured = instrument.MeasurePower();
                Assert.InRange(measured, -0.05, 0.05);
            }
            finally { instrument.Close(); }
        }
        finally { await server.StopAsync(); }
    }
}

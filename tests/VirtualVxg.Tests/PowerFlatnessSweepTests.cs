using OpenTap;
using VirtualVxg.OpenTapPlugin;
using VirtualVxg.Simulator;
using Xunit;
using System.Net.Sockets;

namespace VirtualVxg.Tests;

public class PowerFlatnessSweepTests
{
    [Fact]
    public async Task GoodUnit_AllPointsWithinTolerance_VerdictPass()
    {
        var (server, port) = await StartSimulator(new UnitConfig(
            "good", 42, 0.005, null, Array.Empty<SpurDefect>()));

        try
        {
            var instrument = new VxgInstrument { Host = "127.0.0.1", Port = port };
            instrument.Open();
            try
            {
                var step = new PowerFlatnessSweep
                {
                    Instrument = instrument,
                    StartFreqHz = 1e9,
                    StopFreqHz = 6e9,
                    StepFreqHz = 1e9,
                    NominalPowerDbm = 0.0,
                    ToleranceDb = 0.5
                };
                step.Run();

                Assert.Equal(Verdict.Pass, step.LastVerdict);
                Assert.Equal(0, step.FailedPointCount);
            }
            finally { instrument.Close(); }
        }
        finally { await server.StopAsync(); }
    }

    [Fact]
    public async Task BadUnit_WithSpurAt12Ghz_VerdictFail_GathersFullCurve()
    {
        var (server, port) = await StartSimulator(new UnitConfig(
            "bad", 99, 0.005, null,
            new[] { new SpurDefect(12e9, 200e6, -1.5) }));

        try
        {
            var instrument = new VxgInstrument { Host = "127.0.0.1", Port = port };
            instrument.Open();
            try
            {
                var step = new PowerFlatnessSweep
                {
                    Instrument = instrument,
                    StartFreqHz = 11e9,
                    StopFreqHz = 13e9,
                    StepFreqHz = 100e6,
                    NominalPowerDbm = 0.0,
                    ToleranceDb = 0.5
                };
                step.Run();

                Assert.Equal(Verdict.Fail, step.LastVerdict);
                Assert.True(step.FailedPointCount >= 1,
                    $"expected >= 1 failed point near spur, got {step.FailedPointCount}");
            }
            finally { instrument.Close(); }
        }
        finally { await server.StopAsync(); }
    }

    private static async Task<(ScpiServer server, int port)> StartSimulator(UnitConfig config)
    {
        var handler = new ScpiCommandHandler(new InstrumentState(), new DefectEngine(config));
        var server = new ScpiServer(handler);
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        await server.StartAsync(port, CancellationToken.None);
        return (server, port);
    }
}

using System.Net.Sockets;
using System.Text;
using VirtualVxg.Simulator;
using Xunit;

namespace VirtualVxg.Tests;

public class ScpiServerTests
{
    [Fact]
    public async Task Client_SendsIdnQuery_ReceivesIdentifier()
    {
        var state = new InstrumentState();
        var defects = new DefectEngine(new UnitConfig(
            "test", 42, 0.0, null, Array.Empty<SpurDefect>()));
        var handler = new ScpiCommandHandler(state, defects);
        var server = new ScpiServer(handler);
        await server.StartAsync(0, CancellationToken.None);
        var port = server.BoundPort;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            using var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes("*IDN?\n");
            await stream.WriteAsync(request);

            var buffer = new byte[256];
            var n = await stream.ReadAsync(buffer);
            var reply = Encoding.ASCII.GetString(buffer, 0, n).TrimEnd('\n', '\r');

            Assert.Equal("Keysight Technologies,M9484C,SIM-0001,VirtualVxg-0.1.0", reply);
        }
        finally
        {
            await server.StopAsync();
        }
    }

}

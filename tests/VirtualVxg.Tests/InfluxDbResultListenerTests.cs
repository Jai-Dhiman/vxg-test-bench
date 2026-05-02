using System.Net;
using System.Text;
using OpenTap;
using VirtualVxg.OpenTapPlugin;
using Xunit;

namespace VirtualVxg.Tests;

public class InfluxDbResultListenerTests
{
    [Fact]
    public async Task PublishingFivePoints_PostsFiveLines()
    {
        var listener = new HttpListener();
        var port = GetFreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var capturedBody = "";
        var captureTask = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            capturedBody = await reader.ReadToEndAsync();
            ctx.Response.StatusCode = 204;
            ctx.Response.Close();
        });

        var sink = new InfluxDbResultListener
        {
            Url = $"http://127.0.0.1:{port}",
            Bucket = "vxg_tests",
            Org = "demo",
            Token = "test-token"
        };

        var table = new ResultTable("PowerFlatness",
            new[]
            {
                new ResultColumn("unit_id", new string[] { "u1", "u1", "u1", "u1", "u1" }),
                new ResultColumn("frequency_hz", new double[] { 1e9, 2e9, 3e9, 4e9, 5e9 }),
                new ResultColumn("power_dbm", new double[] { 0.01, -0.02, 0.0, 0.03, -0.01 }),
                new ResultColumn("pass", new bool[] { true, true, true, true, true })
            });

        sink.OnResultPublished(Guid.NewGuid(), table);
        await captureTask;
        listener.Stop();

        var lines = capturedBody.Trim().Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.All(lines, l => Assert.StartsWith("PowerFlatness,unit_id=u1", l));
    }

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}

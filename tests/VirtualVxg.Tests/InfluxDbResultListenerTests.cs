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
                new ResultColumn("run_id", new string[] { "abc", "abc", "abc", "abc", "abc" }),
                new ResultColumn("frequency_hz", new double[] { 1e9, 2e9, 3e9, 4e9, 5e9 }),
                new ResultColumn("power_dbm", new double[] { 0.01, -0.02, 0.0, 0.03, -0.01 }),
                new ResultColumn("pass", new bool[] { true, true, true, true, true }),
                new ResultColumn("nominal_dbm", new double[] { 0.0, 0.0, 0.0, 0.0, 0.0 }),
                new ResultColumn("tolerance_db", new double[] { 0.5, 0.5, 0.5, 0.5, 0.5 })
            });

        sink.OnResultPublished(Guid.NewGuid(), table);
        await captureTask;
        listener.Stop();

        var lines = capturedBody.Trim().Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.All(lines, l =>
        {
            Assert.StartsWith("PowerFlatness,unit_id=u1,run_id=abc", l);
            Assert.Contains("nominal_dbm=0", l);
            Assert.Contains("tolerance_db=0.5", l);
        });
    }

    [Fact]
    public async Task PublishingRunSummary_PostsSingleLine()
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

        var table = new ResultTable("PowerFlatnessRun",
            new[]
            {
                new ResultColumn("unit_id", new string[] { "u1" }),
                new ResultColumn("run_id", new string[] { "deadbeef" }),
                new ResultColumn("verdict", new bool[] { false }),
                new ResultColumn("failed_point_count", new int[] { 3 }),
                new ResultColumn("point_count", new int[] { 40 }),
                new ResultColumn("nominal_dbm", new double[] { 0.0 }),
                new ResultColumn("tolerance_db", new double[] { 0.5 }),
                new ResultColumn("start_freq_hz", new double[] { 1e9 }),
                new ResultColumn("stop_freq_hz", new double[] { 20e9 })
            });

        sink.OnResultPublished(Guid.NewGuid(), table);
        await captureTask;
        listener.Stop();

        var lines = capturedBody.Trim().Split('\n');
        Assert.Single(lines);
        var line = lines[0];
        Assert.StartsWith("PowerFlatnessRun,unit_id=u1,run_id=deadbeef ", line);
        Assert.Contains("verdict=false", line);
        Assert.Contains("failed_point_count=3i", line);
        Assert.Contains("point_count=40i", line);
        Assert.Contains("start_freq_hz=1000000000", line);
    }

    [Fact]
    public void TableMissingRequiredColumns_IsSkipped()
    {
        var sink = new InfluxDbResultListener
        {
            Url = "http://127.0.0.1:1",
            Bucket = "vxg_tests",
            Org = "demo",
            Token = "test-token"
        };

        var perPointMissing = new ResultTable("PowerFlatness",
            new[] { new ResultColumn("unit_id", new string[] { "u1" }) });
        var runMissing = new ResultTable("PowerFlatnessRun",
            new[] { new ResultColumn("unit_id", new string[] { "u1" }) });
        var unknown = new ResultTable("Something",
            new[] { new ResultColumn("unit_id", new string[] { "u1" }) });

        sink.OnResultPublished(Guid.NewGuid(), perPointMissing);
        sink.OnResultPublished(Guid.NewGuid(), runMissing);
        sink.OnResultPublished(Guid.NewGuid(), unknown);
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

using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using OpenTap;

namespace VirtualVxg.OpenTapPlugin;

[Display("InfluxDB Result Listener", "Forwards results to InfluxDB v2 via line protocol.", "VirtualVxg")]
public class InfluxDbResultListener : ResultListener
{
    [Display("URL")] public string Url { get; set; } = "http://localhost:8086";
    [Display("Bucket")] public string Bucket { get; set; } = "vxg_tests";
    [Display("Org")] public string Org { get; set; } = "demo";
    [Display("Token")] public string Token { get; set; } = "";

    private static readonly HttpClient Http = new();

    public override void OnResultPublished(Guid stepRunId, ResultTable result)
    {
        try
        {
            var body = FormatLineProtocol(result);
            if (string.IsNullOrEmpty(body)) return;

            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{Url}/api/v2/write?bucket={Uri.EscapeDataString(Bucket)}&org={Uri.EscapeDataString(Org)}&precision=ns");
            if (!string.IsNullOrEmpty(Token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Token", Token);
            req.Content = new StringContent(body, Encoding.UTF8, "text/plain");

            var resp = Http.Send(req);
            if (!resp.IsSuccessStatusCode)
                Log.Warning($"InfluxDB write failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            Log.Warning($"InfluxDB write threw: {ex.Message}");
        }
    }

    public override void Open() { }
    public override void Close() { }

    private static string FormatLineProtocol(ResultTable result)
    {
        var unitIdCol = FindColumn(result, "unit_id");
        var freqCol = FindColumn(result, "frequency_hz");
        var powerCol = FindColumn(result, "power_dbm");
        var passCol = FindColumn(result, "pass");
        if (unitIdCol is null || freqCol is null || powerCol is null) return "";

        var sb = new StringBuilder();
        var nowNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        for (var i = 0; i < result.Rows; i++)
        {
            var unit = unitIdCol.Data.GetValue(i)?.ToString() ?? "unknown";
            var freq = Convert.ToDouble(freqCol.Data.GetValue(i), CultureInfo.InvariantCulture);
            var power = Convert.ToDouble(powerCol.Data.GetValue(i), CultureInfo.InvariantCulture);
            var pass = passCol is not null
                ? Convert.ToBoolean(passCol.Data.GetValue(i)) ? "true" : "false"
                : "true";
            sb.Append(result.Name)
              .Append(",unit_id=").Append(unit)
              .Append(" frequency_hz=").Append(freq.ToString("R", CultureInfo.InvariantCulture))
              .Append(",power_dbm=").Append(power.ToString("R", CultureInfo.InvariantCulture))
              .Append(",pass=").Append(pass)
              .Append(' ').Append(nowNs + i)
              .Append('\n');
        }
        return sb.ToString();
    }

    private static ResultColumn? FindColumn(ResultTable t, string name) =>
        t.Columns.FirstOrDefault(c => c.Name == name);
}

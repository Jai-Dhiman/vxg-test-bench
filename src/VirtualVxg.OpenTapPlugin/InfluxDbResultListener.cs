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

    private static string EscapeTagValue(string v) =>
        v.Replace(@"\", @"\\").Replace(",", @"\,").Replace("=", @"\=").Replace(" ", @"\ ");

    private static string FormatLineProtocol(ResultTable result)
    {
        var measurementName = EscapeTagValue(result.Name);
        return result.Name switch
        {
            "PowerFlatness" => FormatPerPoint(result, measurementName),
            "PowerFlatnessRun" => FormatRunSummary(result, measurementName),
            _ => ""
        };
    }

    private static string FormatPerPoint(ResultTable result, string measurementName)
    {
        var unitIdCol = FindColumn(result, "unit_id");
        var runIdCol = FindColumn(result, "run_id");
        var freqCol = FindColumn(result, "frequency_hz");
        var powerCol = FindColumn(result, "power_dbm");
        var passCol = FindColumn(result, "pass");
        var nomCol = FindColumn(result, "nominal_dbm");
        var tolCol = FindColumn(result, "tolerance_db");
        if (unitIdCol is null || freqCol is null || powerCol is null) return "";

        var sb = new StringBuilder();
        var nowNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        for (var i = 0; i < result.Rows; i++)
        {
            var unit = EscapeTagValue(unitIdCol.Data.GetValue(i)?.ToString() ?? "unknown");
            var freq = Convert.ToDouble(freqCol.Data.GetValue(i), CultureInfo.InvariantCulture);
            var power = Convert.ToDouble(powerCol.Data.GetValue(i), CultureInfo.InvariantCulture);
            var pass = passCol is not null
                ? Convert.ToBoolean(passCol.Data.GetValue(i)) ? "true" : "false"
                : "true";
            sb.Append(measurementName).Append(",unit_id=").Append(unit);
            if (runIdCol is not null)
                sb.Append(",run_id=").Append(EscapeTagValue(runIdCol.Data.GetValue(i)?.ToString() ?? ""));
            sb.Append(" frequency_hz=").Append(freq.ToString("R", CultureInfo.InvariantCulture))
              .Append(",power_dbm=").Append(power.ToString("R", CultureInfo.InvariantCulture))
              .Append(",pass=").Append(pass);
            if (nomCol is not null)
            {
                var nom = Convert.ToDouble(nomCol.Data.GetValue(i), CultureInfo.InvariantCulture);
                sb.Append(",nominal_dbm=").Append(nom.ToString("R", CultureInfo.InvariantCulture));
            }
            if (tolCol is not null)
            {
                var tol = Convert.ToDouble(tolCol.Data.GetValue(i), CultureInfo.InvariantCulture);
                sb.Append(",tolerance_db=").Append(tol.ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append(' ').Append(nowNs + i).Append('\n');
        }
        return sb.ToString();
    }

    private static string FormatRunSummary(ResultTable result, string measurementName)
    {
        var unitIdCol = FindColumn(result, "unit_id");
        var runIdCol = FindColumn(result, "run_id");
        var verdictCol = FindColumn(result, "verdict");
        if (unitIdCol is null || runIdCol is null || verdictCol is null) return "";

        var failedCol = FindColumn(result, "failed_point_count");
        var pointCol = FindColumn(result, "point_count");
        var nomCol = FindColumn(result, "nominal_dbm");
        var tolCol = FindColumn(result, "tolerance_db");
        var startCol = FindColumn(result, "start_freq_hz");
        var stopCol = FindColumn(result, "stop_freq_hz");

        var sb = new StringBuilder();
        var nowNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        for (var i = 0; i < result.Rows; i++)
        {
            var unit = EscapeTagValue(unitIdCol.Data.GetValue(i)?.ToString() ?? "unknown");
            var runId = EscapeTagValue(runIdCol.Data.GetValue(i)?.ToString() ?? "");
            var verdict = Convert.ToBoolean(verdictCol.Data.GetValue(i)) ? "true" : "false";
            sb.Append(measurementName)
              .Append(",unit_id=").Append(unit)
              .Append(",run_id=").Append(runId)
              .Append(" verdict=").Append(verdict);
            if (failedCol is not null)
                sb.Append(",failed_point_count=").Append(Convert.ToInt64(failedCol.Data.GetValue(i))).Append('i');
            if (pointCol is not null)
                sb.Append(",point_count=").Append(Convert.ToInt64(pointCol.Data.GetValue(i))).Append('i');
            if (nomCol is not null)
                sb.Append(",nominal_dbm=").Append(Convert.ToDouble(nomCol.Data.GetValue(i), CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
            if (tolCol is not null)
                sb.Append(",tolerance_db=").Append(Convert.ToDouble(tolCol.Data.GetValue(i), CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
            if (startCol is not null)
                sb.Append(",start_freq_hz=").Append(Convert.ToDouble(startCol.Data.GetValue(i), CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
            if (stopCol is not null)
                sb.Append(",stop_freq_hz=").Append(Convert.ToDouble(stopCol.Data.GetValue(i), CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
            sb.Append(' ').Append(nowNs + i).Append('\n');
        }
        return sb.ToString();
    }

    private static ResultColumn? FindColumn(ResultTable t, string name) =>
        t.Columns.FirstOrDefault(c => c.Name == name);
}

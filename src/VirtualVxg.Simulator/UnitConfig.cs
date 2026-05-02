using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtualVxg.Simulator;

public record SpurDefect(
    [property: JsonPropertyName("center_hz")] double CenterHz,
    [property: JsonPropertyName("width_hz")] double WidthHz,
    [property: JsonPropertyName("depth_db")] double DepthDb);

public record UnitConfig(
    [property: JsonPropertyName("unit_id")] string UnitId,
    [property: JsonPropertyName("seed")] int Seed,
    [property: JsonPropertyName("noise_floor_db")] double NoiseFloorDb,
    [property: JsonPropertyName("rolloff_db_per_ghz_above_ghz")] RolloffDefect? RolloffDbPerGhzAbove,
    [property: JsonPropertyName("spurs")] SpurDefect[] Spurs)
{
    public static UnitConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<UnitConfig>(json)
            ?? throw new InvalidOperationException($"Failed to parse unit config: {path}");
        return cfg;
    }
}

public record RolloffDefect(
    [property: JsonPropertyName("knee_ghz")] double KneeGhz,
    [property: JsonPropertyName("slope_db_per_ghz")] double SlopeDbPerGhz);

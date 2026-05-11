using System.ComponentModel;
using OpenTap;

namespace VirtualVxg.OpenTapPlugin;

[Display("Power Flatness Sweep", "Sweeps frequency at fixed power, asserts each point within tolerance.", "VirtualVxg")]
public class PowerFlatnessSweep : TestStep
{
    [Display("Instrument")] public VxgInstrument Instrument { get; set; } = null!;
    [Display("Start Frequency (Hz)")] public double StartFreqHz { get; set; } = 1e9;
    [Display("Stop Frequency (Hz)")] public double StopFreqHz { get; set; } = 40e9;
    [Display("Step (Hz)")] public double StepFreqHz { get; set; } = 100e6;
    [Display("Nominal Power (dBm)")] public double NominalPowerDbm { get; set; } = 0.0;
    [Display("Tolerance (dB)")] public double ToleranceDb { get; set; } = 0.5;
    [Display("Unit ID")] public string UnitId { get; set; } = "unknown";

    [Browsable(false)] public Verdict LastVerdict { get; private set; }
    [Browsable(false)] public int FailedPointCount { get; private set; }

    public override void Run()
    {
        var runId = Guid.NewGuid().ToString("N");
        Instrument.SetPower(NominalPowerDbm);
        Instrument.EnableOutput();
        var failed = 0;
        var freqColumn = new List<double>();
        var powerColumn = new List<double>();
        var passColumn = new List<bool>();

        for (var f = StartFreqHz; f <= StopFreqHz + 1e-3; f += StepFreqHz)
        {
            Instrument.SetFrequency(f);
            var measured = Instrument.MeasurePower();
            var pass = Math.Abs(measured - NominalPowerDbm) <= ToleranceDb;
            if (!pass) failed++;
            freqColumn.Add(f);
            powerColumn.Add(measured);
            passColumn.Add(pass);
        }

        var n = freqColumn.Count;
        Results?.PublishTable("PowerFlatness",
            new List<string> { "unit_id", "run_id", "frequency_hz", "power_dbm", "pass", "nominal_dbm", "tolerance_db" },
            new Array[]
            {
                Enumerable.Repeat(UnitId, n).ToArray(),
                Enumerable.Repeat(runId, n).ToArray(),
                freqColumn.ToArray(),
                powerColumn.ToArray(),
                passColumn.ToArray(),
                Enumerable.Repeat(NominalPowerDbm, n).ToArray(),
                Enumerable.Repeat(ToleranceDb, n).ToArray()
            });

        FailedPointCount = failed;
        LastVerdict = failed == 0 ? Verdict.Pass : Verdict.Fail;

        Results?.PublishTable("PowerFlatnessRun",
            new List<string> { "unit_id", "run_id", "verdict", "failed_point_count", "point_count", "nominal_dbm", "tolerance_db", "start_freq_hz", "stop_freq_hz" },
            new Array[]
            {
                new[] { UnitId },
                new[] { runId },
                new[] { failed == 0 },
                new[] { failed },
                new[] { n },
                new[] { NominalPowerDbm },
                new[] { ToleranceDb },
                new[] { StartFreqHz },
                new[] { StopFreqHz }
            });

        UpgradeVerdict(LastVerdict);
    }
}

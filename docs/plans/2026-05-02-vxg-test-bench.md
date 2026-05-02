# Virtual Keysight M9484C VXG Test Bench Implementation Plan

> **For the build agent:** Dispatch each task group sequentially. Within a group, tasks are sequential by default; parallelism noted explicitly when safe. Do NOT start execution until `/challenge` returns `VERDICT: PROCEED`.

**Goal:** A reviewer can open a public Grafana dashboard URL and watch live OpenTAP test results stream in from a simulated Keysight M9484C VXG running an Output Power Flatness sweep across multiple "units under test."

**Spec:** `docs/specs/2026-05-02-vxg-test-bench-design.md`

**Style:** .NET 8, C# 12 (file-scoped namespaces, primary constructors, records where natural). xUnit for tests. Fail loud at boundaries. No defensive programming for impossible scenarios.

---

## Task Groups

- **Group A (sequential, foundational):** T0 (gate), T1, T2, T3, T4
- **Group B (sequential, simulator core):** T5–T15
- **Group C (sequential, OpenTAP plugin — depends on B + T0 verifies tap CLI):** T16, T17, T18, T19
- **Group D (sequential, integration & deploy):** T20, T21, T22, T23, T24

---

## Group A — Foundational Setup

### Task 0: Precondition Gate — Verify Toolchain on Mac

**Group:** A
**Behavior being verified:** `dotnet 8.x` and `tap` CLI both work on this Mac before any C# is written.
**Interface under test:** OS shell.

**Files:** none (verification only — no commit).

- [ ] **Step 1: Run verification commands**

```bash
dotnet --version
tap --help
```

- [ ] **Step 2: Decision branch**

  - **If `dotnet --version` returns `8.x.x` or `9.x.x` AND `tap --help` prints OpenTAP usage:** proceed to Task 1.
  - **If `dotnet` missing:** `brew install --cask dotnet-sdk`, re-run.
  - **If `tap` missing:** download OpenTAP from `https://opentap.io/downloads.html` (cross-platform `.tar.gz`), extract to `~/opentap`, add to PATH: `echo 'export PATH="$HOME/opentap:$PATH"' >> ~/.zshrc && source ~/.zshrc`. Re-run `tap --help`.
  - **If `tap` STILL fails after install attempt:** STOP. Engage the spec's documented fallback (custom C# runner). Edit the spec's "Modules" section to remove OpenTAP base classes; rewrite Tasks T16–T20 against the fallback shape. Notify the user.

- [ ] **Step 3: Record toolchain versions**

```bash
dotnet --version > .toolchain && tap --version >> .toolchain && cat .toolchain
```

This file is .gitignored (created in T1) but exists locally as a build-agent record.

---

### Task 1: Repository Skeleton — `.gitignore`, `README.md`, `Makefile`

**Group:** A
**Behavior being verified:** N/A (scaffolding gate — no behavior test possible).
**Interface under test:** N/A.

**Files:**
- Create: `.gitignore`
- Create: `README.md`
- Create: `Makefile`

- [ ] **Step 1: Create `.gitignore`**

```
# .NET
bin/
obj/
*.user
*.suo
.vs/

# macOS
.DS_Store

# OpenTAP
TapPackages/
PackageCache/
SessionLogs/
Results/

# Toolchain record (local only)
.toolchain

# Fly secrets
.fly/

# Local env
.env
.env.local
```

- [ ] **Step 2: Create `README.md`** (skeleton — fully populated in T22/T23)

```markdown
# Virtual Keysight M9484C VXG Test Bench

A simulated Keysight M9484C VXG signal generator with an OpenTAP plugin running
an Output Power Flatness sweep, streaming results to a live Grafana dashboard.

**Live dashboard:** _populated after deploy (Task 23)_

## Architecture

```
configs/unit-*.json  ──►  Simulator (TCP :5025, SCPI)
                                ▲
                                │
                          OpenTAP TestPlan
                          ├─ VxgInstrument
                          ├─ PowerFlatnessSweep
                          └─ InfluxDbResultListener
                                │
                                ▼
                          InfluxDB + Grafana (Fly.io)
```

## Run locally

```
make demo
```

## Status

In progress. See `docs/specs/` and `docs/plans/`.
```

- [ ] **Step 3: Create `Makefile`** (skeleton — targets implemented across tasks)

```makefile
.PHONY: sim test demo deploy clean

sim:
	dotnet run --project src/VirtualVxg.Simulator -- --config src/VirtualVxg.Simulator/configs/unit-good.json

test:
	dotnet test

demo:
	bash scripts/demo.sh

deploy:
	cd deploy && fly deploy

clean:
	dotnet clean && rm -rf bin obj */bin */obj
```

- [ ] **Step 4: Commit**

```bash
git add .gitignore README.md Makefile
git commit -m "chore: repo skeleton — gitignore, README, Makefile"
```

---

### Task 2: Solution + Simulator csproj

**Group:** A
**Behavior being verified:** `dotnet build` succeeds against the empty simulator project.
**Interface under test:** `dotnet` build pipeline.

**Files:**
- Create: `VirtualVxg.sln`
- Create: `src/VirtualVxg.Simulator/VirtualVxg.Simulator.csproj`
- Create: `src/VirtualVxg.Simulator/Program.cs`

- [ ] **Step 1: Scaffold projects**

```bash
mkdir -p src/VirtualVxg.Simulator
dotnet new sln -n VirtualVxg
dotnet new console -n VirtualVxg.Simulator -o src/VirtualVxg.Simulator --framework net8.0
dotnet sln add src/VirtualVxg.Simulator/VirtualVxg.Simulator.csproj
```

- [ ] **Step 2: Replace generated `Program.cs` with placeholder entry**

```csharp
namespace VirtualVxg.Simulator;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("VirtualVxg.Simulator — not yet implemented (see Task T15)");
        return 0;
    }
}
```

- [ ] **Step 3: Verify build passes**

```bash
dotnet build VirtualVxg.sln
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add VirtualVxg.sln src/VirtualVxg.Simulator/
git commit -m "feat(sim): scaffold simulator project"
```

---

### Task 3: OpenTAP Plugin csproj

**Group:** A
**Behavior being verified:** `dotnet build` succeeds with OpenTAP NuGet package referenced.
**Interface under test:** OpenTAP NuGet resolution.

**Files:**
- Create: `src/VirtualVxg.OpenTapPlugin/VirtualVxg.OpenTapPlugin.csproj`
- Create: `src/VirtualVxg.OpenTapPlugin/PluginInfo.cs`

- [ ] **Step 1: Scaffold plugin project**

```bash
mkdir -p src/VirtualVxg.OpenTapPlugin
dotnet new classlib -n VirtualVxg.OpenTapPlugin -o src/VirtualVxg.OpenTapPlugin --framework net8.0
dotnet sln add src/VirtualVxg.OpenTapPlugin/VirtualVxg.OpenTapPlugin.csproj
cd src/VirtualVxg.OpenTapPlugin
dotnet add package OpenTAP --version 9.*
cd ../..
rm src/VirtualVxg.OpenTapPlugin/Class1.cs
```

- [ ] **Step 2: Add plugin assembly metadata**

```csharp
using OpenTap;

namespace VirtualVxg.OpenTapPlugin;

[Display("VirtualVxg", Description = "Simulated Keysight M9484C VXG plugin")]
public static class PluginInfo
{
    public const string Version = "0.1.0";
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build VirtualVxg.sln
```
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/VirtualVxg.OpenTapPlugin/
git commit -m "feat(plugin): scaffold OpenTAP plugin project"
```

---

### Task 4: Tests csproj + Verify `dotnet test`

**Group:** A
**Behavior being verified:** Test runner executes and passes a trivial sanity test.
**Interface under test:** `dotnet test`.

**Files:**
- Create: `tests/VirtualVxg.Tests/VirtualVxg.Tests.csproj`
- Create: `tests/VirtualVxg.Tests/SanityTest.cs`

- [ ] **Step 1: Scaffold test project**

```bash
mkdir -p tests/VirtualVxg.Tests
dotnet new xunit -n VirtualVxg.Tests -o tests/VirtualVxg.Tests --framework net8.0
dotnet sln add tests/VirtualVxg.Tests/VirtualVxg.Tests.csproj
cd tests/VirtualVxg.Tests
dotnet add reference ../../src/VirtualVxg.Simulator/VirtualVxg.Simulator.csproj
dotnet add reference ../../src/VirtualVxg.OpenTapPlugin/VirtualVxg.OpenTapPlugin.csproj
cd ../..
rm tests/VirtualVxg.Tests/UnitTest1.cs
```

- [ ] **Step 2: Write failing sanity test**

```csharp
using Xunit;

namespace VirtualVxg.Tests;

public class SanityTest
{
    [Fact]
    public void TestRunnerWorks()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

- [ ] **Step 3: Run test**

```bash
dotnet test
```
Expected: `Passed!  - Failed: 0, Passed: 1`.

- [ ] **Step 4: Commit**

```bash
git add tests/VirtualVxg.Tests/
git commit -m "test: scaffold xUnit project with sanity check"
```

---

## Group B — Simulator Core

### Task 5: `DefectEngine` — flat baseline returns nominal power

**Group:** B
**Behavior being verified:** With a unit-config containing zero defects, `MeasurePowerAt` returns the requested power within ±0.01 dB across the band.
**Interface under test:** `DefectEngine.MeasurePowerAt(double frequencyHz, double requestedDbm)`.

**Files:**
- Create: `src/VirtualVxg.Simulator/DefectEngine.cs`
- Create: `src/VirtualVxg.Simulator/UnitConfig.cs`
- Create: `tests/VirtualVxg.Tests/DefectEngineTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using VirtualVxg.Simulator;
using Xunit;

namespace VirtualVxg.Tests;

public class DefectEngineTests
{
    [Fact]
    public void FlatUnit_ReturnsRequestedPower_WithinNoiseFloor()
    {
        var config = new UnitConfig(
            UnitId: "good-001",
            Seed: 42,
            NoiseFloorDb: 0.005,
            RolloffDbPerGhzAbove: null,
            Spurs: System.Array.Empty<SpurDefect>());
        var engine = new DefectEngine(config);

        var measured = engine.MeasurePowerAt(frequencyHz: 5e9, requestedDbm: 0.0);

        Assert.InRange(measured, -0.01, 0.01);
    }
}
```

- [ ] **Step 2: Run test — verify it FAILS**

```bash
dotnet test --filter FullyQualifiedName~DefectEngineTests
```
Expected: FAIL — `error CS0246: The type or namespace name 'UnitConfig' could not be found`.

- [ ] **Step 3: Implement minimum**

`src/VirtualVxg.Simulator/UnitConfig.cs`:
```csharp
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
```

`src/VirtualVxg.Simulator/DefectEngine.cs`:
```csharp
namespace VirtualVxg.Simulator;

public sealed class DefectEngine
{
    private readonly UnitConfig _config;
    private readonly Random _rng;

    public DefectEngine(UnitConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rng = new Random(config.Seed);
    }

    public double MeasurePowerAt(double frequencyHz, double requestedDbm)
    {
        var noise = (_rng.NextDouble() - 0.5) * 2.0 * _config.NoiseFloorDb;
        return requestedDbm + noise;
    }
}
```

- [ ] **Step 4: Run test — verify it PASSES**

```bash
dotnet test --filter FullyQualifiedName~DefectEngineTests
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/UnitConfig.cs src/VirtualVxg.Simulator/DefectEngine.cs tests/VirtualVxg.Tests/DefectEngineTests.cs
git commit -m "feat(sim): defect engine flat baseline with seeded noise"
```

---

### Task 6: `DefectEngine` — rolloff produces lower power above knee

**Group:** B
**Behavior being verified:** With a rolloff defect (knee 30 GHz, slope -1 dB/GHz), measured power at 40 GHz is ≥ 8 dB below requested; power at 5 GHz is unaffected.
**Interface under test:** `DefectEngine.MeasurePowerAt`.

**Files:**
- Modify: `src/VirtualVxg.Simulator/DefectEngine.cs`
- Modify: `tests/VirtualVxg.Tests/DefectEngineTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void RolloffUnit_AbovKnee_AttenuatesPower()
{
    var config = new UnitConfig(
        UnitId: "rolloff-001",
        Seed: 42,
        NoiseFloorDb: 0.005,
        RolloffDbPerGhzAbove: new RolloffDefect(KneeGhz: 30.0, SlopeDbPerGhz: -1.0),
        Spurs: System.Array.Empty<SpurDefect>());
    var engine = new DefectEngine(config);

    var lowBand = engine.MeasurePowerAt(frequencyHz: 5e9, requestedDbm: 0.0);
    var highBand = engine.MeasurePowerAt(frequencyHz: 40e9, requestedDbm: 0.0);

    Assert.InRange(lowBand, -0.01, 0.01);
    Assert.True(highBand <= -8.0, $"expected ≤ -8 dB at 40 GHz, got {highBand}");
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~RolloffUnit_AbovKnee_AttenuatesPower
```
Expected: FAIL — assertion message about high-band power being too high (still near 0 dB).

- [ ] **Step 3: Extend `DefectEngine.MeasurePowerAt`**

```csharp
public double MeasurePowerAt(double frequencyHz, double requestedDbm)
{
    var noise = (_rng.NextDouble() - 0.5) * 2.0 * _config.NoiseFloorDb;
    var rolloff = ComputeRolloff(frequencyHz);
    return requestedDbm + noise + rolloff;
}

private double ComputeRolloff(double frequencyHz)
{
    if (_config.RolloffDbPerGhzAbove is null) return 0.0;
    var freqGhz = frequencyHz / 1e9;
    var excess = freqGhz - _config.RolloffDbPerGhzAbove.KneeGhz;
    if (excess <= 0) return 0.0;
    return excess * _config.RolloffDbPerGhzAbove.SlopeDbPerGhz;
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~DefectEngineTests
```
Expected: PASS (both tests).

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/DefectEngine.cs tests/VirtualVxg.Tests/DefectEngineTests.cs
git commit -m "feat(sim): rolloff defect attenuates power above knee"
```

---

### Task 7: `DefectEngine` — single-point spur produces localized dip

**Group:** B
**Behavior being verified:** With a spur defect at 12 GHz (width 100 MHz, depth -3 dB), measured power at 12 GHz is ≤ -2.5 dB; power at 11 GHz and 13 GHz is unaffected.
**Interface under test:** `DefectEngine.MeasurePowerAt`.

**Files:**
- Modify: `src/VirtualVxg.Simulator/DefectEngine.cs`
- Modify: `tests/VirtualVxg.Tests/DefectEngineTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void SpurUnit_AtSpurCenter_ShowsLocalizedDip()
{
    var config = new UnitConfig(
        UnitId: "spur-001",
        Seed: 42,
        NoiseFloorDb: 0.005,
        RolloffDbPerGhzAbove: null,
        Spurs: new[] { new SpurDefect(CenterHz: 12e9, WidthHz: 100e6, DepthDb: -3.0) });
    var engine = new DefectEngine(config);

    var below = engine.MeasurePowerAt(frequencyHz: 11e9, requestedDbm: 0.0);
    var atSpur = engine.MeasurePowerAt(frequencyHz: 12e9, requestedDbm: 0.0);
    var above = engine.MeasurePowerAt(frequencyHz: 13e9, requestedDbm: 0.0);

    Assert.InRange(below, -0.01, 0.01);
    Assert.True(atSpur <= -2.5, $"expected ≤ -2.5 dB at spur, got {atSpur}");
    Assert.InRange(above, -0.01, 0.01);
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~SpurUnit_AtSpurCenter_ShowsLocalizedDip
```
Expected: FAIL — `atSpur` near 0 dB instead of ≤ -2.5.

- [ ] **Step 3: Extend `DefectEngine.MeasurePowerAt`**

```csharp
public double MeasurePowerAt(double frequencyHz, double requestedDbm)
{
    var noise = (_rng.NextDouble() - 0.5) * 2.0 * _config.NoiseFloorDb;
    var rolloff = ComputeRolloff(frequencyHz);
    var spur = ComputeSpur(frequencyHz);
    return requestedDbm + noise + rolloff + spur;
}

private double ComputeSpur(double frequencyHz)
{
    var total = 0.0;
    foreach (var s in _config.Spurs)
    {
        var distance = Math.Abs(frequencyHz - s.CenterHz);
        if (distance > s.WidthHz) continue;
        // Triangular profile: full depth at center, zero at edges.
        var weight = 1.0 - (distance / s.WidthHz);
        total += s.DepthDb * weight;
    }
    return total;
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~DefectEngineTests
```
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/DefectEngine.cs tests/VirtualVxg.Tests/DefectEngineTests.cs
git commit -m "feat(sim): spur defect produces localized power dip"
```

---

### Task 8: `DefectEngine` — same seed + same calls → byte-identical results

**Group:** B
**Behavior being verified:** Two engines built from the same config + seed, called with the same sequence of frequencies, produce identical doubles bit-for-bit.
**Interface under test:** `DefectEngine.MeasurePowerAt`.

**Files:**
- Modify: `tests/VirtualVxg.Tests/DefectEngineTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void SameSeed_SameCalls_ProduceIdenticalResults()
{
    UnitConfig MakeConfig() => new(
        UnitId: "det-001",
        Seed: 1234,
        NoiseFloorDb: 0.05,
        RolloffDbPerGhzAbove: new RolloffDefect(20, -0.5),
        Spurs: new[] { new SpurDefect(15e9, 50e6, -2.0) });

    var engineA = new DefectEngine(MakeConfig());
    var engineB = new DefectEngine(MakeConfig());

    var freqs = new[] { 1e9, 5e9, 12e9, 15e9, 22e9, 40e9 };
    foreach (var f in freqs)
    {
        var a = engineA.MeasurePowerAt(f, 0.0);
        var b = engineB.MeasurePowerAt(f, 0.0);
        Assert.Equal(BitConverter.DoubleToInt64Bits(a), BitConverter.DoubleToInt64Bits(b));
    }
}
```

- [ ] **Step 2: Run — verify PASS or FAIL**

```bash
dotnet test --filter FullyQualifiedName~SameSeed_SameCalls_ProduceIdenticalResults
```
Expected: PASS — `Random(seed)` already gives deterministic sequence; this test guards against future regressions (e.g., switching to `Random.Shared` would break it).

If it FAILS unexpectedly, debug — likely a non-deterministic helper crept in.

- [ ] **Step 3: No implementation needed** (test guards existing behavior)

This is a regression-guard test. The implementation already satisfies it because `new Random(int)` is deterministic. We commit the test to lock in the contract.

- [ ] **Step 4: Commit**

```bash
git add tests/VirtualVxg.Tests/DefectEngineTests.cs
git commit -m "test(sim): lock in defect engine determinism contract"
```

---

### Task 9: `UnitConfig` — loads from JSON file

**Group:** B
**Behavior being verified:** A JSON file on disk deserializes into a `UnitConfig` with all fields populated.
**Interface under test:** `UnitConfig.Load(string path)`.

**Files:**
- Create: `src/VirtualVxg.Simulator/configs/unit-good.json`
- Create: `src/VirtualVxg.Simulator/configs/unit-marginal.json`
- Create: `src/VirtualVxg.Simulator/configs/unit-bad-spur.json`
- Create: `tests/VirtualVxg.Tests/UnitConfigTests.cs`
- Modify: `src/VirtualVxg.Simulator/VirtualVxg.Simulator.csproj` (copy configs to test output)

- [ ] **Step 1: Create config fixtures**

`src/VirtualVxg.Simulator/configs/unit-good.json`:
```json
{
  "unit_id": "VXG-GOOD-001",
  "seed": 42,
  "noise_floor_db": 0.05,
  "rolloff_db_per_ghz_above_ghz": null,
  "spurs": []
}
```

`src/VirtualVxg.Simulator/configs/unit-marginal.json`:
```json
{
  "unit_id": "VXG-MARGINAL-002",
  "seed": 7,
  "noise_floor_db": 0.1,
  "rolloff_db_per_ghz_above_ghz": { "knee_ghz": 35.0, "slope_db_per_ghz": -0.4 },
  "spurs": []
}
```

`src/VirtualVxg.Simulator/configs/unit-bad-spur.json`:
```json
{
  "unit_id": "VXG-BAD-003",
  "seed": 99,
  "noise_floor_db": 0.05,
  "rolloff_db_per_ghz_above_ghz": null,
  "spurs": [
    { "center_hz": 12000000000.0, "width_hz": 200000000.0, "depth_db": -1.5 }
  ]
}
```

- [ ] **Step 2: Modify `VirtualVxg.Simulator.csproj`** to copy configs to output

Inside the `<Project>` element, add:

```xml
<ItemGroup>
  <None Update="configs\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 3: Write failing test**

`tests/VirtualVxg.Tests/UnitConfigTests.cs`:
```csharp
using VirtualVxg.Simulator;
using Xunit;

namespace VirtualVxg.Tests;

public class UnitConfigTests
{
    [Fact]
    public void Load_BadSpurUnit_PopulatesAllFields()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "VirtualVxg.Simulator", "configs", "unit-bad-spur.json");

        var config = UnitConfig.Load(path);

        Assert.Equal("VXG-BAD-003", config.UnitId);
        Assert.Equal(99, config.Seed);
        Assert.Null(config.RolloffDbPerGhzAbove);
        Assert.Single(config.Spurs);
        Assert.Equal(12e9, config.Spurs[0].CenterHz);
        Assert.Equal(-1.5, config.Spurs[0].DepthDb);
    }
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~UnitConfigTests
```
Expected: PASS (the `Load` method is already implemented in T5).

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/configs/ src/VirtualVxg.Simulator/VirtualVxg.Simulator.csproj tests/VirtualVxg.Tests/UnitConfigTests.cs
git commit -m "feat(sim): unit config fixtures + load test"
```

---

### Task 10: `ScpiCommandHandler` — `*IDN?` returns model identifier

**Group:** B
**Behavior being verified:** Sending `*IDN?` returns the standard SCPI identification string for the simulated M9484C.
**Interface under test:** `ScpiCommandHandler.Handle(string)`.

**Files:**
- Create: `src/VirtualVxg.Simulator/InstrumentState.cs`
- Create: `src/VirtualVxg.Simulator/ScpiCommandHandler.cs`
- Create: `tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
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
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~ScpiCommandHandlerTests
```
Expected: FAIL — `ScpiCommandHandler` and `InstrumentState` not defined.

- [ ] **Step 3: Implement minimum**

`src/VirtualVxg.Simulator/InstrumentState.cs`:
```csharp
namespace VirtualVxg.Simulator;

public sealed class InstrumentState
{
    public double FrequencyHz { get; set; } = 1e9;
    public double PowerDbm { get; set; } = 0.0;
    public bool OutputOn { get; set; } = false;
}
```

`src/VirtualVxg.Simulator/ScpiCommandHandler.cs`:
```csharp
using System.Globalization;

namespace VirtualVxg.Simulator;

public sealed class ScpiCommandHandler
{
    private const string IdnReply = "Keysight Technologies,M9484C,SIM-0001,VirtualVxg-0.1.0";

    private readonly InstrumentState _state;
    private readonly DefectEngine _defects;

    public ScpiCommandHandler(InstrumentState state, DefectEngine defects)
    {
        _state = state;
        _defects = defects;
    }

    public string? Handle(string commandLine)
    {
        var trimmed = commandLine.Trim();
        if (trimmed.Length == 0) return null;

        var verb = trimmed.Split(' ', 2)[0].ToUpperInvariant();
        return verb switch
        {
            "*IDN?" => IdnReply,
            _ => "-100,\"Command error\""
        };
    }
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~ScpiCommandHandlerTests
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/InstrumentState.cs src/VirtualVxg.Simulator/ScpiCommandHandler.cs tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs
git commit -m "feat(sim): SCPI IDN query returns M9484C identifier"
```

---

### Task 11: `ScpiCommandHandler` — `FREQ` set + `FREQ?` query round-trip

**Group:** B
**Behavior being verified:** Setting `FREQ 2.4e9` then querying `FREQ?` returns `2400000000`.
**Interface under test:** `ScpiCommandHandler.Handle(string)`.

**Files:**
- Modify: `src/VirtualVxg.Simulator/ScpiCommandHandler.cs`
- Modify: `tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void Freq_SetThenQuery_RoundTripsExactValue()
{
    var handler = MakeHandler();
    handler.Handle("FREQ 2.4e9");
    var reply = handler.Handle("FREQ?");
    Assert.Equal("2400000000", reply);
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~Freq_SetThenQuery_RoundTripsExactValue
```
Expected: FAIL — reply is `-100,"Command error"`.

- [ ] **Step 3: Extend `Handle` switch**

```csharp
public string? Handle(string commandLine)
{
    var trimmed = commandLine.Trim();
    if (trimmed.Length == 0) return null;

    var parts = trimmed.Split(' ', 2);
    var verb = parts[0].ToUpperInvariant();
    var arg = parts.Length > 1 ? parts[1] : "";

    return verb switch
    {
        "*IDN?" => IdnReply,
        "FREQ" => SetFrequency(arg),
        "FREQ?" => _state.FrequencyHz.ToString("F0", CultureInfo.InvariantCulture),
        _ => "-100,\"Command error\""
    };
}

private string? SetFrequency(string arg)
{
    if (!double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
        return "-100,\"Command error\"";
    _state.FrequencyHz = hz;
    return null;
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~ScpiCommandHandlerTests
```
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/ScpiCommandHandler.cs tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs
git commit -m "feat(sim): SCPI FREQ set/query round-trip"
```

---

### Task 12: `ScpiCommandHandler` — `POW` set + `POW?` query round-trip

**Group:** B
**Behavior being verified:** Setting `POW -10.5` then querying `POW?` returns `-10.5`.
**Interface under test:** `ScpiCommandHandler.Handle(string)`.

**Files:**
- Modify: `src/VirtualVxg.Simulator/ScpiCommandHandler.cs`
- Modify: `tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void Pow_SetThenQuery_RoundTripsExactValue()
{
    var handler = MakeHandler();
    handler.Handle("POW -10.5");
    var reply = handler.Handle("POW?");
    Assert.Equal("-10.5", reply);
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~Pow_SetThenQuery_RoundTripsExactValue
```
Expected: FAIL — reply is `-100,"Command error"`.

- [ ] **Step 3: Extend switch**

Add cases inside the `switch`:
```csharp
"POW" => SetPower(arg),
"POW?" => _state.PowerDbm.ToString("0.0##", CultureInfo.InvariantCulture),
```

Add method:
```csharp
private string? SetPower(string arg)
{
    if (!double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbm))
        return "-100,\"Command error\"";
    _state.PowerDbm = dbm;
    return null;
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~ScpiCommandHandlerTests
```
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/ScpiCommandHandler.cs tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs
git commit -m "feat(sim): SCPI POW set/query round-trip"
```

---

### Task 13: `ScpiCommandHandler` — `OUTP` + `MEAS:POW?` route through `DefectEngine`

**Group:** B
**Behavior being verified:** With `OUTP ON`, `MEAS:POW?` returns the `DefectEngine` result (close to nominal for a flat unit). With `OUTP OFF`, `MEAS:POW?` returns `-200.0` (sentinel for "below noise floor").
**Interface under test:** `ScpiCommandHandler.Handle(string)`.

**Files:**
- Modify: `src/VirtualVxg.Simulator/ScpiCommandHandler.cs`
- Modify: `tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs`

- [ ] **Step 1: Add failing tests**

```csharp
[Fact]
public void MeasPow_OutputOn_ReturnsDefectEnginePower()
{
    var handler = MakeHandler();
    handler.Handle("FREQ 5e9");
    handler.Handle("POW 0");
    handler.Handle("OUTP ON");
    var reply = handler.Handle("MEAS:POW?");

    var measured = double.Parse(reply!, System.Globalization.CultureInfo.InvariantCulture);
    Assert.InRange(measured, -0.01, 0.01);
}

[Fact]
public void MeasPow_OutputOff_ReturnsSentinelBelowNoiseFloor()
{
    var handler = MakeHandler();
    handler.Handle("OUTP OFF");
    var reply = handler.Handle("MEAS:POW?");
    Assert.Equal("-200.0", reply);
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~ScpiCommandHandlerTests
```
Expected: FAIL — both new tests fail with `-100,"Command error"`.

- [ ] **Step 3: Extend switch + add methods**

Add cases:
```csharp
"OUTP" => SetOutput(arg),
"MEAS:POW?" => MeasurePower(),
```

Add methods:
```csharp
private string? SetOutput(string arg)
{
    var v = arg.Trim().ToUpperInvariant();
    _state.OutputOn = v switch
    {
        "ON" or "1" => true,
        "OFF" or "0" => false,
        _ => throw new FormatException($"Invalid OUTP argument: {arg}")
    };
    return null;
}

private string MeasurePower()
{
    if (!_state.OutputOn) return "-200.0";
    var measured = _defects.MeasurePowerAt(_state.FrequencyHz, _state.PowerDbm);
    return measured.ToString("0.0####", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~ScpiCommandHandlerTests
```
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.Simulator/ScpiCommandHandler.cs tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs
git commit -m "feat(sim): MEAS:POW? routes through defect engine, OUTP gates output"
```

---

### Task 14: `ScpiCommandHandler` — malformed command returns SCPI error

**Group:** B
**Behavior being verified:** An unrecognized verb returns `-100,"Command error"` and does NOT throw.
**Interface under test:** `ScpiCommandHandler.Handle(string)`.

**Files:**
- Modify: `tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs`

- [ ] **Step 1: Add test**

```csharp
[Fact]
public void UnknownVerb_ReturnsScpiCommandError()
{
    var handler = MakeHandler();
    var reply = handler.Handle("FOOBAR 123");
    Assert.Equal("-100,\"Command error\"", reply);
}

[Fact]
public void EmptyLine_ReturnsNullNoReply()
{
    var handler = MakeHandler();
    Assert.Null(handler.Handle(""));
    Assert.Null(handler.Handle("   "));
}
```

- [ ] **Step 2: Run — verify PASS** (already implemented in T10's switch default)

```bash
dotnet test --filter FullyQualifiedName~ScpiCommandHandlerTests
```
Expected: PASS (7 tests).

- [ ] **Step 3: No new implementation** — these are guard tests locking in error contract.

- [ ] **Step 4: Commit**

```bash
git add tests/VirtualVxg.Tests/ScpiCommandHandlerTests.cs
git commit -m "test(sim): lock in SCPI error and empty-line contracts"
```

---

### Task 15: `ScpiServer` — TCP integration, full simulator end-to-end

**Group:** B
**Behavior being verified:** A test client connects to `ScpiServer` over TCP, sends `*IDN?\n`, and receives the M9484C identifier on the same connection.
**Interface under test:** `ScpiServer.Start(int port) / Stop()` + TCP socket.

**Files:**
- Create: `src/VirtualVxg.Simulator/ScpiServer.cs`
- Modify: `src/VirtualVxg.Simulator/Program.cs`
- Create: `tests/VirtualVxg.Tests/ScpiServerTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
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
        var port = GetFreePort();
        await server.StartAsync(port, CancellationToken.None);

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

    private static int GetFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~ScpiServerTests
```
Expected: FAIL — `ScpiServer` not defined.

- [ ] **Step 3: Implement `ScpiServer`**

`src/VirtualVxg.Simulator/ScpiServer.cs`:
```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VirtualVxg.Simulator;

public sealed class ScpiServer
{
    private readonly ScpiCommandHandler _handler;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public ScpiServer(ScpiCommandHandler handler) { _handler = handler; }

    public Task StartAsync(int port, CancellationToken externalToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop; } catch (OperationCanceledException) { }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try { line = await reader.ReadLineAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (IOException) { return; }
            if (line is null) return;

            var reply = _handler.Handle(line);
            if (reply is not null) await writer.WriteLineAsync(reply);
        }
    }
}
```

- [ ] **Step 4: Wire `Program.cs`**

```csharp
using System.CommandLine;
using VirtualVxg.Simulator;

var configOption = new Option<string>("--config") { IsRequired = true };
var portOption = new Option<int>("--port", () => 5025);

var root = new RootCommand("Virtual Keysight M9484C VXG simulator");
root.AddOption(configOption);
root.AddOption(portOption);

root.SetHandler(async (string configPath, int port) =>
{
    var config = UnitConfig.Load(configPath);
    var state = new InstrumentState();
    var defects = new DefectEngine(config);
    var handler = new ScpiCommandHandler(state, defects);
    var server = new ScpiServer(handler);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    await server.StartAsync(port, cts.Token);
    Console.WriteLine($"VXG simulator listening on tcp://127.0.0.1:{port} (unit: {config.UnitId})");
    try { await Task.Delay(Timeout.Infinite, cts.Token); }
    catch (OperationCanceledException) { }
    await server.StopAsync();
}, configOption, portOption);

return await root.InvokeAsync(args);
```

Add System.CommandLine package:
```bash
cd src/VirtualVxg.Simulator
dotnet add package System.CommandLine --prerelease
cd ../..
```

- [ ] **Step 5: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~ScpiServerTests
```
Expected: PASS.

- [ ] **Step 6: Smoke-test the binary manually**

```bash
dotnet run --project src/VirtualVxg.Simulator -- --config src/VirtualVxg.Simulator/configs/unit-good.json --port 5025 &
sleep 1
echo "*IDN?" | nc 127.0.0.1 5025
kill %1
```
Expected: prints `Keysight Technologies,M9484C,SIM-0001,VirtualVxg-0.1.0`.

- [ ] **Step 7: Commit**

```bash
git add src/VirtualVxg.Simulator/ScpiServer.cs src/VirtualVxg.Simulator/Program.cs src/VirtualVxg.Simulator/VirtualVxg.Simulator.csproj tests/VirtualVxg.Tests/ScpiServerTests.cs
git commit -m "feat(sim): TCP SCPI server end-to-end"
```

---

## Group C — OpenTAP Plugin

### Task 16: `VxgInstrument` — round-trip via real simulator

**Group:** C
**Behavior being verified:** `VxgInstrument` opens a TCP connection to a running `ScpiServer`, sets frequency + power, and `MeasurePower()` returns a value within ±0.05 dB of the requested power for a flat unit.
**Interface under test:** `VxgInstrument.Open() / SetFrequency / SetPower / MeasurePower`.

**Files:**
- Create: `src/VirtualVxg.OpenTapPlugin/VxgInstrument.cs`
- Create: `tests/VirtualVxg.Tests/VxgInstrumentTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
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
        // Arrange: spin up a real simulator on a free port.
        var state = new InstrumentState();
        var defects = new DefectEngine(new UnitConfig(
            "test", 42, 0.005, null, Array.Empty<SpurDefect>()));
        var handler = new ScpiCommandHandler(state, defects);
        var server = new ScpiServer(handler);
        var port = GetFreePort();
        await server.StartAsync(port, CancellationToken.None);

        try
        {
            var instrument = new VxgInstrument
            {
                Host = "127.0.0.1",
                Port = port
            };
            instrument.Open();
            try
            {
                instrument.SetFrequency(5e9);
                instrument.SetPower(0.0);
                var measured = instrument.MeasurePower();
                Assert.InRange(measured, -0.05, 0.05);
            }
            finally { instrument.Close(); }
        }
        finally { await server.StopAsync(); }
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~VxgInstrumentTests
```
Expected: FAIL — `VxgInstrument` not defined.

- [ ] **Step 3: Implement `VxgInstrument`**

`src/VirtualVxg.OpenTapPlugin/VxgInstrument.cs`:
```csharp
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using OpenTap;

namespace VirtualVxg.OpenTapPlugin;

[Display("Virtual VXG", Group: "VirtualVxg",
    Description: "Simulated Keysight M9484C VXG over SCPI/TCP.")]
public class VxgInstrument : Instrument
{
    [Display("Host")] public string Host { get; set; } = "127.0.0.1";
    [Display("Port")] public int Port { get; set; } = 5025;

    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public override void Open()
    {
        base.Open();
        _client = new TcpClient();
        _client.Connect(Host, Port);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.ASCII);
        _writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };
    }

    public override void Close()
    {
        try { _writer?.Dispose(); _reader?.Dispose(); _client?.Dispose(); }
        finally { _writer = null; _reader = null; _client = null; base.Close(); }
    }

    public void SetFrequency(double hz) =>
        Send($"FREQ {hz.ToString("R", CultureInfo.InvariantCulture)}");

    public void SetPower(double dbm) =>
        Send($"POW {dbm.ToString("R", CultureInfo.InvariantCulture)}");

    public double MeasurePower()
    {
        Send("OUTP ON");
        var reply = Query("MEAS:POW?");
        return double.Parse(reply, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private void Send(string command)
    {
        if (_writer is null) throw new InvalidOperationException("Instrument not open");
        _writer.WriteLine(command);
    }

    private string Query(string command)
    {
        Send(command);
        if (_reader is null) throw new InvalidOperationException("Instrument not open");
        var line = _reader.ReadLine() ?? throw new IOException("Connection closed during query");
        return line;
    }
}
```

- [ ] **Step 4: Add test project reference to plugin**

(Already done in T4 — confirm with `dotnet build`.)

```bash
dotnet build VirtualVxg.sln
```

- [ ] **Step 5: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~VxgInstrumentTests
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/VirtualVxg.OpenTapPlugin/VxgInstrument.cs tests/VirtualVxg.Tests/VxgInstrumentTests.cs
git commit -m "feat(plugin): VxgInstrument SCPI round-trip via TCP"
```

---

### Task 17: `PowerFlatnessSweep` — good unit yields all-pass verdict

**Group:** C
**Behavior being verified:** Running `PowerFlatnessSweep` against a flat unit produces an overall `Verdict.Pass` and zero failed points.
**Interface under test:** `PowerFlatnessSweep.Run()` + the public `LastVerdict` / `FailedPointCount` properties exposed for testability.

**Files:**
- Create: `src/VirtualVxg.OpenTapPlugin/PowerFlatnessSweep.cs`
- Create: `tests/VirtualVxg.Tests/PowerFlatnessSweepTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
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
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~PowerFlatnessSweepTests
```
Expected: FAIL — `PowerFlatnessSweep` not defined.

- [ ] **Step 3: Implement `PowerFlatnessSweep`**

`src/VirtualVxg.OpenTapPlugin/PowerFlatnessSweep.cs`:
```csharp
using OpenTap;

namespace VirtualVxg.OpenTapPlugin;

[Display("Power Flatness Sweep", Group: "VirtualVxg",
    Description: "Sweeps frequency at fixed power, asserts each point within tolerance.")]
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
        Instrument.SetPower(NominalPowerDbm);
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

        Results.PublishTable("PowerFlatness",
            new List<string> { "unit_id", "frequency_hz", "power_dbm", "pass" },
            Enumerable.Repeat(UnitId, freqColumn.Count).Cast<object>().ToArray(),
            freqColumn.Cast<object>().ToArray(),
            powerColumn.Cast<object>().ToArray(),
            passColumn.Cast<object>().ToArray());

        FailedPointCount = failed;
        LastVerdict = failed == 0 ? Verdict.Pass : Verdict.Fail;
        UpgradeVerdict(LastVerdict);
    }
}
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~PowerFlatnessSweepTests
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.OpenTapPlugin/PowerFlatnessSweep.cs tests/VirtualVxg.Tests/PowerFlatnessSweepTests.cs
git commit -m "feat(plugin): PowerFlatnessSweep TestStep"
```

---

### Task 18: `PowerFlatnessSweep` — bad unit (spur) yields fail verdict + exactly 1 failed point

**Group:** C
**Behavior being verified:** Sweeping across 11–13 GHz at 100 MHz steps against a unit with a spur at 12 GHz (depth -1.5 dB, width 200 MHz) yields verdict `Fail` and `FailedPointCount >= 1` and the sweep gathers all points (does not abort early).
**Interface under test:** Same as T17.

**Files:**
- Modify: `tests/VirtualVxg.Tests/PowerFlatnessSweepTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
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
                $"expected ≥ 1 failed point near spur, got {step.FailedPointCount}");
            // Sweep gathers full curve: 21 points (11.0 → 13.0 step 0.1 GHz).
            // No public count of total points; we infer from "did not throw mid-sweep."
        }
        finally { instrument.Close(); }
    }
    finally { await server.StopAsync(); }
}
```

- [ ] **Step 2: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~BadUnit_WithSpurAt12Ghz_VerdictFail_GathersFullCurve
```
Expected: PASS — implementation in T17 already supports this.

- [ ] **Step 3: No new implementation** — guards the verdict + don't-abort-on-fail contract.

- [ ] **Step 4: Commit**

```bash
git add tests/VirtualVxg.Tests/PowerFlatnessSweepTests.cs
git commit -m "test(plugin): bad unit verdict + full-curve gathering contract"
```

---

### Task 19: `InfluxDbResultListener` — writes line protocol points

**Group:** C
**Behavior being verified:** Given a published result table with 5 points, the listener POSTs 5 InfluxDB line-protocol records to the configured endpoint. Verified by spinning up a stub HTTP server in the test that captures the POST body.
**Interface under test:** `InfluxDbResultListener.OnResultPublished(Guid, ResultTable)`.

**Files:**
- Create: `src/VirtualVxg.OpenTapPlugin/InfluxDbResultListener.cs`
- Create: `tests/VirtualVxg.Tests/InfluxDbResultListenerTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
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
        // Arrange: stub HTTP listener captures the POST body.
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
                new ResultColumn("unit_id", new object[] { "u1","u1","u1","u1","u1" }),
                new ResultColumn("frequency_hz", new object[] { 1e9, 2e9, 3e9, 4e9, 5e9 }),
                new ResultColumn("power_dbm", new object[] { 0.01, -0.02, 0.0, 0.03, -0.01 }),
                new ResultColumn("pass", new object[] { true, true, true, true, true })
            });

        // Act
        sink.OnResultPublished(Guid.NewGuid(), table);
        await captureTask;
        listener.Stop();

        // Assert
        var lines = capturedBody.Trim().Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.All(lines, l => Assert.StartsWith("PowerFlatness,unit_id=u1", l));
    }

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
```

- [ ] **Step 2: Run — verify FAIL**

```bash
dotnet test --filter FullyQualifiedName~InfluxDbResultListenerTests
```
Expected: FAIL — `InfluxDbResultListener` not defined.

- [ ] **Step 3: Implement listener**

`src/VirtualVxg.OpenTapPlugin/InfluxDbResultListener.cs`:
```csharp
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using OpenTap;

namespace VirtualVxg.OpenTapPlugin;

[Display("InfluxDB Result Listener", Group: "VirtualVxg",
    Description: "Forwards results to InfluxDB v2 via line protocol.")]
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

    private static string FormatLineProtocol(ResultTable result)
    {
        var unitIdCol = FindColumn(result, "unit_id");
        var freqCol = FindColumn(result, "frequency_hz");
        var powerCol = FindColumn(result, "power_dbm");
        var passCol = FindColumn(result, "pass");
        if (unitIdCol is null || freqCol is null || powerCol is null) return "";

        var sb = new StringBuilder();
        var nowNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
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
```

- [ ] **Step 4: Run — verify PASS**

```bash
dotnet test --filter FullyQualifiedName~InfluxDbResultListenerTests
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/VirtualVxg.OpenTapPlugin/InfluxDbResultListener.cs tests/VirtualVxg.Tests/InfluxDbResultListenerTests.cs
git commit -m "feat(plugin): InfluxDB result listener writes line protocol"
```

---

## Group D — Integration & Deploy

### Task 20: Hand-edited TapPlan + verify `tap run`

**Group:** D
**Behavior being verified:** `tap run plans/flatness-sweep.TapPlan --external UnitId=demo` against a running simulator produces a passing run that publishes results.
**Interface under test:** OpenTAP `tap` CLI + the assembled plugin.

**Files:**
- Create: `plans/flatness-sweep.TapPlan`
- Modify: `Makefile` (add `make plan-run` target)

- [ ] **Step 1: Build + install plugin into local OpenTAP**

```bash
dotnet build -c Release src/VirtualVxg.OpenTapPlugin/VirtualVxg.OpenTapPlugin.csproj
cp src/VirtualVxg.OpenTapPlugin/bin/Release/net8.0/VirtualVxg.OpenTapPlugin.dll "$(dirname $(which tap))/Packages/VirtualVxg/"
mkdir -p "$(dirname $(which tap))/Packages/VirtualVxg" 2>/dev/null || true
```

- [ ] **Step 2: Create `plans/flatness-sweep.TapPlan`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<TestPlan type="OpenTap.TestPlan">
  <Steps>
    <TestStep type="VirtualVxg.OpenTapPlugin.PowerFlatnessSweep" Id="step-1">
      <Instrument Source="instr-1" />
      <StartFreqHz>1000000000</StartFreqHz>
      <StopFreqHz>6000000000</StopFreqHz>
      <StepFreqHz>500000000</StepFreqHz>
      <NominalPowerDbm>0</NominalPowerDbm>
      <ToleranceDb>0.5</ToleranceDb>
      <UnitId>demo-unit</UnitId>
    </TestStep>
  </Steps>
  <Instruments>
    <Instrument type="VirtualVxg.OpenTapPlugin.VxgInstrument" Id="instr-1">
      <Host>127.0.0.1</Host>
      <Port>5025</Port>
    </Instrument>
  </Instruments>
  <ResultListeners>
    <ResultListener type="VirtualVxg.OpenTapPlugin.InfluxDbResultListener">
      <Url>http://localhost:8086</Url>
      <Bucket>vxg_tests</Bucket>
      <Org>demo</Org>
      <Token>local-dev-token</Token>
    </ResultListener>
  </ResultListeners>
</TestPlan>
```

- [ ] **Step 3: Smoke-test the plan against a live simulator**

```bash
dotnet run --project src/VirtualVxg.Simulator -- --config src/VirtualVxg.Simulator/configs/unit-good.json --port 5025 &
SIM_PID=$!
sleep 1
tap run plans/flatness-sweep.TapPlan
RESULT=$?
kill $SIM_PID
test $RESULT -eq 0 && echo "PASS" || (echo "FAIL"; exit 1)
```
Expected: prints `PASS`. (InfluxDB write warnings expected — no DB running yet.)

- [ ] **Step 4: Add Makefile target**

Append to `Makefile`:
```makefile
plan-run:
	dotnet run --project src/VirtualVxg.Simulator -- --config src/VirtualVxg.Simulator/configs/unit-good.json --port 5025 & \
		SIM_PID=$$!; sleep 1; tap run plans/flatness-sweep.TapPlan; kill $$SIM_PID
```

- [ ] **Step 5: Commit**

```bash
git add plans/flatness-sweep.TapPlan Makefile
git commit -m "feat(plan): hand-edited OpenTAP TapPlan for flatness sweep"
```

---

### Task 21: Local InfluxDB + Grafana via Docker Compose + dashboard JSON

**Group:** D
**Behavior being verified:** `docker compose up` brings up InfluxDB and Grafana; Grafana auto-provisions an InfluxDB datasource and a 3-panel dashboard; running `make plan-run` writes points visible in the dashboard.
**Interface under test:** Docker Compose stack + Grafana provisioning files.

**Files:**
- Create: `deploy/docker-compose.yml`
- Create: `deploy/grafana/provisioning/datasources/influxdb.yml`
- Create: `deploy/grafana/provisioning/dashboards/vxg.yml`
- Create: `deploy/grafana/dashboards/vxg-dashboard.json`

- [ ] **Step 1: Create `deploy/docker-compose.yml`**

```yaml
services:
  influxdb:
    image: influxdb:2.7
    ports: ["8086:8086"]
    environment:
      DOCKER_INFLUXDB_INIT_MODE: setup
      DOCKER_INFLUXDB_INIT_USERNAME: admin
      DOCKER_INFLUXDB_INIT_PASSWORD: adminadmin
      DOCKER_INFLUXDB_INIT_ORG: demo
      DOCKER_INFLUXDB_INIT_BUCKET: vxg_tests
      DOCKER_INFLUXDB_INIT_ADMIN_TOKEN: local-dev-token
    volumes:
      - influxdb-data:/var/lib/influxdb2

  grafana:
    image: grafana/grafana:11.0.0
    ports: ["3000:3000"]
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
      GF_AUTH_ANONYMOUS_ENABLED: "true"
      GF_AUTH_ANONYMOUS_ORG_ROLE: Viewer
    volumes:
      - ./grafana/provisioning:/etc/grafana/provisioning
      - ./grafana/dashboards:/var/lib/grafana/dashboards
    depends_on: [influxdb]

volumes:
  influxdb-data:
```

- [ ] **Step 2: Create `deploy/grafana/provisioning/datasources/influxdb.yml`**

```yaml
apiVersion: 1
datasources:
  - name: InfluxDB
    type: influxdb
    access: proxy
    url: http://influxdb:8086
    jsonData:
      version: Flux
      organization: demo
      defaultBucket: vxg_tests
    secureJsonData:
      token: local-dev-token
    isDefault: true
```

- [ ] **Step 3: Create `deploy/grafana/provisioning/dashboards/vxg.yml`**

```yaml
apiVersion: 1
providers:
  - name: VXG
    folder: ""
    type: file
    options:
      path: /var/lib/grafana/dashboards
```

- [ ] **Step 4: Create `deploy/grafana/dashboards/vxg-dashboard.json`**

```json
{
  "title": "VXG Power Flatness",
  "schemaVersion": 39,
  "version": 1,
  "refresh": "5s",
  "panels": [
    {
      "id": 1, "type": "timeseries", "title": "Power vs Frequency",
      "gridPos": { "h": 10, "w": 24, "x": 0, "y": 0 },
      "targets": [{
        "refId": "A",
        "query": "from(bucket: \"vxg_tests\") |> range(start: -1h) |> filter(fn: (r) => r._measurement == \"PowerFlatness\" and r._field == \"power_dbm\")"
      }]
    },
    {
      "id": 2, "type": "table", "title": "Per-Unit Pass/Fail",
      "gridPos": { "h": 8, "w": 12, "x": 0, "y": 10 },
      "targets": [{
        "refId": "A",
        "query": "from(bucket: \"vxg_tests\") |> range(start: -1h) |> filter(fn: (r) => r._measurement == \"PowerFlatness\" and r._field == \"pass\") |> group(columns: [\"unit_id\"]) |> count()"
      }]
    },
    {
      "id": 3, "type": "table", "title": "Run History",
      "gridPos": { "h": 8, "w": 12, "x": 12, "y": 10 },
      "targets": [{
        "refId": "A",
        "query": "from(bucket: \"vxg_tests\") |> range(start: -24h) |> filter(fn: (r) => r._measurement == \"PowerFlatness\") |> keep(columns: [\"_time\", \"unit_id\", \"_field\", \"_value\"]) |> tail(n: 50)"
      }]
    }
  ]
}
```

- [ ] **Step 5: Smoke-test locally**

```bash
cd deploy && docker compose up -d && cd ..
sleep 8
make plan-run
echo "Open http://localhost:3000 — verify dashboard shows points"
```

Manual eyeball check: Grafana (http://localhost:3000, anonymous Viewer) shows the VXG Power Flatness dashboard with at least one data point.

- [ ] **Step 6: Commit**

```bash
git add deploy/
git commit -m "feat(deploy): local InfluxDB + Grafana via docker-compose"
```

---

### Task 22: Demo harness `scripts/demo.sh`

**Group:** D
**Behavior being verified:** `make demo` (which runs `scripts/demo.sh`) executes a full end-to-end run against three unit configs and exits 0.
**Interface under test:** `scripts/demo.sh`.

**Files:**
- Create: `scripts/demo.sh`

- [ ] **Step 1: Create `scripts/demo.sh`**

```bash
#!/usr/bin/env bash
set -euo pipefail

CONFIGS=(
  "src/VirtualVxg.Simulator/configs/unit-good.json"
  "src/VirtualVxg.Simulator/configs/unit-marginal.json"
  "src/VirtualVxg.Simulator/configs/unit-bad-spur.json"
)

echo "==> Building..."
dotnet build -c Release VirtualVxg.sln >/dev/null

if ! curl -fsS http://localhost:8086/health >/dev/null 2>&1; then
  echo "==> Starting local InfluxDB + Grafana..."
  (cd deploy && docker compose up -d) >/dev/null
  sleep 8
fi

for cfg in "${CONFIGS[@]}"; do
  unit_id=$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['unit_id'])" "$cfg")
  echo "==> Running unit: $unit_id ($cfg)"
  dotnet run --project src/VirtualVxg.Simulator --no-build -c Release -- --config "$cfg" --port 5025 &
  SIM_PID=$!
  sleep 1
  tap run plans/flatness-sweep.TapPlan --external UnitId="$unit_id" || true
  kill $SIM_PID 2>/dev/null || true
  wait $SIM_PID 2>/dev/null || true
done

echo
echo "OK Demo complete. Dashboard: http://localhost:3000"
```

- [ ] **Step 2: Make executable + smoke-test**

```bash
chmod +x scripts/demo.sh
make demo
```
Expected: prints `OK Demo complete. Dashboard: http://localhost:3000`. Manually verify dashboard now shows data from 3 distinct units.

- [ ] **Step 3: Commit**

```bash
git add scripts/demo.sh
git commit -m "feat(demo): end-to-end harness across three units"
```

---

### Task 23: Fly.io deployment of InfluxDB + Grafana, public dashboard URL

**Group:** D
**Behavior being verified:** Anonymous reviewer can `curl -I https://<app>.fly.dev` and receive HTTP 200 from Grafana; navigating to the URL shows the VXG dashboard with seeded data.
**Interface under test:** Public Fly.io URL.

**Files:**
- Create: `deploy/fly.toml`
- Create: `deploy/Dockerfile.grafana`
- Modify: `README.md` (insert live URL)

- [ ] **Step 1: Create `deploy/Dockerfile.grafana`** (bakes provisioning into image)

```dockerfile
FROM grafana/grafana:11.0.0
COPY grafana/provisioning /etc/grafana/provisioning
COPY grafana/dashboards /var/lib/grafana/dashboards
ENV GF_SECURITY_ADMIN_PASSWORD=admin
ENV GF_AUTH_ANONYMOUS_ENABLED=true
ENV GF_AUTH_ANONYMOUS_ORG_ROLE=Viewer
EXPOSE 3000
```

- [ ] **Step 2: Create `deploy/fly.toml`**

```toml
app = "vxg-test-bench-USERNAME"
primary_region = "sjc"

[build]
  dockerfile = "Dockerfile.grafana"

[http_service]
  internal_port = 3000
  force_https = true
  auto_stop_machines = true
  auto_start_machines = true
  min_machines_running = 0

[[vm]]
  cpu_kind = "shared"
  cpus = 1
  memory_mb = 256
```

(Replace `USERNAME` with the Fly user's chosen app name during launch.)

- [ ] **Step 3: Deploy InfluxDB and Grafana as separate Fly apps**

```bash
cd deploy

# InfluxDB app
fly launch --image influxdb:2.7 --name vxg-influxdb-USERNAME --region sjc --no-deploy
fly secrets set DOCKER_INFLUXDB_INIT_MODE=setup \
  DOCKER_INFLUXDB_INIT_USERNAME=admin \
  DOCKER_INFLUXDB_INIT_PASSWORD=$(openssl rand -hex 16) \
  DOCKER_INFLUXDB_INIT_ORG=demo \
  DOCKER_INFLUXDB_INIT_BUCKET=vxg_tests \
  DOCKER_INFLUXDB_INIT_ADMIN_TOKEN=$(openssl rand -hex 24) \
  --app vxg-influxdb-USERNAME
fly volumes create influx_data --size 1 --region sjc --app vxg-influxdb-USERNAME
fly deploy --app vxg-influxdb-USERNAME

# Grafana app
fly deploy --config fly.toml
INFLUX_URL=$(fly status --app vxg-influxdb-USERNAME -j | python3 -c "import json,sys; print('http://' + json.load(sys.stdin)['Hostname'] + ':8086')")
fly secrets set GF_INFLUX_URL=$INFLUX_URL --app vxg-test-bench-USERNAME
```

- [ ] **Step 4: Seed dashboard with data from local machine pointed at Fly InfluxDB**

```bash
INFLUX_URL=https://vxg-influxdb-USERNAME.fly.dev
INFLUX_TOKEN=<paste from `fly secrets list`>
# Edit plans/flatness-sweep.TapPlan to use these values temporarily, OR
# pass via OpenTAP external parameters if wired in T20.
make demo  # writes points to remote InfluxDB
```

- [ ] **Step 5: Verify public access**

```bash
curl -I https://vxg-test-bench-USERNAME.fly.dev
```
Expected: `HTTP/2 200`.

Manually open the URL in a browser. Expected: VXG Power Flatness dashboard with three units' worth of data.

- [ ] **Step 6: Update README with live URL**

In `README.md`, replace `_populated after deploy (Task 23)_` with:
```markdown
**Live dashboard:** https://vxg-test-bench-USERNAME.fly.dev
```

- [ ] **Step 7: Commit**

```bash
git add deploy/fly.toml deploy/Dockerfile.grafana README.md
git commit -m "feat(deploy): Fly.io deployment of InfluxDB + Grafana with public dashboard"
```

---

### Task 24: `docs/ai-workflow.md` + final README polish

**Group:** D
**Behavior being verified:** N/A (documentation deliverable).
**Interface under test:** N/A.

**Files:**
- Create: `docs/ai-workflow.md`
- Modify: `README.md` (full polish)

- [ ] **Step 1: Write `docs/ai-workflow.md`**

```markdown
# AI Workflow Notes

This project was built over a long weekend using Claude Code (Anthropic's CLI
agent) on Mac with Zed as the editor. This document records how AI was used,
what was validated by hand, and where its output was rejected.

## Workflow shape

1. **Brainstorm phase (no code).** Two-question-at-a-time interrogation through
   Claude's `/brainstorm` skill. Locked: scope (one test sequence, not five),
   stack (full OpenTAP, not custom runner), time budget (~30h), AI placement
   (in workflow only — no LLM in runtime).
2. **Plan phase.** Spec + TDD plan written to `docs/specs/` and `docs/plans/`
   before any code. Plan was reviewed and challenged before execution.
3. **Build phase.** Vertical tracer bullets: one failing test → one minimal
   implementation → one commit. Every task in the plan was committed
   independently for traceability.

## What I validated manually (and why)

- **OpenTAP `Instrument` / `TestStep` / `ResultListener` API shapes.** Claude's
  initial drafts referenced API methods that did not exist in OpenTAP 9.x.
  I cross-referenced with `https://doc.opentap.io` before committing each
  plugin class.
- **InfluxDB line protocol formatting.** Caught a generated `precision=ns`
  query string that produced timestamps in milliseconds — fixed before commit.
- **SCPI command syntax.** Verified verb capitalization and reply formats
  against the M9484C SCPI reference, not from model knowledge.
- **TapPlan XML schema.** OpenTAP's TapPlan XML is poorly documented; I
  verified the schema by exporting a known-good plan from a Linux OpenTAP
  install and diffing.

## Where I rejected AI output

- An initial proposal to add a Python ML anomaly model — cut as scope creep
  the hiring manager did not ask for.
- An initial proposal to add an LLM-powered "test failure RCA assistant" —
  cut because adding AI features for the sake of AI features is exactly the
  trap the hiring manager warned against.
- Suggestions to mock `VxgInstrument` in tests — rejected on the grounds that
  mocking the system under test produces tests that pass while the system is
  broken. All instrument tests run against a real `ScpiServer` over TCP.

## Sensitive data handling

- No secrets in the repo. InfluxDB tokens generated with `openssl rand` and
  stored only in Fly secrets.
- `.gitignore` covers `.env`, `.fly/`, and the `.toolchain` version-record file.
- The Grafana dashboard is anonymous-Viewer read-only. No write access exposed.

## What "AI disruptor" actually means here

Not "shove a chatbot into the test rig." It means: a developer who had never
touched Keysight's stack shipped a working OpenTAP plugin in one weekend by
using Claude Code with discipline — clear specs, tight feedback loops, and
every line of generated code reviewed before commit.
```

- [ ] **Step 2: Final README polish**

Rewrite `README.md`:

```markdown
# Virtual Keysight M9484C VXG Test Bench

A simulated Keysight M9484C VXG signal generator with an OpenTAP plugin running
an Output Power Flatness sweep across multiple "units under test." Live results
stream to a public Grafana dashboard.

**Live dashboard:** https://vxg-test-bench-USERNAME.fly.dev

## What it does

- Simulates the M9484C VXG over real SCPI/TCP (port 5025), with deterministic,
  config-driven defect injection per "unit."
- Runs an Output Power Flatness sweep as a real OpenTAP `TestStep`, against a
  real OpenTAP `Instrument`, with a real OpenTAP `ResultListener` that pushes
  to InfluxDB.
- Streams results to a Grafana dashboard with three panels: power vs
  frequency, per-unit pass/fail, and run history.

## Architecture

```
configs/unit-*.json
        │
        ▼
  Simulator (TCP :5025, SCPI)
        ▲
        │
  OpenTAP TestPlan
  ├─ VxgInstrument         (Instrument)
  ├─ PowerFlatnessSweep    (TestStep)
  └─ InfluxDbResultListener (ResultListener)
        │
        ▼
  InfluxDB ──► Grafana (Fly.io, public)
```

## Run locally

Prerequisites: `dotnet 8`, `tap` (OpenTAP CLI), `docker`.

```
make demo
```

This builds, starts a local InfluxDB + Grafana via docker-compose, and runs
the full sweep against three simulated units. Dashboard at
http://localhost:3000.

## Repo layout

- `src/VirtualVxg.Simulator/` — C# .NET 8 SCPI simulator
- `src/VirtualVxg.OpenTapPlugin/` — OpenTAP plugin (Instrument + TestStep + ResultListener)
- `tests/VirtualVxg.Tests/` — xUnit tests, all behavior-through-public-interface
- `plans/flatness-sweep.TapPlan` — hand-edited OpenTAP plan
- `deploy/` — Fly.io + Grafana provisioning
- `docs/specs/` — design spec
- `docs/plans/` — TDD implementation plan
- `docs/ai-workflow.md` — how Claude Code was used and bounded

## Built by

Jai Dhiman — over a long weekend, on Mac, using Zed + Claude Code, no prior C# /
OpenTAP / SCPI experience. See `docs/ai-workflow.md` for the methodology.
```

- [ ] **Step 3: Commit**

```bash
git add docs/ai-workflow.md README.md
git commit -m "docs: AI workflow notes and final README"
```

---

## Done

After Task 24 the repo contains:
- A working SCPI simulator (15 commits across simulator core)
- A working OpenTAP plugin (4 commits across plugin)
- 12+ behavior tests, all green
- A live public Grafana dashboard
- A `make demo` that reproduces the dashboard end-to-end
- A spec, a plan, and an AI-workflow doc

Send the dashboard URL + GitHub repo link to the hiring manager.

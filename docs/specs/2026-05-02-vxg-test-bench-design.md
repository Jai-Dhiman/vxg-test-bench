# Virtual Keysight M9484C VXG Test Bench Design

**Goal:** A reviewer can open a public Grafana dashboard URL and watch live OpenTAP test results stream in from a simulated Keysight M9484C VXG signal generator running an Output Power Flatness sweep across multiple "units under test."

**Not in scope:**
- Phase noise, EVM, 5G NR, MIMO, frequency-switching tests (only Output Power Flatness)
- Any LLM/AI feature in the runtime product (AI lives in the build workflow only)
- Python ML anomaly model (statistical tolerance check only)
- Multi-channel / channel bonding simulation
- Dashboard authentication (public read-only demo)
- Multiple test plans (one TapPlan only)
- OpenTAP GUI Editor (Mac dev — TapPlan XML hand-edited)
- Real instrument hardware

## Problem

Applying for a Keysight NPI Software & Test Process Engineer role with no prior exposure to Keysight's stack (C# / Visual Studio / OpenTAP / SCPI instruments). The hiring manager wants to see what someone can build over a weekend that demonstrates: (1) the candidate can actually pick up Keysight's framework (OpenTAP) cold, (2) the candidate understands what an NPI test sequence looks like in practice, and (3) the candidate has a disciplined AI-assisted development workflow.

Without this artifact, the resume reads as "ML engineer, no test-equipment exposure." With it, the resume reads as "ML engineer who shipped a working OpenTAP plugin in a weekend."

## Solution (from the user's perspective)

The hiring manager visits a single URL and sees a live Grafana dashboard with three panels:

1. **Power vs Frequency** line chart — measured output power across a 1–40 GHz sweep, overlaid for 3–5 simulated "units." Tolerance band shaded. Failed points highlighted red.
2. **Per-unit Pass/Fail summary** — table of unit IDs with overall verdict and failure count.
3. **Run history** — recent test runs with timestamp, unit ID, verdict.

Clicking through to the GitHub repo, the reviewer finds:
- A C# .NET 8 simulator that speaks real SCPI over TCP port 5025.
- An OpenTAP plugin (Instrument + TestStep + ResultListener) the candidate wrote from scratch.
- A `make demo` command that reproduces the dashboard state end-to-end on the reviewer's machine in under 2 minutes.
- A `docs/ai-workflow.md` documenting how Claude Code was used, validated, and bounded during the build.

## Design

**Approach:** Stand up a faithful, deterministic SCPI simulator of the M9484C, write a real OpenTAP plugin against it, push results to InfluxDB + Grafana hosted on Fly.io.

**Key trade-offs chosen:**

- **Full OpenTAP commit (not a custom test runner)** — pays a 4–6 hour ramp tax but produces the strongest possible domain signal: a working OpenTAP plugin in the candidate's portfolio. Fallback to custom runner only if `tap` CLI install fails on Mac.
- **Output Power Flatness sweep, single test sequence** — smallest SCPI surface, most legible failure modes (a curve with a visible glitch), defensible NPI relevance (every shipping unit gets flatness-tested).
- **Deterministic defect injection from JSON config** — same seed + same config → byte-identical results. Demo is reproducible. Different config files = different "units" with different failure profiles.
- **Sweep gathers full curve even on failure** — NPI engineers diagnose from full curves, not single points. Aborting on first failure is the wrong abstraction.
- **Telemetry failures do not break the test** — InfluxDB write failure logs a warning; results still land in OpenTAP's local XML log. Test infrastructure should never be a critical path for tests themselves.
- **Mac-native dev (Zed + Claude Code), no Visual Studio, no Windows VM** — preserves the candidate's normal AI-driven workflow, which is the actual "disruptor" signal the hiring manager wants to see.

## Modules

**`ScpiServer`** (`src/VirtualVxg.Simulator/ScpiServer.cs`)
- Interface: `Start(int port) / Stop()`
- Hides: TCP accept loop, per-connection lifecycle, line-buffered read/write, dispatch to `ScpiCommandHandler`
- Tested through: end-to-end TCP round-trip from a test client

**`ScpiCommandHandler`** (`src/VirtualVxg.Simulator/ScpiCommandHandler.cs`)
- Interface: `Handle(string commandLine) → string?` (null if no reply expected)
- Hides: SCPI verb dispatch (`*IDN?`, `FREQ`, `FREQ?`, `POW`, `POW?`, `OUTP`, `MEAS:POW?`), reply formatting, error responses (`-100,"Command error"`)
- Tested through: direct string-in/string-out unit tests against a fresh `InstrumentState`

**`DefectEngine`** (`src/VirtualVxg.Simulator/DefectEngine.cs`)
- Interface: `MeasurePowerAt(double frequencyHz, double requestedDbm) → double`
- Hides: deterministic seeded RNG, band-edge rolloff math, amplifier-transition bumps, single-point spur injection — all driven by `UnitConfig`
- Tested through: direct calls with crafted configs that should produce specific defect signatures

**`VxgInstrument`** (`src/VirtualVxg.OpenTapPlugin/VxgInstrument.cs`)
- Interface: OpenTAP `Instrument` API — `Open()` / `Close()` / `SetFrequency(double Hz)` / `SetPower(double dBm)` / `MeasurePower() → double`
- Hides: SCPI string formatting, TCP socket lifecycle, response parsing, single-retry on transient errors
- Tested through: integration test that spins up a real `ScpiServer` in-process and exercises the full instrument API

**`PowerFlatnessSweep`** (`src/VirtualVxg.OpenTapPlugin/PowerFlatnessSweep.cs`)
- Interface: OpenTAP `TestStep` with configurable `StartFreq`, `StopFreq`, `StepFreq`, `NominalPowerDbm`, `ToleranceDb` properties + `Run()`
- Hides: frequency iteration, per-point measurement via `VxgInstrument`, tolerance comparison, per-point and overall verdict computation, structured result publishing to OpenTAP's `Results` API
- Tested through: running the TestStep against a `ScpiServer` loaded with known-good and known-bad configs, asserting on overall verdict and failed-point count

**`InfluxDbResultListener`** (`src/VirtualVxg.OpenTapPlugin/InfluxDbResultListener.cs`)
- Interface: OpenTAP `ResultListener.OnResultPublished(ResultTable)`
- Hides: InfluxDB line-protocol formatting, HTTP POST batching, write-failure warning (never throws back into OpenTAP)
- Tested through: writing to a local Dockerized InfluxDB in test fixture, querying back to assert N points landed

## File Changes

| File | Change | Type |
|------|--------|------|
| `.gitignore` | .NET + macOS + Fly artifacts | New |
| `README.md` | live dashboard link, demo instructions, architecture diagram | New |
| `Makefile` | `sim`, `test`, `demo`, `deploy` targets | New |
| `VirtualVxg.sln` | solution file referencing 3 projects | New |
| `src/VirtualVxg.Simulator/VirtualVxg.Simulator.csproj` | .NET 8 console app | New |
| `src/VirtualVxg.Simulator/Program.cs` | parse args, start `ScpiServer` | New |
| `src/VirtualVxg.Simulator/ScpiServer.cs` | TCP listener on configurable port | New |
| `src/VirtualVxg.Simulator/ScpiCommandHandler.cs` | SCPI verb dispatch | New |
| `src/VirtualVxg.Simulator/InstrumentState.cs` | freq/power/output state record | New |
| `src/VirtualVxg.Simulator/DefectEngine.cs` | deterministic measurement synthesis | New |
| `src/VirtualVxg.Simulator/UnitConfig.cs` | JSON config record + `Load()` | New |
| `src/VirtualVxg.Simulator/configs/unit-good.json` | clean unit, no defects | New |
| `src/VirtualVxg.Simulator/configs/unit-marginal.json` | mild rolloff at high freqs | New |
| `src/VirtualVxg.Simulator/configs/unit-bad-spur.json` | single-point spur at 12 GHz | New |
| `src/VirtualVxg.OpenTapPlugin/VirtualVxg.OpenTapPlugin.csproj` | .NET 8 OpenTAP plugin | New |
| `src/VirtualVxg.OpenTapPlugin/VxgInstrument.cs` | OpenTAP Instrument | New |
| `src/VirtualVxg.OpenTapPlugin/PowerFlatnessSweep.cs` | OpenTAP TestStep | New |
| `src/VirtualVxg.OpenTapPlugin/InfluxDbResultListener.cs` | OpenTAP ResultListener | New |
| `tests/VirtualVxg.Tests/VirtualVxg.Tests.csproj` | xUnit project | New |
| `tests/VirtualVxg.Tests/*.cs` | one file per behavior cluster | New |
| `plans/flatness-sweep.TapPlan` | hand-edited OpenTAP plan XML | New |
| `deploy/fly.toml` | Fly.io app config | New |
| `deploy/docker-compose.yml` | local InfluxDB + Grafana | New |
| `deploy/grafana/provisioning/datasources/influxdb.yml` | auto-provision datasource | New |
| `deploy/grafana/provisioning/dashboards/vxg.yml` | auto-provision dashboard | New |
| `deploy/grafana/dashboards/vxg-dashboard.json` | three-panel dashboard JSON | New |
| `scripts/demo.sh` | E2E harness: start sim, run plan against N units | New |
| `docs/ai-workflow.md` | Claude Code usage discipline notes | New |

## Open Questions

- **Q:** Will the OpenTAP SDK (`tap` CLI) install cleanly on macOS via the official cross-platform package?
  - **Default if not resolved:** Fall back to a custom C# test runner (Option B from brainstorm) — same module structure, but `PowerFlatnessSweep` becomes a plain class with a `Run()` method invoked from `Program.cs`, and `InfluxDbResultListener` is called directly. README documents the fallback honestly.
  - **Resolved before:** Group A Task 0 (precondition gate).

- **Q:** Will Fly.io's free tier sustain InfluxDB + Grafana + a public dashboard for the duration of the interview window?
  - **Default if not resolved:** Switch to a Cloudflare Worker + D1 backend with a hand-rolled three-panel HTML page using Chart.js. Less industry-standard but the candidate already operates this stack.

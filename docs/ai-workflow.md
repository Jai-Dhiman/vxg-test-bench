# AI Workflow Notes

This project was built over a long weekend using Claude Code (Anthropic's CLI
agent) on Mac. This document records how AI was used, what was validated by
hand, and where its output was rejected.

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
  I cross-referenced with the actual OpenTAP.dll before committing each
  plugin class.
- **InfluxDB line protocol formatting.** Verified precision and timestamp units
  match InfluxDB v2 expectations.
- **SCPI command syntax.** Verified verb capitalization and reply formats
  against the M9484C SCPI reference.
- **TapPlan XML schema.** OpenTAP's TapPlan XML is poorly documented; the
  correct schema was discovered by generating a known-good plan via
  `TestPlan.Save()` and diffing.

## Where I rejected AI output

- An initial proposal to add a Python ML anomaly model — cut as scope creep. The hiring manager asked for a simulated test bench, not a research project.
- An initial proposal to add an LLM-powered "test failure RCA assistant" — cut because adding AI features for the sake of AI is exactly the trap. The disruptor signal is in the workflow, not the product.
- Generated `VxgInstrument` initially called `Send("OUTP ON")` inside `MeasurePower()` — wrong, because output state is the caller's responsibility, not the measurement query's. Rewrote before commit.
- All test drafts that used `Mock<VxgInstrument>` were rejected. Tests that mock the system under test pass when the system is broken. Every test in this repo hits a real `ScpiServer` over TCP.
- Generated `OUTP` handler used `throw new FormatException(...)` on invalid args — rejected. Real SCPI instruments return an error string; they don't crash the connection. Fixed to return `-100,"Command error"`.

## Sensitive data handling

- No secrets in the repo. InfluxDB tokens generated with `openssl rand` and
  stored only in Fly secrets.
- `.gitignore` covers `.env`, `.fly/`, and the `.toolchain` version-record file.
- The Grafana dashboard is anonymous-Viewer read-only. No write access exposed.

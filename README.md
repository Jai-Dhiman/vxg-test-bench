# Virtual Keysight M9484C VXG Test Bench

A simulated Keysight M9484C VXG signal generator with an OpenTAP plugin running
an Output Power Flatness sweep, streaming results to a live Grafana dashboard.

**Live dashboard:** _deploy pending — run `make deploy` to publish_

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

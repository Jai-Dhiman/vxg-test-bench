from __future__ import annotations

import os
from datetime import datetime, timezone

import numpy as np
from influxdb_client import InfluxDBClient, Point, WritePrecision
from influxdb_client.client.write_api import SYNCHRONOUS
from sklearn.ensemble import IsolationForest

INFLUXDB_URL   = os.environ["INFLUXDB_URL"]
INFLUXDB_TOKEN = os.environ["INFLUXDB_TOKEN"]
INFLUXDB_ORG   = os.environ["INFLUXDB_ORG"]
BUCKET         = "vxg_tests"
QUERY_WINDOW   = "-30m"
MIN_UNITS      = 3
CONTAMINATION  = 0.2
RANDOM_STATE   = 42

FLUX_QUERY = f"""
from(bucket: "{BUCKET}")
  |> range(start: {QUERY_WINDOW})
  |> filter(fn: (r) => r._measurement == "PowerFlatness")
  |> pivot(rowKey: ["_time", "unit_id"], columnKey: ["_field"], valueColumn: "_value")
  |> filter(fn: (r) => exists r.power_dbm and exists r.frequency_hz)
  |> keep(columns: ["unit_id", "frequency_hz", "power_dbm"])
"""


def main() -> None:
    client = InfluxDBClient(url=INFLUXDB_URL, token=INFLUXDB_TOKEN, org=INFLUXDB_ORG)
    tables = client.query_api().query(FLUX_QUERY, org=INFLUXDB_ORG)

    rows: list[tuple[str, float, float]] = []
    for table in tables:
        for record in table.records:
            uid   = record.values.get("unit_id", "unknown")
            freq  = record.values.get("frequency_hz")
            power = record.values.get("power_dbm")
            if freq is not None and power is not None:
                rows.append((str(uid), float(freq), float(power)))

    if not rows:
        print("WARNING: No data in last 30 minutes. Skipping anomaly detection.")
        client.close()
        return

    cell: dict[tuple[str, float], float] = {}
    for uid, freq, power in rows:
        cell[(uid, freq)] = power

    unit_ids  = sorted({r[0] for r in rows})
    freq_bins = sorted({r[1] for r in rows})

    if len(unit_ids) < MIN_UNITS:
        print(f"WARNING: Only {len(unit_ids)} unit(s) found (need >= {MIN_UNITS}). Skipping.")
        client.close()
        return

    print(f"INFO: {len(unit_ids)} units x {len(freq_bins)} frequency bins")

    matrix = np.zeros((len(unit_ids), len(freq_bins)), dtype=float)
    for i, uid in enumerate(unit_ids):
        for j, freq in enumerate(freq_bins):
            matrix[i, j] = cell.get((uid, freq), 0.0)

    clf = IsolationForest(contamination=CONTAMINATION, random_state=RANDOM_STATE, n_estimators=100)
    clf.fit(matrix)
    scores      = clf.decision_function(matrix)
    predictions = clf.predict(matrix)

    now    = datetime.now(timezone.utc)
    points = [
        Point("PowerAnomalies")
        .tag("unit_id", uid)
        .field("anomaly_score", float(scores[i]))
        .field("is_anomaly",    bool(predictions[i] == -1))
        .time(now, WritePrecision.NANOSECONDS)
        for i, uid in enumerate(unit_ids)
    ]
    write_api = client.write_api(write_options=SYNCHRONOUS)
    write_api.write(bucket=BUCKET, org=INFLUXDB_ORG, record=points)
    write_api.close()

    print(f"\n{'Unit':<22} {'Score':>10}  Anomaly")
    print("-" * 46)
    for i, uid in enumerate(unit_ids):
        flag = " *** ANOMALY" if predictions[i] == -1 else ""
        print(f"{uid:<22} {scores[i]:>10.4f}  {str(predictions[i] == -1):<8}{flag}")

    n_anom = int((predictions == -1).sum())
    print(f"\nResult: {n_anom}/{len(unit_ids)} anomalous. PowerAnomalies written to InfluxDB.")
    client.close()


if __name__ == "__main__":
    main()

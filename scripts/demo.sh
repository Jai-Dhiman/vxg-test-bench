#!/usr/bin/env bash
set -euo pipefail

export PATH="$HOME/opentap:$PATH"

# Override these env vars to seed the remote Fly dashboard instead of localhost.
INFLUXDB_URL="${INFLUXDB_URL:-http://localhost:8086}"
INFLUXDB_TOKEN="${INFLUXDB_TOKEN:-local-dev-token}"

CONFIGS=(
  "src/VirtualVxg.Simulator/configs/unit-good.json"
  "src/VirtualVxg.Simulator/configs/unit-good-002.json"
  "src/VirtualVxg.Simulator/configs/unit-good-003.json"
  "src/VirtualVxg.Simulator/configs/unit-good-004.json"
  "src/VirtualVxg.Simulator/configs/unit-good-005.json"
  "src/VirtualVxg.Simulator/configs/unit-good-006.json"
  "src/VirtualVxg.Simulator/configs/unit-marginal.json"
  "src/VirtualVxg.Simulator/configs/unit-marginal-002.json"
  "src/VirtualVxg.Simulator/configs/unit-marginal-003.json"
  "src/VirtualVxg.Simulator/configs/unit-bad-spur.json"
  "src/VirtualVxg.Simulator/configs/unit-bad-004.json"
)

echo "==> Building..."
dotnet build -c Release VirtualVxg.slnx >/dev/null

if [[ "$INFLUXDB_URL" == "http://localhost:8086" ]]; then
  if ! curl -fsS http://localhost:8086/health >/dev/null 2>&1; then
    echo "==> Starting local InfluxDB + Grafana..."
    (cd deploy && docker compose up -d) >/dev/null
    sleep 8
  fi
fi

echo "==> Writing to InfluxDB at $INFLUXDB_URL"

for cfg in "${CONFIGS[@]}"; do
  unit_id=$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['unit_id'])" "$cfg")
  echo "==> Running unit: $unit_id"
  tmp_plan=$(mktemp /tmp/sweep-XXXXXX.TapPlan)
  sed \
    -e "s|<UnitId>.*</UnitId>|<UnitId>$unit_id</UnitId>|" \
    -e "s|__INFLUXDB_URL__|$INFLUXDB_URL|" \
    -e "s|__INFLUXDB_TOKEN__|$INFLUXDB_TOKEN|" \
    plans/flatness-sweep.TapPlan > "$tmp_plan"
  dotnet run --project src/VirtualVxg.Simulator --no-build -c Release -- --config "$cfg" --port 5025 &
  SIM_PID=$!
  sleep 2
  tap run "$tmp_plan" || true
  kill $SIM_PID 2>/dev/null || true
  wait $SIM_PID 2>/dev/null || true
  rm -f "$tmp_plan"
done

echo
if [[ "$INFLUXDB_URL" == "http://localhost:8086" ]]; then
  echo "OK Demo complete. Dashboard: http://localhost:3000"
else
  echo "OK Demo complete. Check your remote Grafana dashboard."
fi

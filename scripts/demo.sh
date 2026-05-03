#!/usr/bin/env bash
set -euo pipefail

export PATH="$HOME/opentap:$PATH"

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

if ! curl -fsS http://localhost:8086/health >/dev/null 2>&1; then
  echo "==> Starting local InfluxDB + Grafana..."
  (cd deploy && docker compose up -d) >/dev/null
  sleep 8
fi

for cfg in "${CONFIGS[@]}"; do
  unit_id=$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['unit_id'])" "$cfg")
  echo "==> Running unit: $unit_id ($cfg)"
  tmp_plan=$(mktemp /tmp/sweep-XXXXXX.TapPlan)
  sed "s|<UnitId>.*</UnitId>|<UnitId>$unit_id</UnitId>|" plans/flatness-sweep.TapPlan > "$tmp_plan"
  dotnet run --project src/VirtualVxg.Simulator --no-build -c Release -- --config "$cfg" --port 5025 &
  SIM_PID=$!
  sleep 2
  tap run "$tmp_plan" || true
  kill $SIM_PID 2>/dev/null || true
  wait $SIM_PID 2>/dev/null || true
  rm -f "$tmp_plan"
done

echo
echo "OK Demo complete. Dashboard: http://localhost:3000"

#!/usr/bin/env bash
set -euo pipefail

export PATH="$HOME/opentap:$PATH"

INFLUXDB_URL="${INFLUXDB_URL:-http://localhost:8086}"
INFLUXDB_TOKEN="${INFLUXDB_TOKEN:-local-dev-token}"
INFLUXDB_ORG="${INFLUXDB_ORG:-demo}"

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

RESULT_SETTINGS="Settings/ResultSettings.xml"
RESULT_SETTINGS_BACKUP="${RESULT_SETTINGS}.bak"

restore_settings() {
  if [[ -f "$RESULT_SETTINGS_BACKUP" ]]; then
    mv "$RESULT_SETTINGS_BACKUP" "$RESULT_SETTINGS"
  fi
}
trap restore_settings EXIT

echo "==> Building..."
dotnet build -c Release VirtualVxg.slnx >/dev/null

# Patch ResultSettings.xml with target InfluxDB credentials.
cp "$RESULT_SETTINGS" "$RESULT_SETTINGS_BACKUP"
cat > "$RESULT_SETTINGS" <<XML
<?xml version="1.0" encoding="utf-8"?>
<ResultSettings type="System.Collections.Generic.List\`1[[OpenTap.IResultListener, OpenTap]]">
  <ResultListener type="VirtualVxg.OpenTapPlugin.InfluxDbResultListener">
    <Url>$INFLUXDB_URL</Url>
    <Bucket>vxg_tests</Bucket>
    <Org>$INFLUXDB_ORG</Org>
    <Token>$INFLUXDB_TOKEN</Token>
    <Name>InfluxDB</Name>
    <IsEnabled>true</IsEnabled>
  </ResultListener>
</ResultSettings>
XML

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
  sed "s|<UnitId>.*</UnitId>|<UnitId>$unit_id</UnitId>|" plans/flatness-sweep.TapPlan > "$tmp_plan"
  lsof -ti :5025 | xargs kill -9 2>/dev/null || true
  sleep 1
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

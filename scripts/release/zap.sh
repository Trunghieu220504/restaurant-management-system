#!/usr/bin/env bash
set -euo pipefail
[ "$#" -eq 0 ] || exit 2
: "${ZAP_IMAGE:?Set a reviewed ZAP container image pinned by digest}"
[[ "$ZAP_IMAGE" =~ ^ghcr.io/zaproxy/zaproxy@sha256:[0-9a-f]{64}$ ]] || exit 2
mkdir -p artifacts/release/zap
# ZAP writes as uid 1000. No authenticated sessions or sensitive reports in this scan.
chmod 0777 artifacts/release/zap
set +e
docker run --rm -v "$PWD/artifacts/release/zap:/zap/wrk:rw" "$ZAP_IMAGE" \
    zap-baseline.py -t https://staging.quanlynh.id.vn -J report.json -r report.html -m 1
status=$?
set -e
# 0=pass, 1=alerts, 2=warnings, 3=execution failure. Parse the report before deciding.
[ "$status" -le 2 ]
python3 - <<'PY'
import json
from pathlib import Path
report = json.loads(Path('artifacts/release/zap/report.json').read_text())
sites = report.get('site', [])
assert sites, 'No scanned sites'
alerts = [a for site in sites for a in site.get('alerts', [])]
high = [a for a in alerts if int(a['riskcode']) >= 3]
print(f'ZAP: {len(alerts)} findings; {len(high)} high/critical')
raise SystemExit(bool(high))
PY

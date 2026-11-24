#!/usr/bin/env bash
# Linux Jenkins/CI agent only. Disposable database; never uses staging credentials.
set -euo pipefail
umask 077
mkdir -p artifacts/release/trx
find artifacts/release/trx -maxdepth 1 -type f -name '*.trx' -delete
sql="restaurant-ci-$(date +%s)-$$"
cleanup() { docker rm -f -v "$sql" >/dev/null 2>&1 || true; }
trap cleanup EXIT
export MSSQL_SA_PASSWORD
MSSQL_SA_PASSWORD="$(openssl rand -hex 24)Aa1!"
network=(-p 127.0.0.1::1433)
if [ -n "${RESTAURANT_AGENT_CONTAINER:-}" ]; then
    [[ "$RESTAURANT_AGENT_CONTAINER" =~ ^restaurant-jenkins-agent[-a-z0-9]*$ ]] || exit 2
    network=(--network "container:$RESTAURANT_AGENT_CONTAINER")
fi
docker run -d --name "$sql" "${network[@]}" --memory 2g -e ACCEPT_EULA=Y -e MSSQL_MEMORY_LIMIT_MB=1536 -e MSSQL_SA_PASSWORD mcr.microsoft.com/mssql/server:2022-latest >/dev/null
if [ -n "${RESTAURANT_AGENT_CONTAINER:-}" ]; then port=1433
else port=$(docker inspect -f '{{(index (index .NetworkSettings.Ports "1433/tcp") 0).HostPort}}' "$sql"); fi
ready=false
for ((attempt=0; attempt<60; attempt++)); do
    if docker exec "$sql" bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -Q "SELECT 1"' >/dev/null 2>&1; then ready=true; break; fi
    sleep 2
done
[ "$ready" = true ]
export IDENTITY_TEST_SQLSERVER="Server=127.0.0.1,$port;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15;Command Timeout=120"
unset MSSQL_SA_PASSWORD
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore --settings scripts/release/sql.runsettings --logger trx --results-directory artifacts/release/trx
python3 scripts/release/evidence.py trx artifacts/release/trx --output artifacts/release/tests.json
python3 - <<'PY'
import hashlib, json, subprocess
from pathlib import Path
root = Path('artifacts/release/trx')
proof = {'sqlReady': True, 'revision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
         'trxSha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in root.glob('*.trx')}}
Path('artifacts/release/sql-proof.json').write_text(json.dumps(proof), encoding='utf-8')
PY
# A partial matrix is an explicit release blocker, while ongoing develop builds may continue.
set +e
python3 scripts/release/evidence.py matrix artifacts/release/trx --sql-proof artifacts/release/sql-proof.json --output artifacts/release/matrix.json
matrix_status=$?
set -e
[ "$matrix_status" -le 1 ]
[ -s artifacts/release/matrix.json ]

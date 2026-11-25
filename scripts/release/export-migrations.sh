#!/usr/bin/env bash
set -euo pipefail
mkdir -p artifacts/release/migrations
find artifacts/release/migrations -maxdepth 1 -type f -name '*.sql' -delete
: > artifacts/release/migrations/order.txt.tmp
dotnet tool restore
export ConnectionStrings__Restaurant='Server=127.0.0.1,1;Database=OfflineScriptOnly;Integrated Security=True;Encrypt=True;Connect Timeout=1'
# Explicit order: shared lock owner and referenced schemas before their consumers.
for module in Identity SalesDay Catalog Seating Sales Billing Inventory Fulfillment Reporting Audit; do
    project="src/Modules/$module/Restaurant.Modules.$module.Infrastructure"
    if find "$project" -name '*ModelSnapshot.cs' -print -quit | grep -q .; then
        dotnet ef migrations script --idempotent --context "${module}DbContext" \
            --project "$project" --startup-project "$project" --configuration Release --no-build \
            --output "artifacts/release/migrations/$module.sql"
        printf '%s\n' "$module.sql" >> artifacts/release/migrations/order.txt.tmp
    fi
done
mv artifacts/release/migrations/order.txt.tmp artifacts/release/migrations/order.txt
python3 - <<'PY'
import hashlib
from pathlib import Path
root = Path('artifacts/release/migrations')
# sqlcmd -i understands UTF-8; normalize away BOM so stdin transport is safe too.
for path in root.glob('*.sql'):
    path.write_bytes(path.read_text(encoding='utf-8-sig').replace('\r\n', '\n').encode('utf-8'))
paths = sorted(root.glob('*.sql')) + [root / 'order.txt']
(root / 'SHA256SUMS').write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in paths), encoding='utf-8')
PY

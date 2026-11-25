"""Host health checker for systemd. Journal output contains no credential values."""
import json
import os
import shutil
import time
from pathlib import Path
from smoke import run


def evaluate(disk_percent, memory_percent, backup_age_hours, http_ok):
    failures = []
    if disk_percent > 80:
        failures.append('disk_above_80_percent')
    if memory_percent > 90:
        failures.append('memory_above_90_percent')
    if backup_age_hours > 26:
        failures.append('backup_older_than_26_hours')
    if not http_ok:
        failures.append('https_smoke_failed')
    return failures


if __name__ == '__main__':
    disk = shutil.disk_usage('/var/lib/docker')
    memory = {}
    for line in Path('/proc/meminfo').read_text().splitlines():
        key, value = line.split(':', 1)
        memory[key] = int(value.strip().split()[0])
    latest = Path(os.environ.get('BACKUP_STATUS_FILE', '/var/backups/restaurant/staging/latest.json'))
    age = (time.time() - latest.stat().st_mtime) / 3600 if latest.exists() else float('inf')
    if latest.exists() and json.loads(latest.read_text()).get('status') != 'PASS':
        age = float('inf')
    origin = os.environ.get('RESTAURANT_ORIGIN', 'https://staging.quanlynh.id.vn')
    failures = evaluate(disk.used / disk.total * 100, (1 - memory['MemAvailable'] / memory['MemTotal']) * 100,
                        age, run(origin)['status'] == 'PASS')
    result = {'status': 'FAIL' if failures else 'PASS', 'failures': failures}
    print(json.dumps(result))
    raise SystemExit(bool(failures))

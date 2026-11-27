"""Start the isolated local acceptance stack; private values never enter Git or stdout."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
from urllib.request import urlopen


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--skip-build', action='store_true')
    parser.add_argument('--seed', action='store_true', help='Explicitly rerun the idempotent seed/migrations on this demo database')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    os.chdir(repo)
    root = repo / 'artifacts/hai-live-demo'
    root.mkdir(parents=True, exist_ok=True)
    runtime, credentials = root / 'runtime.env', root / 'credentials.json'
    if not runtime.exists():
        password = secrets.token_urlsafe(24) + 'Aa1!'
        database = 'RestaurantDemo_' + secrets.token_hex(16)
        runtime.write_text('DEMO_SQL_PASSWORD=' + secrets.token_hex(24) + 'Aa1!\nDEMO_DATABASE=' + database + '\nDEMO_JWT_KEY=' + secrets.token_urlsafe(48) + '\n', encoding='utf-8')
        credentials.write_text(json.dumps({'origin': 'http://localhost:3187', 'database': database, 'localOnly': True,
            'accounts': [{'userName': 'demo.' + role.lower(), 'role': role, 'password': password} for role in ['Admin', 'Manager', 'Staff', 'Kitchen', 'Bar']]}, indent=2), encoding='utf-8')
    if not credentials.exists():
        raise RuntimeError('Existing private demo credentials are missing; configuration was not overwritten')
    keys = {line.split('=', 1)[0] for line in runtime.read_text().splitlines() if line.strip()}
    if not keys <= {'DEMO_SQL_PASSWORD', 'DEMO_DATABASE', 'DEMO_JWT_KEY', 'DEMO_REVISION'}:
        raise RuntimeError('Unexpected variables in the private demo configuration')
    identity = json.loads(credentials.read_text())
    if not identity.get('localOnly') or identity.get('origin') != 'http://localhost:3187':
        raise RuntimeError('Only the isolated local demo is allowed')
    compose = ['docker', 'compose', '--env-file', str(runtime), '-f', 'infra/compose.demo.yml']
    def run(arguments, **kwargs):
        return subprocess.run(arguments, check=True, **kwargs)
    if not args.skip_build:
        run(compose + ['build'])
    run(compose + ['up', '-d', 'sqlserver'])
    container = subprocess.check_output(compose + ['ps', '-q', 'sqlserver'], text=True).strip()
    for _ in range(60):
        if subprocess.check_output(['docker', 'inspect', '-f', '{{.State.Health.Status}}', container], text=True).strip() == 'healthy':
            break
        time.sleep(2)
    else:
        raise RuntimeError('Demo SQL did not become healthy')
    if args.seed or not (root / 'status.json').exists():
        env = {**os.environ, 'DemoSeed__Password': identity['accounts'][0]['password']}
        run(compose + ['run', '--rm', '--no-deps', '-e', 'DemoSeed__Password', 'api', '--seed-demo'], env=env)
    run(compose + ['up', '-d', 'api', 'web', 'nginx'])
    run(compose + ['exec', '-T', 'nginx', 'nginx', '-s', 'reload'])
    for _ in range(60):
        try:
            with urlopen(identity['origin'] + '/health/ready', timeout=5) as response:
                if response.status == 200 and response.read().strip() == b'Healthy':
                    break
        except Exception:
            pass
        time.sleep(2)
    else:
        raise RuntimeError('Demo API did not become healthy')
    (root / 'status.json').write_text(json.dumps({'origin': identity['origin'], 'localOnly': True, 'apiReady': True, 'seeded': True}, indent=2))
    print('Demo ready at http://localhost:3187; credentials: artifacts/hai-live-demo/credentials.json (private).')


if __name__ == '__main__':
    main()

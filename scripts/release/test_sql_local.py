"""Cross-platform SQL test runner. Creates and removes only its own Docker container."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
from evidence import summarize_trx


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--filter')
    parser.add_argument('--no-build', action='store_true')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    os.chdir(repo)
    name = 'restaurant-hai-tests-' + secrets.token_hex(6)
    output = repo / 'artifacts' / ('hai-sql-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    output.mkdir(parents=True, exist_ok=False)
    env = {**os.environ, 'MSSQL_SA_PASSWORD': secrets.token_hex(24) + 'Aa1!'}
    created = False
    def run(command, **kwargs):
        return subprocess.run(command, check=True, **kwargs)
    try:
        run(['docker', 'run', '-d', '--name', name, '--memory', '2g', '-p', '127.0.0.1::1433',
             '-e', 'ACCEPT_EULA=Y', '-e', 'MSSQL_MEMORY_LIMIT_MB=1536', '-e', 'MSSQL_SA_PASSWORD',
             'mcr.microsoft.com/mssql/server:2022-latest'], env=env, stdout=subprocess.DEVNULL)
        created = True
        for attempt in range(60):
            ready = subprocess.run(['docker', 'exec', name, 'bash', '-c',
                'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -Q "SELECT 1"'],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
            if ready.returncode == 0:
                break
            time.sleep(2)
        else:
            raise RuntimeError('Disposable SQL Server did not become ready')
        port = subprocess.check_output(['docker', 'inspect', '-f', '{{(index (index .NetworkSettings.Ports "1433/tcp") 0).HostPort}}', name], text=True).strip()
        if not port.isdecimal():
            raise RuntimeError('Unexpected SQL host port')
        env['IDENTITY_TEST_SQLSERVER'] = f'Server=127.0.0.1,{port};User Id=sa;Password={env["MSSQL_SA_PASSWORD"]};Encrypt=True;TrustServerCertificate=True;Connect Timeout=15;Command Timeout=120'
        del env['MSSQL_SA_PASSWORD']
        if not args.no_build:
            run(['dotnet', 'restore', 'RestaurantManagement.slnx', '--locked-mode'], env=env)
            run(['dotnet', 'build', 'RestaurantManagement.slnx', '-c', 'Release', '--no-restore', '-m:2'], env=env)
        target = 'tests/Restaurant.IntegrationTests' if args.filter else 'RestaurantManagement.slnx'
        command = ['dotnet', 'test', target, '-c', 'Release', '--no-build', '--no-restore', '--settings',
                   'scripts/release/sql.runsettings', '--logger', 'trx', '--results-directory', str(output)]
        if args.filter:
            command.extend(['--filter', args.filter])
        result = subprocess.run(command, env=env)
        report = summarize_trx(output)
        report['sqlExecutionVerified'] = True
        (output / 'summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print('Evidence:', output.relative_to(repo))
        return result.returncode or (0 if report['status'] == 'PASS' else 1)
    finally:
        if created:
            # name is generated above, never supplied by the user or a configuration file.
            subprocess.run(['docker', 'rm', '-f', '-v', name], check=True, stdout=subprocess.DEVNULL)


if __name__ == '__main__':
    raise SystemExit(main())

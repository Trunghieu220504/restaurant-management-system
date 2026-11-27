"""Run only inside an isolated Linux container, never on the workstation/VPS.
Docker, curl, hostname and sleep are stubs; no Docker socket is mounted.
Exercise whole deployment control flow, including failures after writers stop.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

REVISION = 'a' * 40
ROOT = Path('/opt/restaurant/releases') / REVISION
STUB = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
args=sys.argv[1:]
name=pathlib.Path(sys.argv[0]).name
state=pathlib.Path(os.environ['CANDIDATE_TEST_STATE'])
scenario=os.environ['CANDIDATE_TEST_SCENARIO']
def log(text):
 with (state/'events').open('a') as f: f.write(text+'\n')
if name=='hostname': print('restaurant-staging'); sys.exit(0)
if name=='sleep': sys.exit(0)
if name=='curl':
 sys.exit(1 if scenario=='smoke' and (state/'switched').exists() and not (state/'rollback').exists() else 0)
if args[0]=='image': print('a'*40); sys.exit(0)
if args[0]=='inspect':
 target=args[-1]
 if '-f' in args:
  fmt=args[args.index('-f')+1]
  print('true' if 'Running' in fmt else 'a'*40 if 'Labels' in fmt else 'sql-unchanged' if 'Id' in fmt else 'sha256:'+'b'*64)
 else:
  print(json.dumps([{'Config':{'Env':['ConnectionStrings__Restaurant=Server=sql;Database=RestaurantStaging;User Id=test;Password=fixture','ReverseProxy__KnownProxies__0=172.18.0.3']},'NetworkSettings':{'Networks':{'staging_internal':{'IPAddress':'172.18.0.3'}}},'Mounts':[{'Destination':'/var/opt/mssql','Source':str(state)}]}]))
 sys.exit(0)
if args[0]=='compose':
 if 'config' in args:
  if '--format' in args: print(json.dumps({'services':{'nginx':{'networks':{'internal':None}}}}))
 elif 'stop' in args: log('writers-stopped')
 elif 'up' in args:
  rollback=any('rollback.override.json' in x for x in args)
  if rollback:
   log('rollback'); (state/'rollback').touch()
  else:
   log('new-images'); (state/'switched').touch()
   if scenario=='startup': sys.exit(1)
 sys.exit(0)
if args[0]=='exec':
 target=args[2] if args[1]=='-i' else args[1]
 if 'sqlserver' in target:
  text=sys.stdin.read()
  if 'SUM(CONVERT' in text: print('1024')
  elif 'FILELISTONLY' in text: print('FixtureData|unused|D|unused\nFixtureLog|unused|L|unused')
  elif '-- fixture-migration' in text:
   log('migration:'+args[-1])
   if scenario=='migration' and args[-1]=='RestaurantStaging': sys.exit(1)
  elif 'BACKUP DATABASE' in text: log('backup')
 elif 'cat' in args: print('server { listen 80; }')
 sys.exit(0)
if args[0]=='cp': pathlib.Path(args[-1]).write_bytes(b'fixture-backup'); sys.exit(0)
if args[0]=='run': log('probe-or-nginx-validation'); sys.exit(0)
if args[0]=='rm': sys.exit(0)
raise SystemExit('Unexpected fake docker call: '+repr(args))
'''

@unittest.skipUnless(os.environ.get('CANDIDATE_TEST_ISOLATED') == 'true', 'isolated container required')
class StagingCandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='candidate-test-')
        self.state = Path(self.temp.name)
        self.bin = self.state / 'bin'
        self.bin.mkdir()
        for name in ('docker', 'curl', 'sleep', 'hostname'):
            path = self.bin / name
            path.write_text(STUB)
            path.chmod(0o755)
        ROOT.mkdir(parents=True, exist_ok=True)
        (ROOT / 'compose').mkdir(exist_ok=True)
        (ROOT / 'migrations').mkdir(exist_ok=True)
        for name in ('.env', 'compose.backend.yml', 'compose.backend.https.yml', 'compose.frontend.yml',
                     'compose.backend.ghcr.yml', 'compose.frontend.ghcr.yml', 'compose.week1-trusted-proxy.yml'):
            (ROOT / 'compose' / name).write_text('# fixture\n')
        source = Path('/work/scripts/release/staging-candidate.sh').read_text()
        source = source.replace('export PATH=/usr/sbin:/usr/bin:/sbin:/bin', f'export PATH={self.bin}:/usr/sbin:/usr/bin:/sbin:/bin')
        self.script = ROOT / 'staging-candidate.sh'
        self.script.write_text(source)
        self.script.chmod(0o700)
        (ROOT / 'release.json').write_text(json.dumps({'revision':REVISION,'apiImage':'sha256:'+'c'*64,'webImage':'sha256:'+'d'*64}))
        (ROOT / 'nginx.conf').write_text('# fixture\n')
        (ROOT / 'migrations/order.txt').write_text('Identity.sql\n')
        (ROOT / 'migrations/Identity.sql').write_text('-- fixture-migration\n')
        paths = [self.script, ROOT/'release.json', ROOT/'nginx.conf', ROOT/'migrations/order.txt', ROOT/'migrations/Identity.sql']
        (ROOT / 'SHA256SUMS').write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.relative_to(ROOT)}\n' for p in paths))
        for p in ROOT.rglob('*'):
            if p.is_file(): p.chmod(0o600)
        Path('/var/backups').mkdir(exist_ok=True)
        Path('/run/lock').mkdir(exist_ok=True)
    def tearDown(self):
        self.temp.cleanup()
    def run_case(self, scenario, mode='--apply'):
        result = subprocess.run(['bash',str(self.script),mode], env={**os.environ,'CANDIDATE_TEST_STATE':str(self.state),'CANDIDATE_TEST_SCENARIO':scenario},capture_output=True,text=True,timeout=20)
        events=(self.state/'events').read_text().splitlines() if (self.state/'events').exists() else []
        return result,events
    def test_plan_never_stops_writers_or_changes_images(self):
        result, events = self.run_case('success','--plan')
        self.assertEqual(0,result.returncode,result.stderr)
        self.assertNotIn('writers-stopped',events)
        self.assertNotIn('new-images',events)
    def test_success_checks_backup_and_migrations_before_switching(self):
        result, events = self.run_case('success')
        self.assertEqual(0,result.returncode,result.stderr)
        self.assertLess(events.index('backup'),events.index('writers-stopped'))
        self.assertLess(events.index('writers-stopped'),events.index('migration:RestaurantStaging'))
        self.assertLess(events.index('migration:RestaurantStaging'),events.index('new-images'))
        self.assertNotIn('rollback',events)
    def test_live_migration_failure_restores_old_application(self):
        result, events = self.run_case('migration')
        self.assertNotEqual(0,result.returncode)
        self.assertIn('writers-stopped',events)
        self.assertIn('rollback',events)
        self.assertNotIn('new-images',events)
    def test_start_failure_rolls_back_and_remains_failed(self):
        result, events = self.run_case('startup')
        self.assertNotEqual(0,result.returncode)
        self.assertIn('new-images',events)
        self.assertIn('rollback',events)
    def test_readiness_failure_rolls_back_and_remains_failed(self):
        result, events = self.run_case('smoke')
        self.assertNotEqual(0,result.returncode)
        self.assertIn('rollback',events)
    def test_tampered_bundle_is_rejected_before_backup(self):
        (ROOT/'migrations/Identity.sql').write_text('tampered')
        result, events = self.run_case('success')
        self.assertNotEqual(0,result.returncode)
        self.assertNotIn('backup',events)
        self.assertNotIn('writers-stopped',events)

if __name__ == '__main__':
    unittest.main()

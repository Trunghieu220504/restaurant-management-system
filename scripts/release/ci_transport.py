#!/usr/bin/env python3
"""Forced SSH command for the deploy key. No shell/SFTP/forwarding commands."""
import os, re, shutil, subprocess, sys, tarfile, tempfile
from pathlib import Path
MAX_TOTAL = 1_500_000_000
BASE = Path('/home/deploy/restaurant-ci-incoming')
MODULES = {'Identity','SalesDay','Catalog','Seating','Sales','Billing','Inventory','Fulfillment','Audit'}
def allowed(name):
    return name in {'release.json','images.tar.gz','SHA256SUMS','migrations/order.txt','migrations/SHA256SUMS'} or any(name == f'migrations/{m}.sql' for m in MODULES)
def receive(stream, base, sha):
    base.mkdir(mode=0o700, parents=True, exist_ok=True)
    target = base / sha
    if target.exists(): raise ValueError('Immutable upload already exists')
    temporary = Path(tempfile.mkdtemp(prefix='.upload-', dir=base))
    try:
        seen = set(); total = 0
        with tarfile.open(fileobj=stream, mode='r|') as archive:
            for member in archive:
                if not member.isfile() or not allowed(member.name) or member.name in seen:
                    raise ValueError('Invalid archive entry')
                total += member.size
                if member.size < 0 or total > MAX_TOTAL: raise ValueError('Archive too large')
                seen.add(member.name)
                path=temporary/member.name
                path.parent.mkdir(mode=0o700, exist_ok=True)
                with archive.extractfile(member) as source, path.open('xb') as output:
                    shutil.copyfileobj(source,output)
                path.chmod(0o600)
        if not {'release.json','images.tar.gz','SHA256SUMS','migrations/order.txt'} <= seen:
            raise ValueError('Incomplete release')
        temporary.rename(target)
    finally:
        if temporary.exists(): shutil.rmtree(temporary)
def main():
    command=os.environ.get('SSH_ORIGINAL_COMMAND','')
    match=re.fullmatch(r'(upload|deploy) ([0-9a-f]{40})',command)
    if not match: return 2
    operation,sha=match.groups()
    if operation=='upload':
        receive(sys.stdin.buffer,BASE,sha)
        print('Upload complete:',sha)
        return 0
    return subprocess.call(['sudo','-n','/usr/local/sbin/restaurant-ci-deploy',sha])
if __name__=='__main__':
    try: raise SystemExit(main())
    except (ValueError, OSError, tarfile.TarError) as error:
        print('Release transport rejected:',str(error),file=sys.stderr); raise SystemExit(2)

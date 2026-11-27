#!/usr/bin/env python3
"""Root-owned deploy entry point. Only current develop or root-approved revisions."""
import fcntl, hashlib, json, os, re, shutil, stat, subprocess, sys, tempfile
from pathlib import Path
MODULES={'Identity','SalesDay','Catalog','Seating','Sales','Billing','Inventory','Fulfillment','Audit'}
TOOLS=Path('/usr/local/lib/restaurant-ci')
REPO='https://github.com/Trunghieu220504/restaurant-management-system.git'
def regular(path):
    if not stat.S_ISREG(path.lstat().st_mode): raise ValueError('Expected regular file')
def digest(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def main():
    if os.geteuid()!=0 or len(sys.argv)!=2 or not re.fullmatch('[0-9a-f]{40}',sys.argv[1]):return 2
    sha=sys.argv[1]
    approved=TOOLS/'approved-revisions'
    allow=set(approved.read_text().splitlines()) if approved.exists() else set()
    head=subprocess.check_output(['git','ls-remote',REPO,'refs/heads/develop'],text=True,timeout=45).split()[0]
    if sha!=head and sha not in allow:raise ValueError('Revision is neither current develop nor explicitly approved')
    lock=open('/run/lock/restaurant-ci-install.lock','w')
    fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    source=Path('/home/deploy/restaurant-ci-incoming')/sha
    if source.is_symlink() or not source.is_dir():raise ValueError('Missing upload directory')
    release=Path('/opt/restaurant/releases')/sha
    if release.exists():
        raise ValueError('Release already installed: inspect previous deployment before retry')
    private=Path(tempfile.mkdtemp(prefix='restaurant-ci-',dir='/var/backups'))
    (private/'migrations').mkdir(mode=0o700)
    paths=['release.json','images.tar.gz','SHA256SUMS','migrations/order.txt','migrations/SHA256SUMS']+[f'migrations/{m}.sql' for m in sorted(MODULES)]
    for name in paths:
        regular(source/name)
        shutil.copyfile(source/name,private/name)
        (private/name).chmod(0o600)
    entries={}
    for line in (private/'SHA256SUMS').read_text().splitlines():
        checksum,name=line.split('  ',1)
        if name not in paths or name=='SHA256SUMS' or name in entries or not re.fullmatch('[0-9a-f]{64}',checksum):raise ValueError('Invalid manifest')
        entries[name]=checksum
    if set(entries)!=set(paths)-{'SHA256SUMS'}:raise ValueError('Manifest coverage incomplete')
    for name,checksum in entries.items():
        if digest(private/name)!=checksum:raise ValueError('Checksum mismatch: '+name)
    manifest=json.loads((private/'release.json').read_text())
    if manifest['revision']!=sha:raise ValueError('Revision mismatch')
    order=(private/'migrations/order.txt').read_text().splitlines()
    if order!=[m+'.sql' for m in ('Identity','SalesDay','Catalog','Seating','Sales','Billing','Inventory','Fulfillment','Audit')]:raise ValueError('Unexpected migration order')
    with (private/'image-load.log').open('w') as log:
        subprocess.run(['docker','load','-i',str(private/'images.tar.gz')],check=True,stdout=log,stderr=log,timeout=300)
    for component in ('api','web'):
        image=manifest[component+'Image']
        if not re.fullmatch('sha256:[0-9a-f]{64}',image):raise ValueError('Image must be immutable')
        actual=json.loads(subprocess.check_output(['docker','image','inspect',image]))[0]
        if actual['Config'].get('Labels',{}).get('org.opencontainers.image.revision')!=sha:raise ValueError('Image source mismatch')
    # Never execute deployment scripts received from CI. Copy reviewed root-owned tools.
    for name,origin in [('staging-candidate.sh',TOOLS/'staging-candidate.sh'),('nginx.conf',TOOLS/'nginx.conf')]:
        regular(origin);shutil.copyfile(origin,private/name);(private/name).chmod(0o600)
    trusted=Path('/opt/restaurant/releases/8af7ab3/compose')
    (private/'compose').mkdir(mode=0o700)
    for name in ('.env','compose.backend.yml','compose.backend.https.yml','compose.frontend.yml','compose.backend.ghcr.yml','compose.frontend.ghcr.yml','compose.week1-trusted-proxy.yml'):
        path=trusted/name;regular(path)
        st=path.stat()
        if st.st_uid!=0 or st.st_mode & 0o022:raise ValueError('Untrusted existing Compose file')
        shutil.copyfile(path,private/'compose'/name);(private/'compose'/name).chmod(0o600)
    names=list(entries)+['staging-candidate.sh','nginx.conf']
    (private/'SHA256SUMS').write_text(''.join(digest(private/n)+'  '+n+'\n' for n in names))
    private.rename(release)
    (release/'staging-candidate.sh').chmod(0o700)
    subprocess.run([str(release/'staging-candidate.sh'),'--plan'],check=True)
    subprocess.run([str(release/'staging-candidate.sh'),'--apply'],check=True)
    print('CI deployment completed:',sha)
    return 0
if __name__=='__main__':
    try:raise SystemExit(main())
    except Exception as error:
        print('CI deploy failed:',str(error),file=sys.stderr);raise SystemExit(1)

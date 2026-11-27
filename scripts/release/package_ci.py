#!/usr/bin/env python3
"""Build immutable images for the checked-out SHA and package migrations; no secrets."""
import gzip,hashlib,json,shutil,subprocess,tarfile,tempfile
from pathlib import Path
ROOT=Path.cwd()
def digest(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    sha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
    parent=ROOT/'artifacts/release'
    parent.mkdir(parents=True,exist_ok=True)
    output=Path(tempfile.mkdtemp(prefix='ci-package-',dir=parent))
    tags=[f'restaurant-{c}:ci-{sha}' for c in ('api','web')]
    for component,tag in zip(('api','web'),tags):
        args=['docker','build','--label','org.opencontainers.image.revision='+sha,'-t',tag]
        args+=['-f','infra/docker/Dockerfile.api','.'] if component=='api' else ['--build-arg','NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true','--build-arg','API_URL=http://api:8080','web']
        subprocess.run(args,check=True)
    images=json.loads(subprocess.check_output(['docker','image','inspect',*tags]))
    for i in images:
        assert i['Config']['Labels']['org.opencontainers.image.revision']==sha
    (output/'release.json').write_text(json.dumps({'revision':sha,'apiImage':images[0]['Id'],'webImage':images[1]['Id']},indent=2))
    shutil.copytree(ROOT/'artifacts/release/migrations',output/'migrations')
    with gzip.open(output/'images.tar.gz','wb',compresslevel=1) as out:
        with subprocess.Popen(['docker','save',*tags],stdout=subprocess.PIPE) as p:
            shutil.copyfileobj(p.stdout,out,4*1024*1024)
            if p.wait():raise RuntimeError('Image export failed')
    files=sorted(p for p in output.rglob('*') if p.is_file())
    (output/'SHA256SUMS').write_text(''.join(digest(p)+'  '+p.relative_to(output).as_posix()+'\n' for p in files))
    with tarfile.open(ROOT/'artifacts/release/ci-package.tar','w') as tar:
        for p in sorted(output.rglob('*')):
            if p.is_file():tar.add(p,arcname=p.relative_to(output).as_posix(),recursive=False)
    print('Packaged revision',sha)
if __name__=='__main__':main()

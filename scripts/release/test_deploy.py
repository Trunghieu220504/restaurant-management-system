"""Exercise deploy orchestration with fake Docker/SQL, never a live database."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


class DeploymentTests(unittest.TestCase):
    def run_deploy(self, failure):
        bash = os.environ.get('RELEASE_BASH') or shutil.which('bash')
        self.assertIsNotNone(bash)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            # Git Bash accepts C:/ paths; the fixture contains no shell metacharacters.
            path = root.as_posix()
            (root / 'staging').mkdir()
            common = r'''
set -euo pipefail
DATABASE=RestaurantTest
COMPOSE_DIRECTORY="$TEST_ROOT/staging"
STAGING_ORIGIN=https://staging.quanlynh.id.vn
MIGRATION_COMPATIBILITY_DIRECTORY="$TEST_ROOT"
flock() { :; }
install() { mkdir -p "${@: -1}"; }
sleep() { :; }
python3() {
    if [ "$1" = - ]; then cat >/dev/null; return 0; fi
    if [ "$FAIL_MODE" = smoke ] && [[ "$*" != *rollback.json* ]]; then return 1; fi
    return 0
}
sql_run() { cat >/dev/null; [ "$FAIL_MODE" != migration ]; }
docker() {
    case "$1" in
    compose)
        local verb=''
        for arg in "$@"; do
            case "$arg" in ps|up|stop|exec) verb=$arg; break;; esac
        done
        case "$verb" in
            ps) printf 'old-%s\n' "${@: -1}" ;;
            up)
                cat "$COMPOSE_DIRECTORY/images.yml" >> "$TEST_ROOT/up.log"
                if [ "$FAIL_MODE" = start ] && grep -q ':bbbb' "$COMPOSE_DIRECTORY/images.yml"; then return 1; fi ;;
            stop) echo stopped >> "$TEST_ROOT/events.log" ;;
            exec) echo reload >> "$TEST_ROOT/events.log" ;;
        esac ;;
    inspect)
        printf 'ghcr.io/trunghieu220504/restaurant-%s@sha256:%s\n' "${@: -1:1}" "$(printf 'a%.0s' {1..64})" | sed 's/restaurant-old-/restaurant-/' ;;
    image)
        if [[ "$*" = *org.opencontainers.image.revision* ]]; then printf '%s\n' "$REVISION";
        else printf '%s@sha256:%s\n' "${@: -1:1}" "$(printf 'b%.0s' {1..64})" | sed "s/:$REVISION//"; fi ;;
    create) echo migration-fixture ;;
    cp)
        local target="${@: -1:1}/migrations"
        mkdir -p "$target"
        printf 'SELECT 1;\n' > "$target/Identity.sql"
        printf 'Identity.sql\n' > "$target/order.txt"
        (cd "$target" && sha256sum Identity.sql order.txt > SHA256SUMS) ;;
    pull|rm) : ;;
    *) echo 'Unexpected fake Docker call' >&2; return 2 ;;
    esac
}
export -f docker
'''
            (root / 'common.sh').write_text(common, encoding='utf-8', newline='\n')
            (root / 'backup.sh').write_text('#!/usr/bin/env bash\nexit 0\n', encoding='utf-8', newline='\n')
            (root / 'backup.sh').chmod(0o755)
            script = Path(__file__).with_name('deploy.sh').read_text(encoding='utf-8-sig')
            script = script.replace('source /usr/local/lib/restaurant/common.sh', 'source "$TEST_ROOT/common.sh"')
            script = script.replace('/etc/restaurant/staging', path + '/staging')
            script = script.replace('/var/lib/restaurant/releases', path + '/releases')
            script = script.replace('/run/lock/restaurant-deploy.lock', path + '/deploy.lock')
            script = script.replace('/usr/local/lib/restaurant/backup.sh', 'bash "$TEST_ROOT/backup.sh"')
            (root / 'deploy.sh').write_text(script, encoding='utf-8', newline='\n')
            revision = 'c' * 40
            env = {**os.environ, 'TEST_ROOT': path, 'FAIL_MODE': failure, 'REVISION': revision}
            result = subprocess.run([bash, str(root / 'deploy.sh'), revision], env=env, capture_output=True, text=True, timeout=15)
            images = (root / 'staging/images.yml').read_text() if (root / 'staging/images.yml').exists() else ''
            return result, images

    def test_success_keeps_new_digests(self):
        result, images = self.run_deploy('none')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('@sha256:' + 'b' * 64, images)

    def test_migration_start_and_smoke_failures_restore_previous_digests(self):
        for failure in ('migration', 'start', 'smoke'):
            with self.subTest(failure=failure):
                result, images = self.run_deploy(failure)
                self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn('@sha256:' + 'a' * 64, images, result.stdout + result.stderr)
                self.assertIn('original release remains FAILED', result.stderr)

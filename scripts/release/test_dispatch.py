import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


class DispatchTests(unittest.TestCase):
    def test_shell_commands_and_malformed_revisions_are_rejected(self):
        bash = os.environ.get('RELEASE_BASH') or shutil.which('bash')
        self.assertIsNotNone(bash, 'Bash is required for the security boundary test')
        script = str(Path(__file__).with_name('ssh-dispatch.sh').resolve())
        for command in ('', 'bash', 'deploy main', 'deploy ' + 'a' * 39, 'deploy ' + 'a' * 40 + ';id', 'deploy ' + 'a' * 40 + '\nwhoami'):
            env = {**os.environ, 'SSH_ORIGINAL_COMMAND': command}
            result = subprocess.run([bash, script], env=env, capture_output=True, timeout=10)
            self.assertEqual(result.returncode, 2, command)

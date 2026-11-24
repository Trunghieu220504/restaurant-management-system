import unittest
from monitor import evaluate


class MonitorTests(unittest.TestCase):
    def test_thresholds_without_filling_real_disk(self):
        self.assertEqual(evaluate(80, 90, 26, True), [])
        self.assertEqual(evaluate(81, 91, 27, False), [
            'disk_above_80_percent', 'memory_above_90_percent',
            'backup_older_than_26_hours', 'https_smoke_failed'])

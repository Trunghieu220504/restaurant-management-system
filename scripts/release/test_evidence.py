import tempfile
import unittest
from pathlib import Path
from evidence import summarize_trx, matrix_report
from smoke import check_origin


class EvidenceTests(unittest.TestCase):
    def test_no_results_cannot_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                summarize_trx(directory)

    def test_skips_and_failed_runs_cannot_pass(self):
        for outcome in ('NotExecuted', 'Failed', 'Timeout'):
            with self.subTest(outcome=outcome), tempfile.TemporaryDirectory() as directory:
                Path(directory, 'run.trx').write_text(f'<TestRun><ResultSummary outcome="Completed"><Counters/></ResultSummary><Results><UnitTestResult testName="case" outcome="{outcome}"/></Results></TestRun>')
                self.assertEqual(summarize_trx(directory)['status'], 'FAIL')

    def test_namespaced_results_do_not_prove_database_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            Path(directory, 'run.trx').write_text('<TestRun xmlns="urn:trx"><ResultSummary outcome="Completed"><Counters passed="1"/></ResultSummary><Results><UnitTestResult testId="1" outcome="Passed"/></Results><TestDefinitions><UnitTest id="1"><TestMethod className="Tests.SQL" name="Atomic"/></UnitTest></TestDefinitions></TestRun>')
            result = summarize_trx(directory)
            self.assertEqual(result['passed'], 1)
            self.assertFalse(result['sqlExecutionVerified'])
            self.assertEqual(result['tests'][0]['name'], 'Tests.SQL.Atomic')

    def test_partial_coverage_and_missing_database_proof_block_matrix(self):
        evidence = {'tests': [{'name': 'SQL.Test', 'status': 'Passed'}]}
        mapping = [{'id': 1, 'tests': ['SQL.Test'], 'coverageComplete': True}]
        self.assertEqual(matrix_report(mapping, evidence)['rows'][0]['status'], 'SKIP')
        mapping[0]['coverageComplete'] = False
        self.assertEqual(matrix_report(mapping, evidence, True)['rows'][0]['status'], 'SKIP')
        mapping[0]['coverageComplete'] = True
        mapping[0]['tests'].append('SQL.Missing')
        self.assertEqual(matrix_report(mapping, evidence, True)['rows'][0]['status'], 'SKIP')

    def test_smoke_rejects_credentials_paths_and_plain_http(self):
        for origin in ('http://example.com', 'https://u:p@example.com', 'https://example.com/a', 'https://example.com?key=secret', 'https://example.com#x'):
            with self.assertRaises(ValueError):
                check_origin(origin)
        self.assertEqual(check_origin('https://example.com/'), 'https://example.com')


if __name__ == '__main__':
    unittest.main()

"""Fail-closed test evidence. Never infer SQL execution from a green TRX alone."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import xml.etree.ElementTree as ET


def summarize_trx(directory):
    files = sorted(Path(directory).glob('*.trx'))
    if not files:
        raise ValueError('No TRX files found')
    tests = []
    run_failed = False
    for path in files:
        root = ET.parse(path).getroot()
        definitions = {}
        for definition in root.findall('.//{*}UnitTest'):
            method = definition.find('{*}TestMethod')
            if method is not None:
                definitions[definition.attrib['id']] = method.attrib.get('className', '').split(',')[0] + '.' + method.attrib.get('name', '')
        results = root.findall('.//{*}UnitTestResult')
        if not results:
            raise ValueError('Empty test run: ' + path.name)
        summary = root.find('{*}ResultSummary')
        counters = root.find('.//{*}Counters')
        if summary is None or counters is None:
            raise ValueError('Incomplete test run: ' + path.name)
        run_failed |= summary.attrib.get('outcome') not in ('Completed', 'Passed')
        run_failed |= any(int(counters.attrib.get(k, '0')) for k in ('failed', 'error', 'timeout', 'aborted', 'notExecuted', 'disconnected'))
        for result in results:
            tests.append({'name': definitions.get(result.attrib.get('testId'), result.attrib.get('testName', '')),
                          'status': result.attrib.get('outcome'), 'file': path.name})
    passed = sum(t['status'] == 'Passed' for t in tests)
    return {'status': 'PASS' if not run_failed and passed == len(tests) else 'FAIL', 'passed': passed,
            'total': len(tests), 'sqlExecutionVerified': False, 'tests': tests}


def matrix_report(mapping, evidence, sql_verified=False):
    names = {item['name'] for item in evidence['tests'] if item['status'] == 'Passed'}
    failed = {item['name'] for item in evidence['tests'] if item['status'] not in ('Passed', 'NotExecuted')}
    rows = []
    for item in mapping:
        required = item.get('tests', [])
        missing = [name for name in required if name not in names]
        complete = bool(required) and not missing and item.get('coverageComplete', False)
        status = 'FAIL' if any(name in failed for name in required) else 'PASS' if complete and sql_verified else 'SKIP'
        rows.append({**item, 'status': status, 'missingTests': missing})
    return {'status': 'PASS' if len(rows) == 17 and all(r['status'] == 'PASS' for r in rows) else 'BLOCKED', 'rows': rows}


def verify_proof(directory, proof):
    paths = sorted(Path(directory).glob('*.trx'))
    hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    return bool(paths) and proof.get('sqlReady') is True and proof.get('revision') == revision and proof.get('trxSha256') == hashes


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['trx', 'matrix'])
    parser.add_argument('directory')
    parser.add_argument('--output', required=True)
    parser.add_argument('--sql-proof')
    args = parser.parse_args()
    result = summarize_trx(args.directory)
    if args.command == 'matrix':
        proof = json.loads(Path(args.sql_proof).read_text(encoding='utf-8-sig')) if args.sql_proof else {}
        mapping = json.loads((Path(__file__).parent / 'matrix.json').read_text(encoding='utf-8-sig'))
        result = matrix_report(mapping, result, verify_proof(args.directory, proof))
    target = Path(args.output)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(result['status'])
    return 0 if result['status'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())

"""Read-only HTTPS deployment smoke. No credentials or application writes."""
import argparse
import json
import ssl
import time
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, HTTPSHandler, HTTPRedirectHandler


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def check_origin(origin):
    url = urlsplit(origin)
    if url.scheme != 'https' or not url.hostname or url.username or url.password or url.path not in ('', '/') or url.query or url.fragment:
        raise ValueError('An HTTPS origin without credentials, query or path is required')
    return origin.rstrip('/')


def run(origin):
    origin = check_origin(origin)
    opener = build_opener(HTTPSHandler(context=ssl.create_default_context()), NoRedirect())
    checks = []
    for path in ('/health/live', '/health/ready', '/login'):
        start = time.monotonic()
        try:
            with opener.open(Request(origin + path, headers={'User-Agent': 'RestaurantReleaseSmoke/1'}), timeout=15) as response:
                body = response.read(1048576).decode('utf-8', errors='replace')
                passed = response.status == 200 and (body.strip() == 'Healthy' if path.startswith('/health/') else '<html' in body.lower())
                hsts = bool(response.headers.get('Strict-Transport-Security'))
                checks.append({'path': path, 'status': 'PASS' if passed and hsts else 'FAIL', 'httpStatus': response.status,
                               'hsts': hsts, 'elapsedMs': round((time.monotonic() - start) * 1000)})
        except Exception as exc:
            checks.append({'path': path, 'status': 'FAIL', 'errorType': type(exc).__name__})
    return {'origin': origin, 'status': 'PASS' if all(c['status'] == 'PASS' for c in checks) else 'FAIL', 'checks': checks}


if __name__ == '__main__':
    from pathlib import Path
    parser = argparse.ArgumentParser()
    parser.add_argument('origin')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    result = run(args.origin)
    target = Path(args.output)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result['status'] == 'PASS' else 1)

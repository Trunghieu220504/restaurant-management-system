import http from 'k6/http';
import { check, fail } from 'k6';
import { Trend } from 'k6/metrics';

// Run only against a dedicated acceptance environment with disposable business data.
// The fixture is private and supplies actual API paths/payloads for this exact revision.
const fixture = JSON.parse(open(__ENV.FLOW_FILE));
if (__ENV.ALLOW_TEST_WRITES !== 'true') throw new Error('Dedicated test-data consent is required');
const base = __ENV.BASE_URL;
const localDemo = base === 'http://localhost:3187';
if (!localDemo && !/^https:\/\/[^/]+$/.test(base || '')) throw new Error('BASE_URL must be an HTTPS origin or the loopback demo');
if (localDemo && __ENV.LOAD_USERNAME !== 'demo.staff') throw new Error('Loopback load tests require the demo Staff account');
const operations = ['login', 'order', 'confirm', 'payment'];
for (const operation of operations) {
    if (!fixture.steps.some(step => step.operation === operation)) throw new Error(`Missing ${operation} step`);
}
const timings = Object.fromEntries(operations.map(name => [name, new Trend(`flow_${name}`, true)]));
export const options = {
    scenarios: {
        hot_flows: { executor: 'per-vu-iterations', vus: 2, iterations: 4, maxDuration: '1m' },
    },
    thresholds: {
        checks: ['rate==1'],
        http_req_failed: ['rate==0'],
        iterations: ['count==8'],
        ...Object.fromEntries(operations.map(name => [`flow_${name}`, ['p(95)<500']])),
    },
};
function resolve(value, variables) {
    if (typeof value === 'string' && value.startsWith('$')) {
        const key = value.substring(1);
        if (!(key in variables)) fail(`Missing fixture variable ${key}`);
        return variables[key];
    }
    if (Array.isArray(value)) return value.map(item => resolve(item, variables));
    if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, resolve(v, variables)]));
    return value;
}
function uuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
        const r = Math.floor(Math.random() * 16);
        return (c === 'x' ? r : (r & 3) | 8).toString(16);
    });
}
export default function () {
    const variables = { username: __ENV.LOAD_USERNAME, password: __ENV.LOAD_PASSWORD, ...fixture.variables };
    for (const step of fixture.steps) {
        if (!/^\/api\/[A-Za-z0-9/{}_-]+$/.test(step.path)) fail('Unsafe fixture API path');
        const path = step.path.replace(/\{([A-Za-z0-9_]+)\}/g, (_, key) => {
            if (!(key in variables)) fail(`Missing path variable ${key}`);
            return encodeURIComponent(variables[key]);
        });
        const headers = { 'Content-Type': 'application/json', 'Idempotency-Key': uuid() };
        if (variables.accessToken) headers.Authorization = `Bearer ${variables.accessToken}`;
        const response = http.request(step.method || 'POST', base + path, step.body ? JSON.stringify(resolve(step.body, variables)) : null,
            { headers, redirects: 0, tags: { name: step.operation }, responseType: 'text' });
        if (timings[step.operation]) timings[step.operation].add(response.timings.duration);
        if (!check(response, { 'expected status': r => (step.statuses || [200]).includes(r.status) })) {
            let code;
            try { code = response.json('code'); } catch {}
            fail(`Flow ${step.operation} failed with HTTP ${response.status}${code ? ` (${code})` : ''}`);
        }
        for (const [variable, selector] of Object.entries(step.extract || {})) variables[variable] = response.json(selector);
    }
}

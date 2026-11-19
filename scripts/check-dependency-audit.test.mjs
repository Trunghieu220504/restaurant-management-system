import assert from 'node:assert/strict';
import test from 'node:test';
import { assessAudit } from './check-dependency-audit.mjs';

const nuget = severity => ({ version: 1, projects: [{ path: 'Api.csproj', frameworks: [{
  framework: 'net10.0', transitivePackages: severity ? [{ id: 'fixture', vulnerabilities: [{ severity }] }] : []
}] }] });
const npm = severity => ({ auditReportVersion: 2,
  metadata: { vulnerabilities: { high: severity === 'high' ? 1 : 0, critical: severity === 'critical' ? 1 : 0 } },
  vulnerabilities: severity ? { fixture: { severity } } : {} });

for (const kind of ['nuget', 'npm']) {
  const fixture = kind === 'nuget' ? nuget : npm;
  test(`${kind}: a clean report passes`, () => assert.equal(assessAudit(kind, fixture()).blocking, 0));
  test(`${kind}: High and Critical block`, () => {
    for (const severity of ['high', 'critical']) assert.equal(assessAudit(kind, fixture(severity)).blocking, 1);
  });
  test(`${kind}: moderate remains visible without blocking`, () => {
    assert.deepEqual(assessAudit(kind, fixture('moderate')), { findings: 1, blocking: 0 });
  });
  test(`${kind}: unknown severities fail closed`, () => assert.throws(() => assessAudit(kind, fixture('unknown'))));
  test(`${kind}: service errors fail closed`, () => assert.throws(() => assessAudit(kind, { ...fixture(), error: 'offline' })));
}
test('NuGet empty or incomplete projects fail closed', () => {
  assert.throws(() => assessAudit('nuget', { version: 1, projects: [] }));
  assert.throws(() => assessAudit('nuget', { version: 1, projects: [{}] }));
  assert.throws(() => assessAudit('nuget', { version: 1, projects: [{ path: 'Api.csproj', frameworks: [] }] }));
  assert.throws(() => assessAudit('nuget', { ...nuget(), logs: [{ level: 'warning' }] }));
});
test('NuGet clean vulnerability-only projects legitimately contain just their path', () => {
  assert.equal(assessAudit('nuget', { version: 1, projects: [{ path: 'Api.csproj' }] }).blocking, 0);
});
test('npm cannot hide positive counts with an empty finding list', () => {
  assert.throws(() => assessAudit('npm', { ...npm('high'), vulnerabilities: {} }));
});

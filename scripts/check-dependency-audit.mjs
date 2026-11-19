import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const severities = new Set(['low', 'moderate', 'medium', 'high', 'critical']);

export function assessAudit(kind, report) {
  if (!report || report.error || report.errors || report.logs?.length) {
    throw new Error('Audit is incomplete or contains errors; refusing a green result.');
  }
  let findings = [];
  if (kind === 'nuget') {
    if (report.version !== 1 || !Array.isArray(report.projects) || !report.projects.length) {
      throw new Error('Expected a non-empty NuGet JSON v1 solution report.');
    }
    for (const project of report.projects) {
      if (!project.path || (project.frameworks !== undefined &&
          (!Array.isArray(project.frameworks) || !project.frameworks.length))) {
        throw new Error('A project audit is malformed.');
      }
      // NuGet's vulnerability-only JSON emits just the path for clean projects.
      for (const framework of project.frameworks ?? []) {
        if (!framework.framework) throw new Error('Framework audit is malformed.');
        for (const key of ['topLevelPackages', 'transitivePackages']) {
          if (framework[key] !== undefined && !Array.isArray(framework[key])) {
            throw new Error('Package audit is malformed.');
          }
          for (const pkg of framework[key] ?? []) {
            for (const vulnerability of pkg.vulnerabilities ?? []) {
              findings.push({ package: pkg.id, severity: vulnerability.severity?.toLowerCase() });
            }
          }
        }
      }
    }
  } else if (kind === 'npm') {
    if (report.auditReportVersion !== 2 || !report.metadata?.vulnerabilities ||
        !report.vulnerabilities || typeof report.vulnerabilities !== 'object') {
      throw new Error('Expected a complete npm audit JSON v2 report.');
    }
    for (const [name, vulnerability] of Object.entries(report.vulnerabilities)) {
      findings.push({ package: name, severity: vulnerability.severity });
    }
    for (const severity of ['high', 'critical']) {
      const count = report.metadata.vulnerabilities[severity];
      if (!Number.isInteger(count) || count < 0) throw new Error('Missing npm severity counts.');
      if (count > 0 && !findings.some(f => f.severity === severity)) {
        throw new Error('npm counts and findings disagree.');
      }
    }
  } else {
    throw new Error('Use nuget or npm.');
  }
  if (findings.some(f => !f.package || !severities.has(f.severity))) {
    throw new Error('Unknown or missing package severity; refusing a green result.');
  }
  return { findings: findings.length, blocking: findings.filter(f => ['high', 'critical'].includes(f.severity)).length };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const result = assessAudit(process.argv[2], JSON.parse(readFileSync(process.argv[3], 'utf8').replace(/^\uFEFF/, '')));
    console.log(`${process.argv[2]}: ${result.findings} findings, ${result.blocking} High/Critical.`);
    if (result.blocking) process.exitCode = 1;
  } catch (error) {
    console.error(error.message);
    process.exitCode = 2;
  }
}

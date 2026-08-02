import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '../..');
const result = spawnSync(
  'dotnet',
  ['list', 'PXA.sln', 'package', '--vulnerable', '--include-transitive', '--format', 'json', '--no-restore'],
  { cwd: root, encoding: 'utf8', maxBuffer: 50 * 1024 * 1024 },
);
if (result.status !== 0) {
  process.stderr.write(result.stderr || result.stdout);
  process.exit(result.status ?? 1);
}

let report;
try {
  report = JSON.parse(result.stdout);
} catch {
  process.stderr.write(result.stdout);
  throw new Error('NuGet did not return a valid JSON vulnerability report.');
}

const findings = [];
for (const project of report.projects ?? []) {
  for (const framework of project.frameworks ?? []) {
    for (const dependencyType of ['topLevelPackages', 'transitivePackages']) {
      for (const dependency of framework[dependencyType] ?? []) {
        for (const vulnerability of dependency.vulnerabilities ?? []) {
          findings.push({
            project: project.path,
            framework: framework.framework,
            dependencyType,
            package: dependency.id,
            version: dependency.resolvedVersion,
            severity: vulnerability.severity,
            advisory: vulnerability.advisoryurl,
          });
        }
      }
    }
  }
}

if (findings.length > 0) {
  console.error(JSON.stringify({ findings }, null, 2));
  throw new Error(`${findings.length} vulnerable NuGet dependency finding(s) detected.`);
}
console.log(`NuGet vulnerability gate passed for ${report.projects?.length ?? 0} projects.`);

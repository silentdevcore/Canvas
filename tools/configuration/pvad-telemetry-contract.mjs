import { readdir, readFile, mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export async function dashboardQueryRules(repoRoot = root) {
  const directory = resolve(repoRoot, 'deploy/observability/grafana/dashboards');
  const rules = [];
  function visit(panel) {
    if (panel.datasource?.type === 'prometheus') {
      for (const target of panel.targets ?? []) {
        if (target.expr)
          rules.push({ record: `pvad_dashboard_contract_${rules.length}`, expr: target.expr });
      }
    }
    for (const child of panel.panels ?? []) visit(child);
  }
  for (const file of (await readdir(directory)).filter(file => file.endsWith('.json')).sort()) {
    const dashboard = JSON.parse(await readFile(resolve(directory, file), 'utf8'));
    for (const panel of dashboard.panels ?? []) visit(panel);
  }
  return { groups: [{ name: 'pvad-dashboard-contract', rules }] };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const output = process.argv[2];
  if (!output) throw new Error('Provide an output path for the dashboard query contract.');
  const document = await dashboardQueryRules();
  await mkdir(dirname(resolve(output)), { recursive: true });
  await writeFile(output, JSON.stringify(document, null, 2) + '\n');
  console.log(`Prepared ${document.groups[0].rules.length} PromQL queries for promtool check rules.`);
}

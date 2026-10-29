import { readFileSync, readdirSync, writeFileSync } from 'node:fs';

const rows = new Set();
function visit(value, owner, pointer = '') {
  if (!value || typeof value !== 'object') return;
  if (value.properties && typeof value.properties === 'object') {
    for (const key of Object.keys(value.properties)) rows.add(`json-property\t${owner}${pointer}\t${key}`);
  }
  for (const [key, child] of Object.entries(value)) {
    if (key === 'operationId' && typeof child === 'string') rows.add(`operation-id\t${owner}${pointer}\t${child}`);
    if (child && typeof child === 'object') visit(child, owner, `${pointer}/${key}`);
  }
}
for (const file of readdirSync('docs/schema').filter(file => file.endsWith('.json'))) {
  visit(JSON.parse(readFileSync(`docs/schema/${file}`, 'utf8')), `docs/schema/${file}`);
}
const claims = readFileSync('PVAD.WebApi/Security/PvadClaimTypes.cs', 'utf8');
for (const match of claims.matchAll(/const string (\w+) = "(pvad_[a-z_]+)"/g)) {
  rows.add(`claim\tPvadClaimTypes.${match[1]}\t${match[2]}`);
}
writeFileSync('checklists/PVAD.Serialized-Contract-Inventory.tsv',
  `kind\towner\tname\n${[...rows].sort().join('\n')}\n`);
console.log(`Recorded ${rows.size} schema/property/operation/claim entries without runtime values.`);

import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const inventory = JSON.parse(readFileSync(join(root, 'product-metadata/data-processing-inventory.json'), 'utf8'));
const requiredCoverage = ['identity', 'billing', 'documents', 'workers', 'mail', 'telemetry', 'logs', 'browserStorage', 'providers', 'regions', 'transfers', 'retention'];
const errors = [];

for (const area of requiredCoverage) {
  if (inventory.coverage?.[area] !== true) errors.push(`Required processing area is not covered: ${area}`);
}
if (inventory.productionApproved && /pending/i.test(inventory.controllerIdentity))
  errors.push('Production approval requires a verified controller identity.');

const providerIds = new Set();
for (const provider of inventory.providers ?? []) {
  if (providerIds.has(provider.id)) errors.push(`Duplicate provider id: ${provider.id}`);
  providerIds.add(provider.id);
  if (provider.transferRisk === 'conditional' && !/review|required|disabled/i.test(provider.approval))
    errors.push(`Conditional provider ${provider.id} lacks an explicit transfer review gate.`);
}

const activityIds = new Set();
const inventoriedEntities = new Set();
for (const activity of inventory.activities ?? []) {
  if (activityIds.has(activity.id)) errors.push(`Duplicate activity id: ${activity.id}`);
  activityIds.add(activity.id);
  for (const providerId of activity.providerIds ?? []) {
    if (!providerIds.has(providerId)) errors.push(`Unknown provider ${providerId} in activity ${activity.id}`);
  }
  for (const source of activity.sources ?? []) {
    if (!existsSync(join(root, source))) errors.push(`Missing source ${source} for activity ${activity.id}`);
  }
  for (const entity of activity.entities ?? []) {
    if (inventoriedEntities.has(entity)) errors.push(`Persistence entity is assigned more than once: ${entity}`);
    inventoriedEntities.add(entity);
  }
  if (!activity.retention?.rule || typeof activity.retention.legalApprovalRequired !== 'boolean' ||
      !['approved', 'pending-legal'].includes(activity.retention.approvalStatus))
    errors.push(`Activity ${activity.id} lacks a retention decision.`);
  if (activity.retention?.status === 'legal-review-required' && !activity.retention.legalApprovalRequired)
    errors.push(`Activity ${activity.id} cannot waive legal approval for an unresolved retention rule.`);
  if (activity.retention?.approvalStatus === 'approved' && activity.retention.legalApprovalRequired)
    errors.push(`Activity ${activity.id} cannot be approved while legal approval remains required.`);
}

const dbContext = readFileSync(join(root, 'src/Infrastructure/PXA.Infrastructure.Persistence/PxaDbContext.cs'), 'utf8');
const persistedEntities = [...dbContext.matchAll(/DbSet<([A-Za-z0-9_]+)>/g)].map(match => match[1]);
for (const entity of persistedEntities) {
  if (!inventoriedEntities.has(entity)) errors.push(`Persisted entity is missing from the processing inventory: ${entity}`);
}
for (const entity of inventoriedEntities) {
  if (!persistedEntities.includes(entity)) errors.push(`Inventoried persistence entity is not a PxaDbContext DbSet: ${entity}`);
}

const browserActivity = inventory.activities?.find(activity => activity.id === 'browser-storage');
if (!browserActivity?.sources.includes('product-metadata/browser-storage.json'))
  errors.push('Browser storage must reference the dedicated key-level inventory.');
if (inventory.providers?.some(provider => provider.region.toLowerCase().includes('global')))
  errors.push('A provider region may not be described as global without a reviewed transfer model.');

if (errors.length) {
  console.error(errors.map(error => `- ${error}`).join('\n'));
  process.exit(1);
}

const unresolved = inventory.activities.filter(activity => activity.retention.approvalStatus !== 'approved').length;
console.log(`Validated ${inventory.activities.length} processing activities, ${persistedEntities.length} persisted entities, and ${inventory.providers.length} providers; ${unresolved} retention decisions require legal approval.`);

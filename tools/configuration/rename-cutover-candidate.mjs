import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const roles = ['company', 'documentation', 'demo', 'designer', 'account', 'admin', 'operator', 'api', 'mcp'];

function origin(value, role) {
  try {
    const url = new URL(value);
    if (url.protocol !== 'https:' || url.username || url.password || url.pathname !== '/' || url.search || url.hash
        || /[\\\s\u0000-\u001f\u007f*]/.test(value)) throw new Error();
    return url.origin;
  } catch { throw new Error(`Invalid candidate host: ${role}.`); }
}

export function prepareCandidate(profile, version) {
  if (profile.schemaVersion !== 1 || profile.status !== 'candidate' || !/^\d+\.\d+\.\d+$/.test(version)
      || !/^[a-z0-9]+(?:[.-][a-z0-9]+)*\.[a-z]{2,}$/.test(profile.baseDomain ?? ''))
    throw new Error('Invalid cutover candidate metadata.');
  const hosts = Object.fromEntries(roles.map(role => [role, origin(profile.hosts?.[role], role)]));
  if (new Set(Object.values(hosts)).size !== roles.length) throw new Error('Candidate hosts must be distinct.');
  for (const value of Object.values(hosts)) {
    const host = new URL(value).hostname;
    if (host !== profile.baseDomain && !host.endsWith(`.${profile.baseDomain}`))
      throw new Error('Candidate host is outside the selected domain.');
  }
  for (const [key, label] of [['supportAddress', 'support'], ['senderAddress', 'sender']]) {
    const address = profile[key];
    if (typeof address !== 'string' || /[\s\u0000-\u001f\u007f]/.test(address)
        || !new RegExp(`^[a-zA-Z0-9._+-]+@${profile.baseDomain.replaceAll('.', '\\.')}$`).test(address))
      throw new Error(`Invalid candidate ${label} address.`);
  }
  const publishers = profile.publishers;
  if (!publishers || Object.keys(publishers).sort().join(',') !== 'container,npm,nuget')
    throw new Error('Candidate must declare each publisher independently.');
  for (const [kind, publisher] of Object.entries(publishers)) {
    const pattern = kind === 'npm' ? /^@[a-z0-9]+(?:[.-][a-z0-9]+)*$/
      : kind === 'container' ? /^[a-z0-9.-]+\/[a-z0-9]+(?:[._-][a-z0-9]+)*$/
      : /^[A-Za-z][A-Za-z0-9_.-]*$/;
    if (publisher !== null && (typeof publisher !== 'string' || !pattern.test(publisher)))
      throw new Error(`Invalid candidate publisher: ${kind}.`);
  }
  if (profile.webhookUrl !== null) {
    try {
      const url = new URL(profile.webhookUrl);
      if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash
          || /[\\\s\u0000-\u001f\u007f]/.test(profile.webhookUrl)) throw new Error();
    } catch { throw new Error('Invalid candidate webhook URL.'); }
  }

  const ui = roles.filter(role => !['api', 'mcp'].includes(role));
  const publicReturns = ['company', 'documentation', 'demo', 'designer', 'account'];
  const images = publishers.container === null ? null : {
    api: `${publishers.container}/pvad-webapi:${version}`,
    relay: `${publishers.container}/pvad-observability-webhook-relay:${version}`,
  };
  return {
    schemaVersion: 1,
    scope: 'Offline candidate only; this output never authorizes deployment, publishing or OS registration.',
    deploymentAuthorized: false,
    blockers: [
      ...Object.entries(publishers).filter(([, value]) => value === null).map(([kind]) => `publisher-unselected:${kind}`),
      ...(profile.webhookUrl === null ? ['webhook-target-unselected'] : []),
      'live-dns-tls-mail-key-and-ownership-validation-required', 'rollout-and-rollback-authorization-required',
    ],
    frontendEnvironment: Object.fromEntries(ui.filter(role => role !== 'admin')
      .map(role => [`VITE_PVAD_${role.toUpperCase()}_URL`, `${hosts[role]}/`])),
    designerRuntime: { __PVAD_CONFIG__: { accountUrl: `${hosts.account}/` } },
    apiSettings: {
      AllowedHosts: [...roles.map(role => new URL(hosts[role]).hostname),
        'localhost', '127.0.0.1', 'pvad-webapi', 'pxa-webapi'].join(';'),
      Cors: { AllowedOrigins: [hosts.api] },
      Registration: { AllowedReturnOrigins: publicReturns.map(role => hosts[role]) },
      DesignerAuthentication: { AllowedOrigins: [hosts.designer] },
      Mail: { Transport: 'Disabled', SenderAddress: profile.senderAddress, SenderName: 'PVADE',
        AccountBaseUrl: hosts.account, CompanyBaseUrl: hosts.company, DesignerBaseUrl: hosts.designer, AdminBaseUrl: hosts.admin,
        SupportUrl: `${hosts.company}/support.html` },
      OAuth: { Enabled: false, Issuer: hosts.account, McpResource: `${hosts.mcp}/mcp` },
    },
    hosts,
    images,
    webhookTarget: profile.webhookUrl,
    retainedBindings: ['database-name-and-owner', 'existing-volume-ids', 'secret-mounts', 'data-protection-application', 'signing-key-ids', 'oauth-client-and-resource-ids'],
  };
}

export function fileTypeDescriptors(manifest) {
  if (manifest.schemaVersion !== 1 || manifest.nativeFileOpenSupported !== false || manifest.types?.length !== 2)
    throw new Error('Invalid native file type preparation manifest.');
  for (const [index, kind] of ['pdf', 'spreadsheet'].entries()) {
    const type = manifest.types[index], suffix = kind === 'pdf' ? 'pvadp' : 'pvads';
    if (type.kind !== kind || type.extension !== `.${suffix}` || type.mediaType !== `application/vnd.pvad.${suffix}+zip`
        || type.macTypeIdentifier !== `com.pvade.pvad.${kind}-project`
        || type.windowsProgId !== `PVAD.Project.${kind === 'pdf' ? 'Pdf' : 'Spreadsheet'}`
        || !/^[\w ().-]+$/.test(type.description)) throw new Error('Invalid native file type identity.');
  }
  const mime = manifest.types.map(type => `  <mime-type type="${type.mediaType}"><comment>${type.description}</comment><sub-class-of type="application/zip"/><glob pattern="*${type.extension}"/></mime-type>`).join('\n');
  return {
    nativeFileOpenSupported: false,
    linuxSharedMimeXml: `<?xml version="1.0" encoding="UTF-8"?>\n<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">\n${mime}\n</mime-info>\n`,
    macExportedTypes: manifest.types.map(type => ({ UTTypeIdentifier: type.macTypeIdentifier,
      UTTypeDescription: type.description, UTTypeConformsTo: ['com.pkware.zip-archive'],
      UTTypeTagSpecification: { 'public.filename-extension': [type.extension.slice(1)], 'public.mime-type': type.mediaType } })),
    windowsTypes: manifest.types.map(type => ({ extension: type.extension, progId: type.windowsProgId, description: type.description })),
    installCommands: [],
    openCommands: [],
  };
}

export function canonicalComposeCandidate(document, volumeBindings) {
  const result = structuredClone(document);
  if (!result.services || !result.volumes || !volumeBindings || result.include)
    throw new Error('Expected a self-contained Compose model and explicit volume bindings.');
  const names = Object.keys(result.services);
  if (names.some(name => !/^[a-zA-Z0-9][a-zA-Z0-9_.-]*$/.test(name))) throw new Error('Invalid Compose service name.');
  const canonical = name => name.startsWith('pxa-') ? `pvad-${name.slice(4)}` : name;
  if (new Set(names.map(canonical)).size !== names.length) throw new Error('Canonical service names collide.');
  for (const name of Object.keys(volumeBindings)) {
    if (!Object.hasOwn(result.volumes, name)) throw new Error('Unknown retained volume binding.');
  }
  for (const name of Object.keys(result.volumes)) {
    const physical = volumeBindings[name];
    if (typeof physical !== 'string' || !/^[a-zA-Z0-9][a-zA-Z0-9_.-]*$/.test(physical))
      throw new Error(`Missing or invalid physical volume binding: ${name}.`);
    result.volumes[name] = { external: true, name: physical };
  }
  const services = {};
  const aliasOwners = new Map();
  for (const [name, service] of Object.entries(result.services)) {
    if (service.container_name || service.links || service.volumes_from || service.network_mode || service.extends)
      throw new Error('Compose service contains an unsupported identity binding; review it explicitly.');
    if (service.depends_on) {
      const dependencies = Array.isArray(service.depends_on) ? service.depends_on : Object.keys(service.depends_on);
      if (dependencies.some(dependency => !Object.hasOwn(result.services, dependency))) throw new Error('Unknown service dependency.');
      service.depends_on = Array.isArray(service.depends_on) ? dependencies.map(canonical)
        : Object.fromEntries(Object.entries(service.depends_on).map(([dependency, settings]) => [canonical(dependency), settings]));
    }
    const networks = Array.isArray(service.networks)
      ? Object.fromEntries(service.networks.map(network => [network, {}]))
      : service.networks ?? { default: {} };
    service.networks = Object.fromEntries(Object.entries(networks).map(([network, config]) => {
      const settings = config ?? {};
      if (typeof settings !== 'object' || Array.isArray(settings)
          || (settings.aliases !== undefined && (!Array.isArray(settings.aliases) || settings.aliases.some(alias => typeof alias !== 'string'))))
        throw new Error('Unsupported Compose network alias configuration.');
      const aliases = [...new Set([...(settings.aliases ?? []), name, canonical(name)])];
      for (const alias of aliases) {
        const key = `${network}:${alias}`;
        if (aliasOwners.has(key) && aliasOwners.get(key) !== name) throw new Error('Compose DNS aliases collide.');
        aliasOwners.set(key, name);
      }
      return [network, { ...settings, aliases }];
    }));
    services[canonical(name)] = service;
  }
  result.services = services;
  return {
    deploymentAuthorized: false,
    requirements: ['verify-actual-volume-ids-and-backup', 'stop-original-writers-before-cutover', 'validate-key-access-and-rollback'],
    compose: result,
  };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const flags = process.argv.slice(2);
    if (flags.some(flag => flag !== '--require-ready')) throw new Error('Only --require-ready is supported; no deployment commands exist.');
    const root = new URL('../../', import.meta.url);
    const plan = prepareCandidate(JSON.parse(readFileSync(new URL('deploy/rename/pvade-targets.candidate.json', root))),
      readFileSync(new URL('VERSION', root), 'utf8').trim());
    console.log(JSON.stringify({ ...plan, fileTypes: fileTypeDescriptors(JSON.parse(readFileSync(new URL('product-metadata/native-file-types.json', root)))) }, null, 2));
    if (flags.includes('--require-ready') && plan.blockers.length) process.exitCode = 2;
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}

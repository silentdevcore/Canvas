export function createNativeLicensePage(api, escapeHtml, changed) {
  let context = null;
  let epoch = 0;
  let state;
  const initialForm = () => ({ organizationId: '', customerReference: '', model: 'Subscription',
    validFrom: new Date(Date.now() + 120000).toISOString().slice(0, 19),
    cutoff: new Date(Date.now() + 365 * 86400000).toISOString().slice(0, 19),
    reasonCode: 'purchase', redistributionAllowed: false, grants: [] });
  function reset() {
    epoch++;
    state = { loaded: false, busy: false, error: '', notice: '', capabilities: null, organizations: [],
      items: [], total: 0, page: 1, organizationId: '', detail: null, form: initialForm(), retry: null };
  }
  reset();
  const e = escapeHtml;
  const date = value => value ? new Date(value).toLocaleString() : 'No end date';
  const problem = error => error.status === 403
    ? 'Manufacturer access requires a recent sign-in and explicit issuer authorization. Sign in again or contact your operator.'
    : error.message || 'Native licensing is unavailable.';
  async function work(operation) {
    if (state.busy) return;
    const token = epoch;
    state.busy = true; state.error = ''; changed();
    try { await operation(token); }
    catch (error) { if (token === epoch) state.error = problem(error); }
    finally { if (token === epoch) { state.busy = false; changed(); } }
  }
  async function list(token) {
    const result = await api.getNativeLicenses({ page: state.page, organizationId: state.organizationId });
    if (token === epoch) Object.assign(state, { items: result.items, total: result.total });
  }
  function capture(form) {
    const data = new FormData(form);
    state.form = { ...state.form, organizationId: String(data.get('organizationId') || ''),
      customerReference: String(data.get('customerReference') || ''), model: String(data.get('model')),
      validFrom: String(data.get('validFrom')), cutoff: String(data.get('cutoff') || ''),
      reasonCode: String(data.get('reasonCode')), redistributionAllowed: data.get('redistributionAllowed') === 'on',
      grants: (state.capabilities?.products || []).filter(product => data.has(`product:${product.id}`))
        .map(product => ({ product: product.id, providers: data.getAll(`provider:${product.id}`).map(String) })) };
  }
  function retryId(payload) {
    const fingerprint = JSON.stringify(payload);
    if (state.retry?.fingerprint !== fingerprint) state.retry = { fingerprint, id: crypto.randomUUID() };
    return state.retry.id;
  }
  function issueForm() {
    const form = state.form;
    const options = `${form.organizationId && !state.organizations.some(org => org.id === form.organizationId) ? `<option value="${e(form.organizationId)}" selected>${e(form.organizationId)}</option>` : ''}${state.organizations.map(org => `<option value="${e(org.id)}" ${org.id === form.organizationId ? 'selected' : ''}>${e(org.name)}</option>`).join('')}`;
    return `<section class="admin-section"><div class="admin-section-heading"><h2>Issue native license</h2></div>
      <form id="native-organization-search" class="admin-table-toolbar"><input name="search" aria-label="Search organizations" placeholder="Organization name"><button type="submit">Search organizations</button></form>
      <form id="native-license-issue" class="admin-form-stack"><fieldset ${state.busy ? 'disabled' : ''}>
        <div class="admin-subscription-fields">
          <label class="admin-field"><span>Organization</span><select name="organizationId" required><option value="">Select organization</option>${options}</select></label>
          <label class="admin-field"><span>Customer reference</span><input name="customerReference" maxlength="160" value="${e(form.customerReference)}" required></label>
          <label class="admin-field"><span>License model</span><select name="model">${['Subscription', 'Perpetual', 'Trial'].map(value => `<option ${form.model === value ? 'selected' : ''}>${value}</option>`).join('')}</select></label>
          <label class="admin-field"><span>Start (UTC)</span><input name="validFrom" type="datetime-local" step="1" value="${e(form.validFrom)}" required></label>
          ${form.model === 'Trial' ? '<p>Trial: 30 days from the signed start. Redistribution is not available.</p>' : `<label class="admin-field"><span>${form.model === 'Perpetual' ? 'Updates through' : 'Valid until'} (UTC)</span><input name="cutoff" type="datetime-local" step="1" value="${e(form.cutoff)}" required></label>`}
          <label class="admin-field"><span>Reason</span><select name="reasonCode">${['purchase', 'evaluation', 'correction'].map(value => `<option ${form.reasonCode === value ? 'selected' : ''}>${value}</option>`).join('')}</select></label>
        </div>
        <fieldset class="native-license-products"><legend>Products and providers</legend>${state.capabilities.products.map(product => {
          const selected = form.grants.find(grant => grant.product === product.id);
          return `<div><label><input name="product:${e(product.id)}" type="checkbox" ${selected ? 'checked' : ''}> ${e(product.id)}</label>
            ${product.providers.length ? `<label class="admin-field"><span>Providers for ${e(product.id)}</span><select name="provider:${e(product.id)}" multiple ${selected ? '' : 'disabled'} required size="4">${product.providers.map(provider => `<option value="${e(provider)}" ${selected?.providers.includes(provider) ? 'selected' : ''}>${e(provider)}</option>`).join('')}</select></label>` : ''}</div>`;
        }).join('')}</fieldset>
        ${form.model === 'Trial' ? '' : `<label><input name="redistributionAllowed" type="checkbox" ${form.redistributionAllowed ? 'checked' : ''}> Redistribution permitted</label>`}
        <div class="admin-form-actions"><button class="pvad-button pvad-button--primary" type="submit">${state.busy ? 'Processing...' : 'Issue signed native license'}</button></div>
      </fieldset></form></section>`;
  }
  function details() {
    if (!state.detail) return '';
    const { license, grants, customerReference, redistributionAllowed, renewedByLicenseId } = state.detail;
    const renewable = license.model !== 'Trial' && !renewedByLicenseId;
    return `<section class="admin-section" aria-label="Native license details"><div class="admin-section-heading"><h2>Native license ${e(license.id)}</h2></div>
      <dl class="admin-detail-list admin-detail-list--compact"><div><dt>Customer reference</dt><dd>${e(customerReference)}</dd></div><div><dt>Model</dt><dd>${e(license.model)}</dd></div>
      <div><dt>Redistribution</dt><dd>${redistributionAllowed ? 'Permitted' : 'Not permitted'}</dd></div><div><dt>Artifact SHA-256</dt><dd class="native-license-hash">${e(license.artifactSha256)}</dd></div>
      <div><dt>Issued</dt><dd>${e(date(license.issuedAt))}</dd></div><div><dt>Products</dt><dd>${grants.map(grant => `${e(grant.product)}${grant.providers.length ? ` (${grant.providers.map(e).join(', ')})` : ''}`).join('<br>')}</dd></div></dl>
      <div class="admin-row-actions"><a data-native href="${api.nativeLicenseDownloadUrl(license.id)}">Download</a>
      ${license.previousLicenseId ? `<button type="button" data-native-detail="${e(license.previousLicenseId)}">Previous license</button>` : ''}
      ${renewedByLicenseId ? `<button type="button" data-native-detail="${e(renewedByLicenseId)}">Renewed license</button>` : ''}</div>
      ${renewable ? `<form id="native-license-renew" class="admin-form-stack"><label class="admin-field"><span>${license.model === 'Perpetual' ? 'New update cutoff' : 'New subscription end'} (UTC)</span><input name="cutoff" type="datetime-local" step="1" required></label><button class="pvad-button pvad-button--primary" type="submit" ${state.busy ? 'disabled' : ''}>Issue renewal</button></form>` : ''}</section>`;
  }
  return {
    setContext(id) { if (context !== id) { context = id; reset(); } },
    clear() { context = null; reset(); },
    ensureLoaded() {
      if (state.loaded || state.busy) return;
      state.loaded = true;
      state.capabilities = null; state.items = []; state.detail = null;
      return work(async token => {
        const capabilities = await api.getNativeLicenseCapabilities();
        if (token !== epoch) return;
        state.capabilities = capabilities;
        if (capabilities.enabled) {
          const result = await api.getAdminOrganizations({ pageSize: 100 });
          if (token !== epoch) return;
          state.organizations = result.items;
          await list(token);
        }
        if (token === epoch) state.loaded = true;
      });
    },
    html() {
      return `<div class="native-license-workspace"><header class="admin-page-header"><div><p class="pvad-kicker">Manufacturer</p><h1>Native library licenses</h1></div><button class="pvad-button pvad-button--secondary" id="native-license-refresh" type="button" ${state.busy ? 'disabled' : ''}>Refresh</button></header>
        ${state.error ? `<div class="admin-alert admin-alert--error" role="alert">${e(state.error)} <button id="native-license-reauth" type="button" ${state.busy ? 'disabled' : ''}>Sign out and sign in</button></div>` : ''}
        ${state.notice ? `<div class="admin-alert" role="status">${e(state.notice)}</div>` : ''}
        ${!state.capabilities ? (state.error ? '' : '<p role="status">Checking manufacturer access...</p>') : !state.capabilities.enabled ? '<section class="admin-section"><h2>Manufacturer issuance disabled</h2><p>This deployment cannot issue native licenses.</p></section>' : `${issueForm()}${details()}
        <section class="admin-table-section" aria-busy="${state.busy}"><form id="native-license-filter" class="admin-table-toolbar"><label>Organization <select name="organizationId"><option value="">All organizations</option>${state.organizations.map(org => `<option value="${e(org.id)}" ${state.organizationId === org.id ? 'selected' : ''}>${e(org.name)}</option>`).join('')}</select></label><button type="submit">Filter</button></form>
        <div class="admin-table-scroll"><table><thead><tr><th>License</th><th>Model</th><th>Start</th><th>End / updates</th><th>Actions</th></tr></thead><tbody>${state.items.map(item => `<tr><td>${e(item.id)}</td><td>${e(item.model)}</td><td>${e(date(item.validFrom))}</td><td>${e(date(item.validUntil || item.updatesUntil))}</td><td><button type="button" data-native-detail="${e(item.id)}">Details</button> <a data-native href="${api.nativeLicenseDownloadUrl(item.id)}">Download</a></td></tr>`).join('') || '<tr><td colspan="5">No native licenses</td></tr>'}</tbody></table></div>
        <footer class="admin-pagination"><span>Page ${state.page} / ${Math.max(1, Math.ceil(state.total / 25))}</span><div><button id="native-license-previous" ${state.busy || state.page <= 1 ? 'disabled' : ''}>Previous</button><button id="native-license-next" ${state.busy || state.page * 25 >= state.total ? 'disabled' : ''}>Next</button></div></footer></section>`}</div>`;
    },
    bind(root = document) {
      root.querySelector('#native-license-reauth')?.addEventListener('click', () => work(async token => {
        await api.logout();
        if (token === epoch) window.dispatchEvent(new Event('pvad:session-expired'));
      }));
      root.querySelector('#native-license-refresh')?.addEventListener('click', () => { state.loaded = false; this.ensureLoaded(); });
      const issue = root.querySelector('#native-license-issue');
      issue?.addEventListener('input', () => capture(issue));
      issue?.addEventListener('change', event => { capture(issue); if (event.target.name === 'model' || event.target.name?.startsWith('product:')) changed(); });
      issue?.addEventListener('submit', event => {
        event.preventDefault(); capture(issue);
        work(async token => {
          const form = state.form;
          if (!form.grants.length) throw new Error('Select at least one product.');
          const from = new Date(`${form.validFrom}Z`).toISOString();
          const payload = { ...form, validFrom: from, validUntil: form.model === 'Perpetual' ? null : form.model === 'Trial'
            ? new Date(Date.parse(from) + 30 * 86400000).toISOString() : new Date(`${form.cutoff}Z`).toISOString(),
            updatesUntil: form.model === 'Perpetual' ? new Date(`${form.cutoff}Z`).toISOString() : null,
            redistributionAllowed: form.model !== 'Trial' && form.redistributionAllowed };
          delete payload.cutoff;
          const result = await api.issueNativeLicense({ ...payload, requestId: retryId(payload) });
          if (token !== epoch) return;
          state.retry = null; state.notice = 'Native license issued.'; state.page = 1;
          const detail = await api.getNativeLicenseDetails(result.id);
          if (token === epoch) { state.detail = detail; await list(token); }
        });
      });
      root.querySelector('#native-organization-search')?.addEventListener('submit', event => {
        event.preventDefault(); const search = String(new FormData(event.currentTarget).get('search') || '');
        work(async token => { const result = await api.getAdminOrganizations({ search, pageSize: 100 }); if (token === epoch) state.organizations = result.items; });
      });
      root.querySelector('#native-license-filter')?.addEventListener('submit', event => {
        event.preventDefault(); state.organizationId = String(new FormData(event.currentTarget).get('organizationId') || ''); state.page = 1;
        work(list);
      });
      for (const [selector, increment] of [['#native-license-previous', -1], ['#native-license-next', 1]])
        root.querySelector(selector)?.addEventListener('click', () => { state.page += increment; work(list); });
      root.querySelectorAll('[data-native-detail]').forEach(button => button.addEventListener('click', () => work(async token => {
        const result = await api.getNativeLicenseDetails(button.dataset.nativeDetail); if (token === epoch) state.detail = result;
      })));
      root.querySelector('#native-license-renew')?.addEventListener('submit', event => {
        event.preventDefault(); const cutoff = new Date(`${new FormData(event.currentTarget).get('cutoff')}Z`).toISOString();
        const license = state.detail.license;
        work(async token => {
          const payload = { validUntil: license.model === 'Subscription' ? cutoff : null, updatesUntil: license.model === 'Perpetual' ? cutoff : null, reasonCode: 'renewal' };
          const result = await api.renewNativeLicense(license.id, { ...payload, requestId: retryId({ licenseId: license.id, ...payload }) });
          if (token !== epoch) return;
          state.retry = null; state.notice = 'Renewal issued; the original artifact remains unchanged.';
          const detail = await api.getNativeLicenseDetails(result.id);
          if (token === epoch) { state.detail = detail; await list(token); }
        });
      });
    },
  };
}

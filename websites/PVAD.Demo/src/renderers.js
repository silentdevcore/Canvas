import { categories, demos, siteLinks, statusNotes } from './demoData.js';
import { getActiveDemo, getBookingState } from './state.js';
import { renderPvadFooter } from '../../shared/footer.js';
import { companyPage } from '../../shared/siteLinks.js';

function renderReference(value) {
  if (!value.startsWith('/')) return value;
  return `<a href="${value}" target="_blank" rel="noreferrer">${value}</a>`;
}

export function statusClass(status) {
  if (status === 'Ready') return 'pvad-status--ready';
  if (status === 'Preview') return 'pvad-status--preview';
  return 'pvad-status--planned';
}

function renderCategoryButtons() {
  return categories
    .map(
      (category, index) => `
        <button class="pvad-demo-filter ${index === 0 ? 'is-active' : ''}" type="button" data-category="${category}">
          ${category}
        </button>
      `,
    )
    .join('');
}

function renderTags(tags) {
  return tags.map((tag) => `<span>${tag}</span>`).join('');
}

export function renderReceiptPreview(preview) {
  return `
    <div class="pvad-demo-preview pvad-demo-preview--receipt">
      <div class="pvad-receipt">
        <div class="pvad-receipt__head">
          <div>
            <strong>Booking Receipt</strong>
            <span>${preview.reference}</span>
          </div>
          <span>${preview.date}</span>
        </div>
        <div class="pvad-receipt__customer">${preview.customer}</div>
        <table>
          <thead><tr><th>Item</th><th>Qty</th><th>Amount</th></tr></thead>
          <tbody>
            ${preview.items
              .map((item) => `<tr><td>${item.label}</td><td>${item.quantity}</td><td>${item.amount}</td></tr>`)
              .join('')}
          </tbody>
          <tfoot><tr><td colspan="2">Total</td><td>${preview.total}</td></tr></tfoot>
        </table>
      </div>
    </div>
  `;
}

function renderBookingInputs(preview) {
  return `
    <form class="pvad-demo-form" data-booking-form>
      <label>
        <span>Customer</span>
        <input name="customer" value="${preview.customer}" autocomplete="off">
      </label>
      <div class="pvad-demo-form__row">
        <label>
          <span>Reference</span>
          <input name="reference" value="${preview.reference}" autocomplete="off">
        </label>
        <label>
          <span>Date</span>
          <input name="date" type="date" value="${preview.date}">
        </label>
      </div>
      <label>
        <span>First item</span>
        <input name="item0Label" value="${preview.items[0].label}" autocomplete="off">
      </label>
      <div class="pvad-demo-form__row">
        <label>
          <span>Quantity</span>
          <input name="item0Quantity" type="number" min="1" value="${preview.items[0].quantity}">
        </label>
        <label>
          <span>Amount</span>
          <input name="item0Amount" inputmode="decimal" value="${preview.items[0].amount}">
        </label>
      </div>
    </form>
  `;
}

function renderChartPreview(preview) {
  const maxValue = Math.max(...preview.series.map((item) => item.value));

  return `
    <div class="pvad-demo-preview">
      <div class="pvad-chart-preview">
        ${preview.series
          .map(
            (item) => `
              <div class="pvad-chart-preview__bar" style="--bar-height: ${(item.value / maxValue) * 100}%">
                <span>${item.value}</span>
                <i></i>
                <b>${item.label}</b>
              </div>
            `,
          )
          .join('')}
      </div>
    </div>
  `;
}

function renderTablePreview(preview) {
  return `
    <div class="pvad-demo-preview">
      <table class="pvad-table-preview">
        <thead><tr>${preview.columns.map((column) => `<th>${column}</th>`).join('')}</tr></thead>
        <tbody>
          ${preview.rows.map((row) => `<tr>${row.map((cell) => `<td>${cell}</td>`).join('')}</tr>`).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function renderListPreview(preview, label) {
  const items = preview.sections || preview.tools || preview.steps || [];

  return `
    <div class="pvad-demo-preview">
      <div class="pvad-list-preview">
        <strong>${label}</strong>
        <ol>
          ${items.map((item) => `<li>${item}</li>`).join('')}
        </ol>
        ${preview.note ? `<p>${preview.note}</p>` : ''}
      </div>
    </div>
  `;
}

function renderDemoPreview(demo) {
  if (demo.preview.type === 'receipt') return renderReceiptPreview(getBookingState());
  if (demo.preview.type === 'chart') return renderChartPreview(demo.preview);
  if (demo.preview.type === 'table') return renderTablePreview(demo.preview);
  if (demo.preview.type === 'sheet') return renderTablePreview({
    columns: demo.preview.cells[0] ?? [],
    rows: demo.preview.cells.slice(1),
  });
  if (demo.preview.type === 'report') return renderListPreview(demo.preview, 'Mapped sections');
  if (demo.preview.type === 'viewer') return renderListPreview(demo.preview, 'Viewer tools');
  if (demo.preview.type === 'migration') return renderListPreview(demo.preview, 'Migration flow');
  return renderListPreview(demo.preview, 'Importer flow');
}

export function renderDemoCode(demo) {
  const preview = demo.id === 'booking-receipt' ? getBookingState() : demo.preview;

  return `<pre class="pvad-code"><code>${JSON.stringify(
    {
      id: demo.id,
      status: demo.status,
      source: demo.source,
      checklist: demo.checklist,
      download: demo.download,
      preview,
    },
    null,
    2,
  )}</code></pre>`;
}

function renderDemoWorkbench(demo) {
  const isBookingDemo = demo.id === 'booking-receipt';

  return `
    <div class="pvad-demo-workbench">
      <div class="pvad-demo-tabs" role="tablist" aria-label="Demo detail views">
        <button class="is-active" type="button" data-demo-tab="preview">Preview</button>
        <button type="button" data-demo-tab="input">Input</button>
        <button type="button" data-demo-tab="output">Output</button>
        <button type="button" data-demo-tab="code">Code</button>
      </div>
      <div class="pvad-demo-tab-panel is-active" data-demo-panel="preview">
        ${renderDemoPreview(demo)}
      </div>
      <div class="pvad-demo-tab-panel" data-demo-panel="input">
        ${isBookingDemo ? renderBookingInputs(getBookingState()) : `<div class="pvad-demo-preview"><p>${demo.input}</p></div>`}
      </div>
      <div class="pvad-demo-tab-panel" data-demo-panel="output">
        <div class="pvad-demo-output">
          <strong>${demo.output}</strong>
          <a class="pvad-button pvad-button--primary" href="${demo.download.startsWith('/') ? demo.download : '#source-context'}" ${demo.download.startsWith('/') ? 'target="_blank" rel="noreferrer"' : ''}>Download example</a>
        </div>
      </div>
      <div class="pvad-demo-tab-panel" data-demo-panel="code">
        ${renderDemoCode(demo)}
      </div>
    </div>
  `;
}

function renderDemoCards(items, activeDemoId) {
  return items
    .map(
      (demo) => `
        <article class="pvad-demo-card ${demo.id === activeDemoId ? 'is-active' : ''}" data-category="${demo.category}" data-search="${`${demo.title} ${demo.category} ${demo.tags.join(' ')}`.toLowerCase()}">
          <div class="pvad-demo-card__meta">
            <span class="pvad-status ${statusClass(demo.status)}">${demo.status}</span>
            <span>${demo.category}</span>
          </div>
          <h3>${demo.title}</h3>
          <p>${demo.text}</p>
          <div class="pvad-demo-tags">${renderTags(demo.tags)}</div>
          <dl class="pvad-demo-card__facts">
            <div><dt>Input</dt><dd>${demo.input}</dd></div>
            <div><dt>Output</dt><dd>${demo.output}</dd></div>
          </dl>
          <div class="pvad-demo-actions">
            <a class="pvad-button pvad-button--primary" href="#demo/${demo.id}">Open demo</a>
            <a class="pvad-button pvad-button--secondary" href="${siteLinks.documentation}">Docs</a>
            <a class="pvad-demo-source" href="${demo.source.startsWith('/') ? demo.source : '#source-context'}" ${demo.source.startsWith('/') ? 'target="_blank" rel="noreferrer"' : ''}>View source</a>
          </div>
        </article>
      `,
    )
    .join('');
}

function renderDemoDetail(demo) {
  return `
    <div class="pvad-container pvad-demo-detail">
      <div>
        <p class="pvad-kicker">Demo detail</p>
        <h2 class="pvad-heading">${demo.title}</h2>
        <p class="pvad-lede">
          ${demo.text}
        </p>
        <div class="pvad-demo-detail-facts">
          <span class="pvad-status ${statusClass(demo.status)}">${demo.status}</span>
          <span>${demo.category}</span>
          <span>${demo.checklist}</span>
        </div>
        <dl class="pvad-demo-detail-list">
          <div><dt>Input</dt><dd>${demo.input}</dd></div>
          <div><dt>Output</dt><dd>${demo.output}</dd></div>
          ${demo.inputFile ? `<div><dt>Input file</dt><dd>${renderReference(demo.inputFile)}</dd></div>` : ''}
          ${demo.outputFile ? `<div><dt>Output file</dt><dd>${renderReference(demo.outputFile)}</dd></div>` : ''}
          <div><dt>Source</dt><dd>${renderReference(demo.source)}</dd></div>
          <div><dt>Checklist</dt><dd><a href="#source-context">${demo.checklist}</a></dd></div>
          <div><dt>Download</dt><dd>${renderReference(demo.download)}</dd></div>
        </dl>
      </div>
      <div class="pvad-demo-detail__side">
        ${renderDemoWorkbench(demo)}
      </div>
    </div>
  `;
}

function renderStatusNotes(items) {
  return items
    .map(
      (item) => `
        <article class="pvad-card pvad-demo-status-card">
          <span class="pvad-status ${statusClass(item.title)}">${item.title}</span>
          <p>${item.text}</p>
        </article>
      `,
    )
    .join('');
}

export function renderApp(root) {
  const activeDemo = getActiveDemo();

  root.innerHTML = `
  <div class="pvad-site pvad-site--demo">
    <header class="pvad-site-header">
      <div class="pvad-site-header__inner">
        <a class="pvad-brand" href="/" aria-label="PVADE Demo home">
          <span class="pvad-brand__mark">PVADE</span>
          <span class="pvad-brand__name">PVADE <small>Demo</small></span>
        </a>
        <nav class="pvad-site-nav" aria-label="Primary navigation">
          <a href="${siteLinks.company}">Company</a>
          <a href="${companyPage('products')}">Products</a>
          <a href="${siteLinks.documentation}">Documentation</a>
          <a href="${siteLinks.demo}" aria-current="page">Demo</a>
          <a href="${companyPage('pricing')}">Pricing</a>
          <a href="${companyPage('about')}">About</a>
          <a href="${companyPage('support')}">Support</a>
        </nav>
        <div class="pvad-header-actions">
          <a class="pvad-button pvad-button--secondary pvad-header-cta" href="${siteLinks.designer}">Live demo</a>
          <a class="pvad-button pvad-button--primary pvad-header-cta" href="${companyPage('contact')}">Contact sales</a>
        </div>
      </div>
    </header>

    <main class="pvad-site-main" id="top">
      <div class="pvad-page-header">
        <div class="pvad-container pvad-demo-header">
          <div>
            <p class="pvad-kicker">PVADE Demo Gallery</p>
            <h1 class="pvad-heading">Explore document automation workflows in action.</h1>
            <p class="pvad-lede">
              Browse examples for generator, viewer, designer, migration, spreadsheet,
              and import/export workflows. Each demo links back to documentation and source context.
            </p>
          </div>
          <div class="pvad-card pvad-demo-summary">
            <strong>Demo status</strong>
            <span><b>1</b> ready</span>
            <span><b>5</b> preview</span>
            <span><b>2</b> planned</span>
          </div>
        </div>
      </div>

      <section class="pvad-section pvad-section--compact">
        <div class="pvad-container">
          <div class="pvad-demo-toolbar" aria-label="Demo filters">
            <input class="pvad-search" type="search" placeholder="Search demos" aria-label="Search demos" data-demo-search>
            <div class="pvad-demo-filters">
              ${renderCategoryButtons()}
            </div>
          </div>

          <div class="pvad-demo-grid pvad-demo-gallery" data-demo-gallery data-active-demo="${activeDemo.id}">
            ${renderDemoCards(demos, activeDemo.id)}
          </div>
        </div>
      </section>

      <section class="pvad-section pvad-section--soft" id="demo-detail">
        ${renderDemoDetail(activeDemo)}
      </section>

      <section class="pvad-section" id="status-model">
        <div class="pvad-container">
          <p class="pvad-kicker">Status model</p>
          <h2 class="pvad-heading">Every demo communicates readiness clearly</h2>
          <div class="pvad-demo-status-grid">
            ${renderStatusNotes(statusNotes)}
          </div>
        </div>
      </section>

      <section class="pvad-section pvad-section--soft" id="source-context">
        <div class="pvad-container pvad-demo-source-panel">
          <div>
            <p class="pvad-kicker">Source context</p>
            <h2 class="pvad-heading">Each demo should point to source, docs, and checklist status.</h2>
            <p class="pvad-lede">
              Demo cards already use direct detail links. Source downloads become real file links as hosted examples are added.
            </p>
          </div>
          <div class="pvad-card">
            <a href="${siteLinks.documentation}">Open related documentation</a>
            ${activeDemo.inputFile ? `<a href="${activeDemo.inputFile}" target="_blank" rel="noreferrer">Open active input file</a>` : ''}
            ${activeDemo.outputFile ? `<a href="${activeDemo.outputFile}" target="_blank" rel="noreferrer">Open active output file</a>` : ''}
            ${activeDemo.source.startsWith('/') ? `<a href="${activeDemo.source}" target="_blank" rel="noreferrer">Open active source file</a>` : ''}
            <a href="#demo-detail">Review demo detail pattern</a>
            <a href="#status-model">Review status model</a>
            <a href="#demo/${activeDemo.id}">Open active demo route</a>
            <a href="#top">Back to gallery</a>
          </div>
        </div>
      </section>
    </main>

    ${renderPvadFooter('PVADE Demo')}
  </div>
`;
}

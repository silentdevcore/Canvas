import { categories, demos, siteLinks, statusNotes } from './demoData.js';
import { getActiveDemo, getBookingState } from './state.js';
import { renderPxaFooter } from '../../shared/footer.js';
import { companyPage } from '../../shared/siteLinks.js';

function renderReference(value) {
  if (!value.startsWith('/')) return value;
  return `<a href="${value}" target="_blank" rel="noreferrer">${value}</a>`;
}

export function statusClass(status) {
  if (status === 'Ready') return 'pxa-status--ready';
  if (status === 'Preview') return 'pxa-status--preview';
  return 'pxa-status--planned';
}

function renderCategoryButtons() {
  return categories
    .map(
      (category, index) => `
        <button class="pxa-demo-filter ${index === 0 ? 'is-active' : ''}" type="button" data-category="${category}">
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
    <div class="pxa-demo-preview pxa-demo-preview--receipt">
      <div class="pxa-receipt">
        <div class="pxa-receipt__head">
          <div>
            <strong>Booking Receipt</strong>
            <span>${preview.reference}</span>
          </div>
          <span>${preview.date}</span>
        </div>
        <div class="pxa-receipt__customer">${preview.customer}</div>
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
    <form class="pxa-demo-form" data-booking-form>
      <label>
        <span>Customer</span>
        <input name="customer" value="${preview.customer}" autocomplete="off">
      </label>
      <div class="pxa-demo-form__row">
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
      <div class="pxa-demo-form__row">
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
    <div class="pxa-demo-preview">
      <div class="pxa-chart-preview">
        ${preview.series
          .map(
            (item) => `
              <div class="pxa-chart-preview__bar" style="--bar-height: ${(item.value / maxValue) * 100}%">
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
    <div class="pxa-demo-preview">
      <table class="pxa-table-preview">
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
    <div class="pxa-demo-preview">
      <div class="pxa-list-preview">
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
  if (demo.preview.type === 'table' || demo.preview.type === 'sheet') return renderTablePreview(demo.preview);
  if (demo.preview.type === 'report') return renderListPreview(demo.preview, 'Mapped sections');
  if (demo.preview.type === 'viewer') return renderListPreview(demo.preview, 'Viewer tools');
  if (demo.preview.type === 'migration') return renderListPreview(demo.preview, 'Migration flow');
  return renderListPreview(demo.preview, 'Importer flow');
}

export function renderDemoCode(demo) {
  const preview = demo.id === 'booking-receipt' ? getBookingState() : demo.preview;

  return `<pre class="pxa-code"><code>${JSON.stringify(
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
    <div class="pxa-demo-workbench">
      <div class="pxa-demo-tabs" role="tablist" aria-label="Demo detail views">
        <button class="is-active" type="button" data-demo-tab="preview">Preview</button>
        <button type="button" data-demo-tab="input">Input</button>
        <button type="button" data-demo-tab="output">Output</button>
        <button type="button" data-demo-tab="code">Code</button>
      </div>
      <div class="pxa-demo-tab-panel is-active" data-demo-panel="preview">
        ${renderDemoPreview(demo)}
      </div>
      <div class="pxa-demo-tab-panel" data-demo-panel="input">
        ${isBookingDemo ? renderBookingInputs(getBookingState()) : `<div class="pxa-demo-preview"><p>${demo.input}</p></div>`}
      </div>
      <div class="pxa-demo-tab-panel" data-demo-panel="output">
        <div class="pxa-demo-output">
          <strong>${demo.output}</strong>
          <a class="pxa-button pxa-button--primary" href="${demo.download.startsWith('/') ? demo.download : '#source-context'}" ${demo.download.startsWith('/') ? 'target="_blank" rel="noreferrer"' : ''}>Download example</a>
        </div>
      </div>
      <div class="pxa-demo-tab-panel" data-demo-panel="code">
        ${renderDemoCode(demo)}
      </div>
    </div>
  `;
}

function renderDemoCards(items, activeDemoId) {
  return items
    .map(
      (demo) => `
        <article class="pxa-demo-card ${demo.id === activeDemoId ? 'is-active' : ''}" data-category="${demo.category}" data-search="${`${demo.title} ${demo.category} ${demo.tags.join(' ')}`.toLowerCase()}">
          <div class="pxa-demo-card__meta">
            <span class="pxa-status ${statusClass(demo.status)}">${demo.status}</span>
            <span>${demo.category}</span>
          </div>
          <h3>${demo.title}</h3>
          <p>${demo.text}</p>
          <div class="pxa-demo-tags">${renderTags(demo.tags)}</div>
          <dl class="pxa-demo-card__facts">
            <div><dt>Input</dt><dd>${demo.input}</dd></div>
            <div><dt>Output</dt><dd>${demo.output}</dd></div>
          </dl>
          <div class="pxa-demo-actions">
            <a class="pxa-button pxa-button--primary" href="#demo/${demo.id}">Open demo</a>
            <a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}">Docs</a>
            <a class="pxa-demo-source" href="${demo.source.startsWith('/') ? demo.source : '#source-context'}" ${demo.source.startsWith('/') ? 'target="_blank" rel="noreferrer"' : ''}>View source</a>
          </div>
        </article>
      `,
    )
    .join('');
}

function renderDemoDetail(demo) {
  return `
    <div class="pxa-container pxa-demo-detail">
      <div>
        <p class="pxa-kicker">Demo detail</p>
        <h2 class="pxa-heading">${demo.title}</h2>
        <p class="pxa-lede">
          ${demo.text}
        </p>
        <div class="pxa-demo-detail-facts">
          <span class="pxa-status ${statusClass(demo.status)}">${demo.status}</span>
          <span>${demo.category}</span>
          <span>${demo.checklist}</span>
        </div>
        <dl class="pxa-demo-detail-list">
          <div><dt>Input</dt><dd>${demo.input}</dd></div>
          <div><dt>Output</dt><dd>${demo.output}</dd></div>
          ${demo.inputFile ? `<div><dt>Input file</dt><dd>${renderReference(demo.inputFile)}</dd></div>` : ''}
          ${demo.outputFile ? `<div><dt>Output file</dt><dd>${renderReference(demo.outputFile)}</dd></div>` : ''}
          <div><dt>Source</dt><dd>${renderReference(demo.source)}</dd></div>
          <div><dt>Checklist</dt><dd><a href="#source-context">${demo.checklist}</a></dd></div>
          <div><dt>Download</dt><dd>${renderReference(demo.download)}</dd></div>
        </dl>
      </div>
      <div class="pxa-demo-detail__side">
        ${renderDemoWorkbench(demo)}
      </div>
    </div>
  `;
}

function renderStatusNotes(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-demo-status-card">
          <span class="pxa-status ${statusClass(item.title)}">${item.title}</span>
          <p>${item.text}</p>
        </article>
      `,
    )
    .join('');
}

export function renderApp(root) {
  const activeDemo = getActiveDemo();

  root.innerHTML = `
  <div class="pxa-site pxa-site--demo">
    <header class="pxa-site-header">
      <div class="pxa-site-header__inner">
        <a class="pxa-brand" href="/" aria-label="PXA.Demo home">
          <span class="pxa-brand__mark">PXA</span>
          <span class="pxa-brand__name">Power Dox Automation <small>Demo</small></span>
        </a>
        <nav class="pxa-site-nav" aria-label="Primary navigation">
          <div class="pxa-nav-group">
            <a class="pxa-nav-trigger" href="${siteLinks.company}">Company</a>
            <div class="pxa-nav-submenu" aria-label="Company pages">
              <a href="${companyPage('products')}">Products</a>
              <a href="${companyPage('pricing')}">Pricing</a>
              <a href="${companyPage('about')}">About</a>
              <a href="${companyPage('support')}">Support</a>
            </div>
          </div>
          <a href="${siteLinks.documentation}">Documentation</a>
          <a href="${siteLinks.demo}" aria-current="page">Demo</a>
        </nav>
        <a class="pxa-button pxa-button--primary pxa-header-cta" href="${companyPage('contact')}">Contact sales</a>
      </div>
    </header>

    <main class="pxa-site-main" id="top">
      <div class="pxa-page-header">
        <div class="pxa-container pxa-demo-header">
          <div>
            <p class="pxa-kicker">PXA Demo Gallery</p>
            <h1 class="pxa-heading">Explore document automation workflows in action.</h1>
            <p class="pxa-lede">
              Browse examples for generator, viewer, designer, migration, spreadsheet,
              and import/export workflows. Each demo links back to documentation and source context.
            </p>
          </div>
          <div class="pxa-card pxa-demo-summary">
            <strong>Demo status</strong>
            <span><b>1</b> ready</span>
            <span><b>5</b> preview</span>
            <span><b>2</b> planned</span>
          </div>
        </div>
      </div>

      <section class="pxa-section pxa-section--compact">
        <div class="pxa-container">
          <div class="pxa-demo-toolbar" aria-label="Demo filters">
            <input class="pxa-search" type="search" placeholder="Search demos" aria-label="Search demos" data-demo-search>
            <div class="pxa-demo-filters">
              ${renderCategoryButtons()}
            </div>
          </div>

          <div class="pxa-demo-grid pxa-demo-gallery" data-demo-gallery data-active-demo="${activeDemo.id}">
            ${renderDemoCards(demos, activeDemo.id)}
          </div>
        </div>
      </section>

      <section class="pxa-section pxa-section--soft" id="demo-detail">
        ${renderDemoDetail(activeDemo)}
      </section>

      <section class="pxa-section" id="status-model">
        <div class="pxa-container">
          <p class="pxa-kicker">Status model</p>
          <h2 class="pxa-heading">Every demo communicates readiness clearly</h2>
          <div class="pxa-demo-status-grid">
            ${renderStatusNotes(statusNotes)}
          </div>
        </div>
      </section>

      <section class="pxa-section pxa-section--soft" id="source-context">
        <div class="pxa-container pxa-demo-source-panel">
          <div>
            <p class="pxa-kicker">Source context</p>
            <h2 class="pxa-heading">Each demo should point to source, docs, and checklist status.</h2>
            <p class="pxa-lede">
              Demo cards already use direct detail links. Source downloads become real file links as hosted examples are added.
            </p>
          </div>
          <div class="pxa-card">
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

    ${renderPxaFooter('PXA.Demo')}
  </div>
`;
}

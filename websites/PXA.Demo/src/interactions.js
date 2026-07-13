import { getActiveDemo, updateBookingState } from './state.js';
import { renderDemoCode, renderReceiptPreview } from './renderers.js';

function applyFilters() {
  const gallery = document.querySelector('[data-demo-gallery]');
  const search = document.querySelector('[data-demo-search]');
  const activeCategory = document.querySelector('.pxa-demo-filter.is-active')?.dataset.category || 'All';
  const query = search.value.trim().toLowerCase();

  Array.from(gallery.children).forEach((card) => {
    const matchesCategory = activeCategory === 'All' || card.dataset.category === activeCategory;
    const matchesSearch = !query || card.dataset.search.includes(query);
    card.hidden = !(matchesCategory && matchesSearch);
  });
}

function bindDemoTabs() {
  const tabs = Array.from(document.querySelectorAll('[data-demo-tab]'));
  const panels = Array.from(document.querySelectorAll('[data-demo-panel]'));

  tabs.forEach((tab) => {
    tab.addEventListener('click', () => {
      tabs.forEach((item) => item.classList.remove('is-active'));
      panels.forEach((panel) => panel.classList.remove('is-active'));
      tab.classList.add('is-active');
      document.querySelector(`[data-demo-panel="${tab.dataset.demoTab}"]`)?.classList.add('is-active');
    });
  });
}

function bindBookingForm() {
  const form = document.querySelector('[data-booking-form]');
  if (!form) return;

  form.addEventListener('input', () => {
    const formData = new FormData(form);
    const bookingState = updateBookingState((currentState) => ({
      ...currentState,
      customer: formData.get('customer'),
      reference: formData.get('reference'),
      date: formData.get('date'),
      items: currentState.items.map((item, index) => {
        if (index !== 0) return item;
        return {
          label: formData.get('item0Label'),
          quantity: Number(formData.get('item0Quantity')) || 1,
          amount: formData.get('item0Amount'),
        };
      }),
    }));

    document.querySelector('[data-demo-panel="preview"]').innerHTML = renderReceiptPreview(bookingState);
    document.querySelector('[data-demo-panel="code"]').innerHTML = renderDemoCode(getActiveDemo());
  });
}

export function bindDemoInteractions() {
  const search = document.querySelector('[data-demo-search]');
  const filters = Array.from(document.querySelectorAll('[data-category]'));

  filters.forEach((button) => {
    button.addEventListener('click', () => {
      filters.forEach((item) => item.classList.remove('is-active'));
      button.classList.add('is-active');
      applyFilters();
    });
  });

  search.addEventListener('input', applyFilters);
  bindDemoTabs();
  bindBookingForm();
}

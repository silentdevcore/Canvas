const apiBase = '/api/pxa/v1/admin/operator/documentation';
const navigation = document.querySelector('#runbook-navigation');
const content = document.querySelector('#runbook-content');

function appendTextElement(parent, tag, text, className) {
  const element = document.createElement(tag);
  element.textContent = text;
  if (className) element.className = className;
  parent.append(element);
  return element;
}

function renderMarkdown(markdown) {
  content.replaceChildren();
  let list = null;
  let code = null;

  for (const rawLine of markdown.replaceAll('\r\n', '\n').split('\n')) {
    const line = rawLine.trimEnd();
    if (line.startsWith('```')) {
      if (code) {
        content.append(code);
        code = null;
      } else {
        code = document.createElement('pre');
      }
      list = null;
      continue;
    }
    if (code) {
      code.textContent += `${rawLine}\n`;
      continue;
    }
    if (!line.trim()) {
      list = null;
      continue;
    }
    const heading = /^(#{1,3})\s+(.+)$/.exec(line);
    if (heading) {
      appendTextElement(content, `h${heading[1].length}`, heading[2]);
      list = null;
      continue;
    }
    if (line.startsWith('> ')) {
      appendTextElement(content, 'aside', line.slice(2), 'operator-warning');
      list = null;
      continue;
    }
    if (/^[-*]\s+/.test(line)) {
      if (!list) {
        list = document.createElement('ul');
        content.append(list);
      }
      appendTextElement(list, 'li', line.replace(/^[-*]\s+/, ''));
      continue;
    }
    if (/^\d+\.\s+/.test(line)) {
      if (!list || list.tagName !== 'OL') {
        list = document.createElement('ol');
        content.append(list);
      }
      appendTextElement(list, 'li', line.replace(/^\d+\.\s+/, ''));
      continue;
    }
    appendTextElement(content, 'p', line);
    list = null;
  }
}

async function request(url) {
  const response = await fetch(url, {
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
    cache: 'no-store',
  });
  if (response.status === 401) {
    window.location.assign(`/login?returnUrl=${encodeURIComponent(window.location.href)}`);
    throw new Error('Authentication required.');
  }
  if (response.status === 403)
    throw new Error('This account is not an authorized system operator.');
  if (!response.ok)
    throw new Error('Protected operator documentation is unavailable.');
  return response.json();
}

async function openRunbook(item, button) {
  for (const candidate of navigation.querySelectorAll('button'))
    candidate.removeAttribute('aria-current');
  button.setAttribute('aria-current', 'page');
  content.replaceChildren();
  appendTextElement(content, 'p', 'Loading runbook...', 'operator-status');
  try {
    const runbook = await request(item.href);
    renderMarkdown(runbook.markdown);
    content.focus();
    history.replaceState(null, '', `/documentation/#${encodeURIComponent(item.slug)}`);
  } catch (error) {
    content.replaceChildren();
    appendTextElement(content, 'h2', 'Runbook unavailable');
    appendTextElement(content, 'p', error instanceof Error ? error.message : String(error), 'operator-error');
  }
}

async function start() {
  try {
    const catalog = await request(apiBase);
    navigation.replaceChildren();
    if (!catalog.documents.length)
      throw new Error('No protected runbooks are installed.');
    const requested = decodeURIComponent(window.location.hash.slice(1));
    let initial = null;
    for (const item of catalog.documents) {
      const button = document.createElement('button');
      button.type = 'button';
      appendTextElement(button, 'strong', item.title);
      appendTextElement(button, 'span', item.summary);
      button.addEventListener('click', () => openRunbook(item, button));
      navigation.append(button);
      if (item.slug === requested || (!initial && !requested)) initial = [item, button];
    }
    initial ??= [catalog.documents[0], navigation.querySelector('button')];
    await openRunbook(initial[0], initial[1]);
  } catch (error) {
    navigation.replaceChildren();
    content.replaceChildren();
    appendTextElement(content, 'h1', 'Operator documentation unavailable');
    appendTextElement(content, 'p', error instanceof Error ? error.message : String(error), 'operator-error');
  }
}

start();

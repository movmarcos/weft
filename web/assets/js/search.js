// Weft docs — client-side search. No dependencies. Vanilla ES module.
// Fetches /search-index.json at load time, binds to .strip-search inputs,
// renders a dropdown with up to 8 results, supports keyboard navigation.

const MAX_RESULTS = 8;
const DEBOUNCE_MS = 150;

let entries = [];
let activeInput = null;
let dropdown = null;
let selectedIndex = -1;

// ---- fetch index --------------------------------------------------------

async function loadIndex() {
  try {
    const res = await fetch('/search-index.json');
    if (!res.ok) return;
    entries = await res.json();
  } catch {
    // silently skip — search just won't work
  }
}

// ---- search logic -------------------------------------------------------

function search(query) {
  if (!query || query.trim().length < 2) return [];
  const tokens = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
  const results = [];
  for (const e of entries) {
    const haystack = [
      e.title,
      e.eyebrow,
      ...(e.headings || []),
      e.excerpt,
    ].join(' ').toLowerCase();
    if (tokens.every(t => haystack.includes(t))) {
      results.push(e);
    }
    if (results.length >= MAX_RESULTS) break;
  }
  return results;
}

function firstMatchingHeading(e, tokens) {
  if (!e.headings) return '';
  for (const h of e.headings) {
    if (tokens.some(t => h.toLowerCase().includes(t))) return h;
  }
  return e.headings[0] || '';
}

// ---- dropdown rendering -------------------------------------------------

function createDropdown() {
  const el = document.createElement('div');
  el.className = 'search-dropdown';
  el.setAttribute('role', 'listbox');
  el.setAttribute('aria-label', 'Search results');
  document.body.appendChild(el);
  return el;
}

function positionDropdown(input) {
  const rect = input.getBoundingClientRect();
  dropdown.style.top  = `${rect.bottom + window.scrollY}px`;
  dropdown.style.left = `${rect.left + window.scrollX}px`;
  dropdown.style.width = `${Math.max(rect.width, 320)}px`;
}

function renderResults(results, tokens) {
  if (!dropdown) return;
  if (!results.length) {
    dropdown.innerHTML = '<div class="search-no-results">No threads found.</div>';
    dropdown.classList.add('open');
    return;
  }
  dropdown.innerHTML = results.map((e, i) => {
    const heading = firstMatchingHeading(e, tokens);
    const headingHtml = heading
      ? `<span class="search-result-heading">${escHtml(heading)}</span>`
      : '';
    return `<a class="search-result-item" role="option" aria-selected="false"
        href="${escHtml(e.url)}" data-index="${i}">
      <span class="search-result-eyebrow">${escHtml(e.eyebrow || '')}</span>
      <span class="search-result-title">${escHtml(e.title || '')}</span>
      ${headingHtml}
    </a>`;
  }).join('');
  selectedIndex = -1;
  dropdown.classList.add('open');
}

function closeDropdown() {
  if (!dropdown) return;
  dropdown.classList.remove('open');
  dropdown.innerHTML = '';
  selectedIndex = -1;
}

function escHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// ---- keyboard navigation ------------------------------------------------

function moveSelection(dir) {
  if (!dropdown) return;
  const items = dropdown.querySelectorAll('.search-result-item');
  if (!items.length) return;
  if (selectedIndex >= 0) items[selectedIndex].setAttribute('aria-selected', 'false');
  selectedIndex = Math.max(0, Math.min(items.length - 1, selectedIndex + dir));
  items[selectedIndex].setAttribute('aria-selected', 'true');
  items[selectedIndex].scrollIntoView({ block: 'nearest' });
}

function openSelected() {
  if (!dropdown || selectedIndex < 0) return;
  const item = dropdown.querySelectorAll('.search-result-item')[selectedIndex];
  if (item) item.click();
}

// ---- debounce -----------------------------------------------------------

function debounce(fn, ms) {
  let t;
  return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
}

// ---- wire inputs --------------------------------------------------------

function wireInput(input) {
  if (input._searchWired) return;
  input._searchWired = true;

  const onInput = debounce(() => {
    const q = input.value;
    if (!q || q.trim().length < 2) { closeDropdown(); return; }
    positionDropdown(input);
    const tokens = q.trim().toLowerCase().split(/\s+/).filter(Boolean);
    renderResults(search(q), tokens);
  }, DEBOUNCE_MS);

  input.addEventListener('input', onInput);

  input.addEventListener('focus', () => {
    activeInput = input;
    if (input.value.trim().length >= 2) {
      positionDropdown(input);
      const tokens = input.value.trim().toLowerCase().split(/\s+/).filter(Boolean);
      renderResults(search(input.value), tokens);
    }
  });

  input.addEventListener('keydown', (e) => {
    if (!dropdown || !dropdown.classList.contains('open')) return;
    if (e.key === 'ArrowDown')  { e.preventDefault(); moveSelection(1);  }
    else if (e.key === 'ArrowUp')   { e.preventDefault(); moveSelection(-1); }
    else if (e.key === 'Enter')     { e.preventDefault(); openSelected();    }
    else if (e.key === 'Escape')    { closeDropdown(); input.blur();          }
  });
}

// ---- init ---------------------------------------------------------------

document.addEventListener('DOMContentLoaded', async () => {
  await loadIndex();

  dropdown = createDropdown();

  // Delegate clicks inside dropdown
  dropdown.addEventListener('click', (e) => {
    const item = e.target.closest('.search-result-item');
    if (item && item.href) {
      e.preventDefault();
      closeDropdown();
      window.location.href = item.href;
    }
  });

  // Close when clicking outside
  document.addEventListener('click', (e) => {
    if (!dropdown) return;
    if (!dropdown.contains(e.target) && e.target !== activeInput) {
      closeDropdown();
    }
  });

  // Wire all existing inputs
  document.querySelectorAll('.strip-search').forEach(wireInput);

  // Reposition on scroll / resize
  window.addEventListener('scroll', () => {
    if (activeInput && dropdown && dropdown.classList.contains('open')) {
      positionDropdown(activeInput);
    }
  }, { passive: true });
  window.addEventListener('resize', () => {
    if (activeInput && dropdown && dropdown.classList.contains('open')) {
      positionDropdown(activeInput);
    }
  }, { passive: true });
});

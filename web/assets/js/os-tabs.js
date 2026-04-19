// os-tabs.js — sync bash/powershell choice across all tab groups on the page,
// persist via localStorage so the user picks once and every doc remembers.

(function () {
  const KEY = 'weft-os-pref';
  const DEFAULT = 'bash';

  function applyPref(os) {
    document.querySelectorAll('.os-tabs').forEach(container => {
      const hasOs = container.querySelector(`.os-tab-btn[data-os="${os}"]`);
      // If this tab group doesn't have the preferred OS, leave its current state alone.
      if (!hasOs) return;
      container.querySelectorAll('.os-tab-btn').forEach(btn =>
        btn.classList.toggle('active', btn.dataset.os === os));
      container.querySelectorAll('.os-tab-panel').forEach(panel =>
        panel.classList.toggle('active', panel.dataset.os === os));
    });
  }

  document.addEventListener('click', e => {
    const btn = e.target.closest('.os-tab-btn');
    if (!btn) return;
    const os = btn.dataset.os;
    if (!os) return;
    try { localStorage.setItem(KEY, os); } catch { /* private mode */ }
    applyPref(os);
  });

  function init() {
    let pref = DEFAULT;
    try { pref = localStorage.getItem(KEY) || DEFAULT; } catch { /* private mode */ }
    applyPref(pref);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();

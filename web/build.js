// Weft docs website — Pass 2 build script.
//
// Reads nav.json + tiles.json + cli.json + exit-codes.json. Converts each
// markdown doc to HTML via marked (Prism-highlighted code fences) and stitches
// them into templates with partials expanded. Generates:
//   - /index.html                    (homepage with loom)
//   - /docs/<slug>.html              (eight markdown-driven doc pages)
//   - /cli/index.html                (synthesized CLI reference)
//   - /samples/index.html            (samples landing grid)
//   - /samples/<slug>/index.html     (per-sample page from sample README.md)
//   - /exit-codes/index.html         (color-coded exit-codes table)
import { readFile, writeFile, mkdir, readdir, copyFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { dirname, join, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { marked } from 'marked';
import matter from 'gray-matter';
import Prism from 'prismjs';
import loadLanguages from 'prismjs/components/index.js';

// Preload the languages we know show up in Weft docs.
loadLanguages(['bash', 'yaml', 'json', 'powershell', 'csharp', 'xml-doc', 'markup']);

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = __dirname;
const REPO = resolve(ROOT, '..');
const DIST = join(ROOT, 'dist');
const DOCS_DIST = join(DIST, 'docs');
const TEMPLATES = join(ROOT, 'templates');
const PARTIALS = join(TEMPLATES, 'partials');
const ASSETS_SRC = join(ROOT, 'assets');
const ASSETS_DIST = join(DIST, 'assets');
const DATA = join(ROOT, 'data');

const VERSION = 'v1.0.0';

// --- markdown rendering --------------------------------------------------

let currentH2s = [];

function htmlEscape(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function slugify(text) {
  return String(text).toLowerCase().replace(/[^\w]+/g, '-').replace(/^-|-$/g, '');
}

const renderer = new marked.Renderer();
renderer.code = (code, infostring) => {
  const lang = (infostring || '').trim().split(/\s+/)[0];
  if (lang && Prism.languages[lang]) {
    const highlighted = Prism.highlight(code, Prism.languages[lang], lang);
    return `<pre class="code-block language-${lang}" data-lang="${lang}"><code class="language-${lang}">${highlighted}</code></pre>`;
  }
  const langAttr = lang ? ` data-lang="${lang}"` : '';
  const classAttr = lang ? ` language-${lang}` : '';
  return `<pre class="code-block${classAttr}"${langAttr}><code>${htmlEscape(code)}</code></pre>`;
};
renderer.heading = (text, level, raw) => {
  const slug = slugify(raw);
  if (level === 2) currentH2s.push({ text, slug });
  return `<h${level} id="${slug}">${text}</h${level}>`;
};
// Transform GitHub-style admonitions: blockquotes starting with [!TYPE]
const originalBlockquote = renderer.blockquote.bind(renderer);
renderer.blockquote = (quote) => {
  // quote already has <p> wrappers from marked.
  const m = quote.match(/^\s*<p>\s*\[!(WARNING|TIP|NOTE|CAUTION|IMPORTANT)\]\s*(<br\s*\/?>)?\s*([\s\S]*)/i);
  if (m) {
    const kind = m[1].toLowerCase();
    const rest = m[3].replace(/<\/p>\s*$/, '</p>');
    const labelMap = {
      warning: 'Warning', caution: 'Caution', important: 'Important',
      tip: 'Tip', note: 'Note',
    };
    return `<div class="callout callout-${kind}"><div class="callout-label">${labelMap[kind] || kind}</div>${rest}</div>`;
  }
  return originalBlockquote(quote);
};
marked.setOptions({ renderer });

// --- helpers -------------------------------------------------------------

async function ensureDir(p) {
  if (!existsSync(p)) await mkdir(p, { recursive: true });
}

async function copyDir(src, dst) {
  await ensureDir(dst);
  const entries = await readdir(src, { withFileTypes: true });
  for (const entry of entries) {
    const s = join(src, entry.name);
    const d = join(dst, entry.name);
    if (entry.isDirectory()) await copyDir(s, d);
    else await copyFile(s, d);
  }
}

function fillTemplate(tpl, tokens) {
  let out = tpl;
  for (const [k, v] of Object.entries(tokens)) {
    out = out.split(`{{${k}}}`).join(v ?? '');
  }
  return out;
}

// Strip the first `<h1>...</h1>` tag (with possible newlines) from rendered HTML.
function stripLeadingH1(html) {
  return html.replace(/^\s*<h1[^>]*>[\s\S]*?<\/h1>\s*/, '');
}

// Mark the first remaining <p> as the lede.
function applyLedeClass(html) {
  return html.replace(/<p>/, '<p class="lede">');
}

function renderWarpToc(h2s, activeSlug = null) {
  if (!h2s || !h2s.length) {
    return '<div class="warp-toc-empty">No sub-sections on this page.</div>';
  }
  return h2s.map(h => {
    const active = activeSlug && activeSlug === h.slug ? ' active' : '';
    return `<a class="warp-thread${active}" href="#${h.slug}"><span>${h.text}</span></a>`;
  }).join('\n    ');
}

function renderRelated(relatedSlugs, slugToMeta) {
  if (!relatedSlugs || !relatedSlugs.length) {
    return '<div class="related-empty">No related threads yet.</div>';
  }
  return relatedSlugs.map(s => {
    const m = slugToMeta.get(s);
    if (!m) {
      return `<a class="mini-tile gold" href="/docs/${s}.html">
      <div class="mini-tile-icon">?</div>
      <div class="mini-tile-text"><strong>${s}</strong><span>Related doc</span></div>
    </a>`;
    }
    const color = m.color || 'gold';
    const icon = m.icon || '·';
    const blurb = m.eyebrow || '';
    return `<a class="mini-tile ${color}" href="${m.href}">
      <div class="mini-tile-icon">${icon}</div>
      <div class="mini-tile-text"><strong>${htmlEscape(m.title)}</strong><span>${htmlEscape(blurb)}</span></div>
    </a>`;
  }).join('\n    ');
}

function renderPrevNext(index, items, hrefKey = 'href') {
  const prev = index > 0 ? items[index - 1] : null;
  const next = index < items.length - 1 ? items[index + 1] : null;
  const prevLink = prev
    ? `<a class="nav-arrow" href="${prev[hrefKey]}" rel="prev">
      <div class="nav-arrow-label">&larr; Previous on the weft</div>
      <div class="nav-arrow-title">${htmlEscape(prev.title)}</div>
    </a>`
    : '<span class="nav-arrow disabled"></span>';
  const nextLink = next
    ? `<a class="nav-arrow next" href="${next[hrefKey]}" rel="next">
      <div class="nav-arrow-label">Next on the weft &rarr;</div>
      <div class="nav-arrow-title">${htmlEscape(next.title)}</div>
    </a>`
    : '<span class="nav-arrow disabled"></span>';
  return { prevLink, nextLink };
}

// --- partials ------------------------------------------------------------

async function loadPartials() {
  const strip   = await readFile(join(PARTIALS, 'strip.html'), 'utf8');
  const weftBar = await readFile(join(PARTIALS, 'weft-bar.html'), 'utf8');
  const footer  = await readFile(join(PARTIALS, 'footer.html'), 'utf8');
  return { strip, weftBar, footer };
}

function buildStrip(partial, { currentNav, right }) {
  const current = k => (currentNav === k ? ' class="current"' : '');
  return fillTemplate(partial, {
    NAV_DOCS_CURRENT:    current('docs'),
    NAV_CLI_CURRENT:     current('cli'),
    NAV_SAMPLES_CURRENT: current('samples'),
    STRIP_RIGHT: right ?? `<div class="strip-version">${VERSION}</div>`,
  });
}

function buildWeftBar(partial, navDocs, currentSlug) {
  const n = navDocs.length;
  // Distribute knots across 5% .. 95%.
  const first = 5, last = 95;
  const step = n > 1 ? (last - first) / (n - 1) : 0;
  const knots = navDocs.map((d, i) => {
    const left = first + step * i;
    const name = d.shortTitle || d.title;
    const cls = d.slug === currentSlug ? 'knot current' : 'knot';
    return `<a class="${cls}" style="left: ${left.toFixed(1)}%;" href="/docs/${d.slug}.html" data-name="${htmlEscape(name)}"></a>`;
  }).join('\n    ');
  return fillTemplate(partial, { KNOTS: knots });
}

// --- loom rendering ------------------------------------------------------

function renderLoomThreads(tilesData) {
  const warpClasses = ['warp', 'warp-soft', 'warp', 'warp-soft', 'warp', 'warp-soft', 'warp'];
  const warp = tilesData.colPercents.map((p, i) =>
    `<div class="${warpClasses[i]}" style="left: ${p}%;"></div>`
  ).join('\n      ');
  const weftClasses = ['weft-thread', 'weft-thread soft', 'weft-thread', 'weft-thread soft'];
  const weft = tilesData.rowPercents.map((p, i) =>
    `<div class="${weftClasses[i]}" style="top: ${p}%;"></div>`
  ).join('\n      ');
  return { warp, weft };
}

function renderLoomTiles(tilesData, slugToHref) {
  return tilesData.tiles.map(t => {
    const col = tilesData.colPercents[t.col - 1];
    const row = tilesData.rowPercents[t.row - 1];
    const href = slugToHref.get(t.slug)
      || (t.slug === 'architecture'
        ? 'https://github.com/marcosmagri/PowerBIAutomationDeploy/blob/master/docs/superpowers/specs/2026-04-18-weft-docs-website-design.md'
        : `/docs/${t.slug}.html`);
    return `<a class="tile ${t.color}" href="${href}" style="left: ${col}%; top: ${row}%;">
        <div class="tile-inner">
          <div class="tile-icon">${htmlEscape(t.icon)}</div>
          <div class="tile-title">${htmlEscape(t.label)}</div>
        </div>
      </a>`;
  }).join('\n      ');
}

// --- main ----------------------------------------------------------------

async function build() {
  await ensureDir(DIST);
  await ensureDir(DOCS_DIST);

  const nav   = JSON.parse(await readFile(join(DATA, 'nav.json'), 'utf8'));
  const tiles = JSON.parse(await readFile(join(DATA, 'tiles.json'), 'utf8'));
  const cli   = JSON.parse(await readFile(join(DATA, 'cli.json'), 'utf8'));
  const ecs   = JSON.parse(await readFile(join(DATA, 'exit-codes.json'), 'utf8'));

  const partials = await loadPartials();

  const docTemplate     = await readFile(join(TEMPLATES, 'doc.html'), 'utf8');
  const homeTemplate    = await readFile(join(TEMPLATES, 'homepage.html'), 'utf8');
  const cliTemplate     = await readFile(join(TEMPLATES, 'cli.html'), 'utf8');
  const samplesTemplate = await readFile(join(TEMPLATES, 'samples.html'), 'utf8');
  const sampleTemplate  = await readFile(join(TEMPLATES, 'sample.html'), 'utf8');
  const exitCodesTpl    = await readFile(join(TEMPLATES, 'exit-codes.html'), 'utf8');

  // --- parse docs (first pass for nav-lookup metadata) ----------------
  const parsed = [];
  for (const entry of nav.docs) {
    const abs = resolve(ROOT, entry.file);
    const raw = await readFile(abs, 'utf8');
    const { data, content } = matter(raw);
    parsed.push({ slug: entry.slug, frontmatter: data, body: content });
  }
  // Sort by frontmatter `order` (matches the lifecycle order on the weft bar).
  parsed.sort((a, b) => (a.frontmatter.order ?? 999) - (b.frontmatter.order ?? 999));

  const slugToMeta = new Map();
  for (const p of parsed) {
    slugToMeta.set(p.slug, {
      slug: p.slug,
      title: p.frontmatter.title || p.slug,
      shortTitle: p.frontmatter.shortTitle || (p.frontmatter.title || p.slug).split(/[—–-]/)[0].trim(),
      eyebrow: p.frontmatter.eyebrow || '',
      color: p.frontmatter.color || 'gold',
      icon: p.frontmatter.icon || '·',
      href: `/docs/${p.slug}.html`,
    });
  }
  // Manufactured synthetic docs that still need to appear on the weft bar
  // / related-rail if any markdown doc references them.
  slugToMeta.set('cli',        { slug: 'cli',        title: 'CLI reference', shortTitle: 'CLI',         eyebrow: 'CLI · Reference',       color: 'green', icon: '⌘', href: '/cli/'       });
  slugToMeta.set('samples',    { slug: 'samples',    title: 'Samples',       shortTitle: 'Samples',     eyebrow: 'Samples · End-to-end',  color: 'gold',  icon: '⊟', href: '/samples/'   });
  slugToMeta.set('exit-codes', { slug: 'exit-codes', title: 'Exit codes',    shortTitle: 'Exit codes',  eyebrow: 'Exit codes · Reference', color: 'gold', icon: '№', href: '/exit-codes/' });
  slugToMeta.set('architecture', { slug: 'architecture', title: 'Architecture', shortTitle: 'Architecture', eyebrow: 'Architecture · Design',  color: 'green', icon: '⊜', href: 'https://github.com/marcosmagri/PowerBIAutomationDeploy/blob/master/docs/superpowers/specs/2026-04-18-weft-docs-website-design.md' });

  const slugToHref = new Map(Array.from(slugToMeta.entries(), ([k, v]) => [k, v.href]));

  // --- doc pages ------------------------------------------------------
  const docsForNav = parsed.map(p => ({
    slug: p.slug,
    title: p.frontmatter.title || p.slug,
    shortTitle: slugToMeta.get(p.slug).shortTitle,
  }));

  let writtenCount = 0;

  for (let i = 0; i < parsed.length; i++) {
    const { slug, frontmatter, body } = parsed[i];
    currentH2s = [];
    let html = marked.parse(body);
    html = stripLeadingH1(html);
    html = applyLedeClass(html);

    const { prevLink, nextLink } = renderPrevNext(
      i,
      docsForNav.map(d => ({ ...d, href: `/docs/${d.slug}.html` })),
    );

    const strip   = buildStrip(partials.strip, {
      currentNav: 'docs',
      right: `<input class="strip-search" placeholder="search the loom...">`,
    });
    const weftBar = buildWeftBar(partials.weftBar, docsForNav, slug);
    const footer  = partials.footer;

    const rendered = fillTemplate(docTemplate, {
      TITLE:    frontmatter.title || slug,
      EYEBROW:  frontmatter.eyebrow || '',
      COLOR:    frontmatter.color || '',
      ICON:     frontmatter.icon || '',
      CONTENT:  html,
      STRIP:    strip,
      WEFT_BAR: weftBar,
      FOOTER:   footer,
      WARP_TOC: renderWarpToc(currentH2s),
      RELATED:  renderRelated(frontmatter.related, slugToMeta),
      PREV_LINK: prevLink,
      NEXT_LINK: nextLink,
    });

    const out = join(DOCS_DIST, `${slug}.html`);
    await writeFile(out, rendered, 'utf8');
    writtenCount++;
    console.log(`wrote ${relative(ROOT, out)}`);
  }

  // --- homepage -------------------------------------------------------
  {
    const { warp, weft } = renderLoomThreads(tiles);
    const tilesHtml = renderLoomTiles(tiles, slugToHref);
    const strip = buildStrip(partials.strip, { currentNav: null });
    const homeRendered = fillTemplate(homeTemplate, {
      STRIP: strip,
      FOOTER: partials.footer,
      WARP_THREADS: warp,
      WEFT_THREADS: weft,
      TILES: tilesHtml,
    });
    const out = join(DIST, 'index.html');
    await writeFile(out, homeRendered, 'utf8');
    writtenCount++;
    console.log(`wrote ${relative(ROOT, out)}`);
  }

  // --- CLI reference --------------------------------------------------
  {
    const strip   = buildStrip(partials.strip, {
      currentNav: 'cli',
      right: `<input class="strip-search" placeholder="search the loom...">`,
    });
    const weftBar = buildWeftBar(partials.weftBar, docsForNav, null);

    const h2s = cli.commands.map(c => ({ text: `weft ${c.name}`, slug: `cmd-${c.slug}` }));
    const warpToc = renderWarpToc(h2s);

    const cmdsHtml = cli.commands.map(c => {
      const opts = (c.options || []).map(o =>
        `<tr><td>${htmlEscape(o.flag)}</td><td>${htmlEscape(o.desc)}${o.required ? ' <em>(required)</em>' : ''}</td></tr>`
      ).join('\n');
      const optionsBlock = opts
        ? `<h4>Options</h4>
        <table class="cli-options">
          <thead><tr><th>Flag</th><th>Description</th></tr></thead>
          <tbody>
${opts}
          </tbody>
        </table>`
        : '';
      const exampleHighlighted = Prism.highlight(c.example, Prism.languages.bash, 'bash');
      return `<section class="cli-command" id="cmd-${c.slug}">
      <h2 id="cmd-${c.slug}">weft ${htmlEscape(c.name)}</h2>
      <p class="cli-synopsis">${htmlEscape(c.synopsis)}</p>
      <div class="cli-usage">${htmlEscape(c.usage)}</div>
      ${optionsBlock}
      <h4>Example</h4>
      <pre class="code-block language-bash" data-lang="bash"><code class="language-bash">${exampleHighlighted}</code></pre>
      <p><strong>Exits:</strong> ${htmlEscape(c.exits)}</p>
    </section>`;
    }).join('\n\n    ');

    const related = renderRelated(
      ['getting-started', 'authentication', 'exit-codes', 'troubleshooting'],
      slugToMeta,
    );

    const rendered = fillTemplate(cliTemplate, {
      STRIP: strip,
      WEFT_BAR: weftBar,
      FOOTER: partials.footer,
      WARP_TOC: warpToc,
      CLI_COMMANDS: cmdsHtml,
      RELATED: related,
    });
    const outDir = join(DIST, 'cli');
    await ensureDir(outDir);
    await writeFile(join(outDir, 'index.html'), rendered, 'utf8');
    writtenCount++;
    console.log(`wrote ${relative(ROOT, join(outDir, 'index.html'))}`);
  }

  // --- Exit codes -----------------------------------------------------
  {
    const strip   = buildStrip(partials.strip, {
      currentNav: null,
      right: `<input class="strip-search" placeholder="search the loom...">`,
    });
    const weftBar = buildWeftBar(partials.weftBar, docsForNav, null);

    const rows = ecs.codes.map(c => `<tr class="ec-${c.severity}">
          <td class="ec-code">${c.code}</td>
          <td class="ec-meaning">${htmlEscape(c.name)} &mdash; ${htmlEscape(c.meaning)}</td>
          <td class="ec-next">${c.next
            .replace(/\/docs\/([a-z-]+)\.html/g, (_, s) => `<a href="/docs/${s}.html">/docs/${s}.html</a>`)
            .replace(/`([^`]+)`/g, (_, s) => `<code>${htmlEscape(s)}</code>`)}</td>
        </tr>`).join('\n        ');

    const related = renderRelated(
      ['troubleshooting', 'partition-preservation', 'hooks', 'cli'],
      slugToMeta,
    );

    const rendered = fillTemplate(exitCodesTpl, {
      STRIP: strip,
      WEFT_BAR: weftBar,
      FOOTER: partials.footer,
      EXIT_CODE_ROWS: rows,
      RELATED: related,
    });
    const outDir = join(DIST, 'exit-codes');
    await ensureDir(outDir);
    await writeFile(join(outDir, 'index.html'), rendered, 'utf8');
    writtenCount++;
    console.log(`wrote ${relative(ROOT, join(outDir, 'index.html'))}`);
  }

  // --- Samples landing + sample pages ---------------------------------
  const sampleDirs = [
    { slug: '01-simple-bim',          title: 'Sample 01 — Simple .bim',                 blurb: 'Minimal: one .bim, no parameters, no hooks.' },
    { slug: '02-tabular-editor-folder', title: 'Sample 02 — Tabular Editor folder',      blurb: 'Save-to-Folder layout (database.json + per-table JSON).' },
    { slug: '03-with-parameters',     title: 'Sample 03 — Parameterized model',         blurb: 'Per-environment M parameter injection.' },
    { slug: '04-full-pipeline',       title: 'Sample 04 — Full pipeline',               blurb: 'Cert auth + hooks on every lifecycle phase.' },
  ];

  // Samples landing
  {
    const strip   = buildStrip(partials.strip, {
      currentNav: 'samples',
      right: `<input class="strip-search" placeholder="search the loom...">`,
    });
    const weftBar = buildWeftBar(partials.weftBar, docsForNav, null);

    const cards = sampleDirs.map(s =>
      `<a class="sample-card" href="/samples/${s.slug}/">
        <div class="sample-slug">${htmlEscape(s.slug)}</div>
        <h3>${htmlEscape(s.title)}</h3>
        <p>${htmlEscape(s.blurb)}</p>
      </a>`
    ).join('\n      ');

    const h2s = sampleDirs.map(s => ({ text: s.title, slug: s.slug }));
    const warpToc = sampleDirs.map(s =>
      `<a class="warp-thread" href="/samples/${s.slug}/"><span>${htmlEscape(s.title)}</span></a>`
    ).join('\n    ');

    const related = renderRelated(['getting-started', 'parameters', 'hooks', 'cli'], slugToMeta);

    const rendered = fillTemplate(samplesTemplate, {
      STRIP: strip,
      WEFT_BAR: weftBar,
      FOOTER: partials.footer,
      WARP_TOC: warpToc,
      SAMPLE_CARDS: cards,
      RELATED: related,
    });
    const outDir = join(DIST, 'samples');
    await ensureDir(outDir);
    await writeFile(join(outDir, 'index.html'), rendered, 'utf8');
    writtenCount++;
    console.log(`wrote ${relative(ROOT, join(outDir, 'index.html'))}`);
  }

  // Per-sample pages
  for (let i = 0; i < sampleDirs.length; i++) {
    const s = sampleDirs[i];
    const readmePath = join(REPO, 'samples', s.slug, 'README.md');
    if (!existsSync(readmePath)) {
      console.warn(`skipping sample ${s.slug} — README.md not found at ${readmePath}`);
      continue;
    }
    const raw = await readFile(readmePath, 'utf8');
    currentH2s = [];
    let html = marked.parse(raw);
    html = stripLeadingH1(html);
    html = applyLedeClass(html);

    const strip   = buildStrip(partials.strip, {
      currentNav: 'samples',
      right: `<input class="strip-search" placeholder="search the loom...">`,
    });
    const weftBar = buildWeftBar(partials.weftBar, docsForNav, null);

    // related = the other three samples
    const others = sampleDirs.filter(x => x.slug !== s.slug);
    const relatedHtml = others.map(x =>
      `<a class="mini-tile gold" href="/samples/${x.slug}/">
      <div class="mini-tile-icon">⊟</div>
      <div class="mini-tile-text"><strong>${htmlEscape(x.title)}</strong><span>${htmlEscape(x.blurb)}</span></div>
    </a>`
    ).join('\n    ');

    const sampleItems = sampleDirs.map(x => ({
      slug: x.slug, title: x.title, href: `/samples/${x.slug}/`,
    }));
    const { prevLink, nextLink } = renderPrevNext(i, sampleItems);

    const rendered = fillTemplate(sampleTemplate, {
      TITLE: s.title,
      SLUG: s.slug,
      STRIP: strip,
      WEFT_BAR: weftBar,
      FOOTER: partials.footer,
      WARP_TOC: renderWarpToc(currentH2s),
      CONTENT: html,
      RELATED: relatedHtml,
      PREV_LINK: prevLink,
      NEXT_LINK: nextLink,
    });

    const outDir = join(DIST, 'samples', s.slug);
    await ensureDir(outDir);
    await writeFile(join(outDir, 'index.html'), rendered, 'utf8');
    writtenCount++;
    console.log(`wrote ${relative(ROOT, join(outDir, 'index.html'))}`);
  }

  // --- assets ---------------------------------------------------------
  if (existsSync(ASSETS_SRC)) {
    await copyDir(ASSETS_SRC, ASSETS_DIST);
    console.log(`copied assets -> ${relative(ROOT, ASSETS_DIST)}`);
  }

  console.log(`\n==> ${writtenCount} HTML file(s) written to dist/`);
}

build().catch(err => {
  console.error(err);
  process.exit(1);
});

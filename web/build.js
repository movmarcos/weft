// Weft docs website — Pass 1 build script.
// Reads nav.json, converts markdown docs to HTML via marked (+ prismjs for
// code blocks), stitches them into doc.html / homepage.html templates, and
// writes the result to web/dist/.
import { readFile, writeFile, mkdir, readdir, copyFile, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { dirname, join, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { marked } from 'marked';
import matter from 'gray-matter';
import Prism from 'prismjs';
import loadLanguages from 'prismjs/components/index.js';

// Preload a handful of languages we expect to show up in Weft docs.
loadLanguages(['bash', 'yaml', 'json', 'powershell', 'csharp', 'xml-doc', 'markup']);

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = __dirname;
const DIST = join(ROOT, 'dist');
const DOCS_DIST = join(DIST, 'docs');
const TEMPLATES = join(ROOT, 'templates');
const ASSETS_SRC = join(ROOT, 'assets');
const ASSETS_DIST = join(DIST, 'assets');
const DATA = join(ROOT, 'data');

// --- markdown rendering --------------------------------------------------

// We collect H2s as we render so we can build the per-page warp TOC without a
// second pass. `currentH2s` is reset for each doc before marked runs.
let currentH2s = [];

const renderer = new marked.Renderer();
renderer.code = (code, infostring) => {
  const lang = (infostring || '').trim().split(/\s+/)[0];
  if (lang && Prism.languages[lang]) {
    const highlighted = Prism.highlight(code, Prism.languages[lang], lang);
    return `<pre class="language-${lang}"><code class="language-${lang}">${highlighted}</code></pre>`;
  }
  const escaped = code
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
  return `<pre><code>${escaped}</code></pre>`;
};
renderer.heading = (text, level, raw) => {
  const slug = raw.toLowerCase().replace(/[^\w]+/g, '-').replace(/^-|-$/g, '');
  if (level === 2) currentH2s.push({ text, slug });
  return `<h${level} id="${slug}">${text}</h${level}>`;
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

function renderWarpToc(h2s) {
  if (!h2s.length) return '<!-- no H2s -->';
  const items = h2s
    .map(h => `<li><a href="#${h.slug}">${h.text}</a></li>`)
    .join('');
  return `<nav aria-label="On this page"><ul>${items}</ul></nav>`;
}

function renderRelated(relatedSlugs, navIndex) {
  if (!relatedSlugs || !relatedSlugs.length) return '<!-- no related -->';
  const items = relatedSlugs
    .map(s => {
      const entry = navIndex.get(s);
      if (!entry) return `<li>${s}</li>`;
      return `<li><a href="/docs/${entry.slug}.html">${entry.title || entry.slug}</a></li>`;
    })
    .join('');
  return `<nav aria-label="Related"><h4>Related</h4><ul>${items}</ul></nav>`;
}

function renderPrevNext(index, docs) {
  const prev = index > 0 ? docs[index - 1] : null;
  const next = index < docs.length - 1 ? docs[index + 1] : null;
  const prevLink = prev
    ? `<a href="/docs/${prev.slug}.html" rel="prev">← ${prev.title || prev.slug}</a>`
    : '<span></span>';
  const nextLink = next
    ? `<a href="/docs/${next.slug}.html" rel="next">${next.title || next.slug} →</a>`
    : '<span></span>';
  return { prevLink, nextLink };
}

// --- main ----------------------------------------------------------------

async function build() {
  await ensureDir(DIST);
  await ensureDir(DOCS_DIST);

  const nav = JSON.parse(await readFile(join(DATA, 'nav.json'), 'utf8'));
  const docTemplate = await readFile(join(TEMPLATES, 'doc.html'), 'utf8');
  const homeTemplate = await readFile(join(TEMPLATES, 'homepage.html'), 'utf8');

  // First pass: parse every doc so related-link lookups can see titles.
  const parsed = [];
  for (const entry of nav.docs) {
    const abs = resolve(ROOT, entry.file);
    const raw = await readFile(abs, 'utf8');
    const { data, content } = matter(raw);
    parsed.push({ slug: entry.slug, frontmatter: data, body: content });
  }
  const navIndex = new Map(
    parsed.map(p => [p.slug, { slug: p.slug, title: p.frontmatter.title }])
  );

  // Second pass: render each doc through marked + template.
  for (let i = 0; i < parsed.length; i++) {
    const { slug, frontmatter, body } = parsed[i];
    currentH2s = [];
    const html = marked.parse(body);
    const { prevLink, nextLink } = renderPrevNext(i, parsed.map(p => ({
      slug: p.slug, title: p.frontmatter.title,
    })));

    const rendered = fillTemplate(docTemplate, {
      TITLE: frontmatter.title || slug,
      EYEBROW: frontmatter.eyebrow || '',
      COLOR: frontmatter.color || '',
      ICON: frontmatter.icon || '',
      CONTENT: html,
      WEFT_BAR: '<!-- WEFT_BAR partial — Pass 2 -->',
      WARP_TOC: renderWarpToc(currentH2s),
      RELATED: renderRelated(frontmatter.related, navIndex),
      PREV_LINK: prevLink,
      NEXT_LINK: nextLink,
      STRIP: '<!-- STRIP partial — Pass 2 -->',
      FOOTER: '<!-- FOOTER partial — Pass 2 -->',
    });

    const out = join(DOCS_DIST, `${slug}.html`);
    await writeFile(out, rendered, 'utf8');
    console.log(`wrote ${relative(ROOT, out)}`);
  }

  // Homepage.
  const homeOut = join(DIST, 'index.html');
  const homeRendered = fillTemplate(homeTemplate, {
    STRIP: '<!-- STRIP partial — Pass 2 -->',
    WEFT_BAR: '<!-- WEFT_BAR partial — Pass 2 -->',
    FOOTER: '<!-- FOOTER partial — Pass 2 -->',
  });
  await writeFile(homeOut, homeRendered, 'utf8');
  console.log(`wrote ${relative(ROOT, homeOut)}`);

  // Assets.
  if (existsSync(ASSETS_SRC)) {
    await copyDir(ASSETS_SRC, ASSETS_DIST);
    console.log(`copied assets -> ${relative(ROOT, ASSETS_DIST)}`);
  }
}

build().catch(err => {
  console.error(err);
  process.exit(1);
});

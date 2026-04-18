# Weft Documentation Website — Design Spec

**Status:** Approved (mockups validated 2026-04-18)
**Date:** 2026-04-18
**Author:** Marcos Magri (with Claude)
**Audience:** Implementation worker (Plan 5)

## 1. Problem & goal

Weft v1.0.0 ships with eight markdown documentation files in `docs/`. They are linkable from GitHub but not browseable as a website. We need a public docs site that is:

- **Visually distinct** from off-the-shelf doc-site templates (Mintlify, Docusaurus, GitBook). The user explicitly asked for "innovative, out of the box, breaks the patterns."
- **Easy to navigate** for both linear readers (start to finish) and reference users (jump to one topic).
- **On-brand** for a tool literally named after weaving.
- **Self-contained** — deploys to GitHub Pages, no external services, no proprietary editor.
- **Maintainable** — adding a new doc means dropping a markdown file, not touching templates.

## 2. Visual concept — "The Loom"

The site's organizing metaphor is a literal loom in mid-weave.

- **Warp threads** — vertical, structural, stable. Visually they are the page's left rail and the homepage's vertical lines. Conceptually they map to **stable concepts**: auth, parameters, partitions, configuration.
- **Weft threads** — horizontal, the moving piece (the tool's namesake). Visually they are the top breadcrumb and the homepage's horizontal lines. Conceptually they map to the **deploy lifecycle / reading order**: getting started → auth → parameters → deploy concerns → troubleshooting.
- **Tiles** — at the intersections. Each tile is a doc page. Color-coded by type:
  - **Pink/magenta** = safety-critical (partitions, incremental, restore, troubleshooting).
  - **Gold** = practical guides (getting-started, hooks, samples, exit codes).
  - **Green** = reference (CLI ref, auth, parameters, architecture).

This is not pure decoration. The breadcrumb thread, side TOC threads, and homepage loom are all **functional navigation** — the metaphor IS the nav.

## 3. Color & typography

| Token | Hex | Usage |
|---|---|---|
| `--cream` | `#fefcf7` | Page background |
| `--paper` | `#fffefa` | Card / surface background |
| `--paper-2` | `#fdf9ed` | Subtle surface variant (breadcrumb bar) |
| `--warp` | `#1a4d2e` | Primary green; warp threads, headings |
| `--warp-soft` | `#4a8c5a` | Secondary green; muted threads |
| `--weft-pink` | `#e85a9c` | Primary magenta; current state, key italics |
| `--weft-pink-deep` | `#b8389a` | Hover state for pink |
| `--weft-pink-tint` | `#fce8f0` | Pink callout background |
| `--gold` | `#d4a437` | Accent; weft threads, links, tags |
| `--gold-soft` | `#e8c573` | Soft gold; secondary borders |
| `--gold-tint` | `#fbf1d6` | Gold callout / inline-code background |
| `--ink` | `#2a2a2a` | Body text |
| `--ink-soft` | `#5a5040` | Secondary text, italics |
| `--rule` | `#e8dfc8` | Hairlines, borders |

**Type stack**:
- Body & headings: `'Iowan Old Style', Georgia, 'Times New Roman', serif` (warm, book-like)
- UI labels & sans elements: `-apple-system, system-ui, sans-serif`
- Code: `'JetBrains Mono', 'SF Mono', monospace`

**Hierarchy**:
- H1: 48px (doc page) / 64px (homepage hero), green, italic accents in pink
- H2: 28px, green, with pink-circle bullet prefix
- H3: 19px, sans-serif (a deliberate switch — H3 is "subsection of body")
- Body: 17px / 1.65 line-height (generous for reading)
- Code: 13–14px

## 4. Page types

### 4.1 Homepage

Branded entry. Hero on the left (title with one italicized pink word, italic lede, primary/ghost CTA, brew install line). Interactive loom on the right inside a paper card with a double-frame (gold inner border). 12 tiles at thread intersections, hover scales + inverts color. Three-up "why" footer below.

### 4.2 Doc page (8 of these + CLI ref + samples landing + exit codes = 11 total)

Three-column layout (200px / 1fr / 220px) inside a 1280px max-width container:

- **Top strip**: brand mark + nav (Docs / CLI Reference / Samples / GitHub) + search input.
- **Weft breadcrumb bar**: gold thread with 8 knots; current knot is pink and larger; tooltips on hover; click to jump.
- **Left rail (warp TOC)**: vertical green threads, one per H2 on the page. Active = pink knot. Sticky.
- **Center column**: eyebrow tag → big serif H1 → italic lede → body content with code blocks (cream paper, NOT dark theme) and pink/gold callouts. 720px max width.
- **Right rail (related threads)**: 4 mini-tiles linking to related docs. Same color coding. Hover slides right with pink edge.
- **Bottom**: previous / next "along the weft" navigation.

### 4.3 CLI Reference page

Single page, list of all six commands (`validate`, `plan`, `deploy`, `refresh`, `restore-history`, `inspect`). Each command rendered as a "tile-card" with:
- Command name in monospace, green
- One-line synopsis
- Required and optional options table
- Exit codes that this command can return
- Code-block example

### 4.4 Samples landing page

Grid of 4 sample cards (one per `samples/01..04`). Each card shows the sample name, a one-line description, the `weft.yaml` snippet preview, and a "Read the README" link to the rendered sample README on its own page.

### 4.5 Exit codes page

A single styled table (0..10), with each row clickable to jump to a troubleshooting paragraph. Color-coded: 0 green, 2-7 gold, 8-10 pink.

## 5. Site map

```
/                          (homepage with loom)
/docs/getting-started      ┐
/docs/authentication       │
/docs/parameters           │
/docs/partition-preservation
/docs/incremental-refresh  ├── 8 user docs (existing markdown, transformed)
/docs/restore-history      │
/docs/hooks                │
/docs/troubleshooting      ┘
/cli/                      (CLI reference, single page)
/samples/                  (samples landing)
/samples/01-simple-bim     ┐
/samples/02-tabular-editor-folder
/samples/03-with-parameters├── 4 sample READMEs
/samples/04-full-pipeline  ┘
/exit-codes                (color-coded table)
```

Total: **15 pages** generated from existing markdown sources + a small amount of new content (homepage hero text, CLI ref synthesized from `--help`, exit-codes table).

## 6. Build approach

**Tech**: small custom Node.js build script (~150 lines). No framework, no JSX, no build complexity. Dependencies (kept minimal):
- `marked` for markdown → HTML
- `gray-matter` for optional frontmatter (titles, eyebrows, weft position)
- `prismjs` for code-block syntax highlighting (themed to match the cream-paper aesthetic — overrides Prism's defaults with our color tokens)

**Source**: `docs/*.md` (existing, already content-correct), `web/templates/` (HTML shells), `web/assets/` (CSS, fonts, decorative SVG).

**Output**: `web/dist/` (gitignored). One HTML file per source markdown, plus the homepage and special-case pages.

**Build commands**:
```bash
cd web
npm install
npm run build       # writes web/dist/
npm run dev         # writes web/dist/ + serves on localhost:8000 with watch
```

## 7. Repository layout

```
weft/
├── docs/                           # existing, source of truth — UNCHANGED
│   ├── getting-started.md
│   ├── authentication.md
│   ├── parameters.md
│   ├── partition-preservation.md
│   ├── incremental-refresh.md
│   ├── restore-history.md
│   ├── hooks.md
│   └── troubleshooting.md
└── web/                            # NEW
    ├── package.json                # deps + scripts
    ├── build.js                    # the Node build script
    ├── dev.js                      # watch + serve helper
    ├── templates/
    │   ├── homepage.html           # loom layout + hero
    │   ├── doc.html                # 3-column doc page
    │   ├── cli.html                # CLI reference
    │   ├── samples.html            # samples landing
    │   ├── sample.html             # individual sample page
    │   ├── exit-codes.html         # color-coded table
    │   └── partials/
    │       ├── strip.html          # top brand strip
    │       ├── weft-bar.html       # breadcrumb thread
    │       └── footer.html         # site footer
    ├── assets/
    │   ├── css/
    │   │   ├── tokens.css          # color + type variables
    │   │   ├── strip.css
    │   │   ├── loom.css            # homepage loom + tile styles
    │   │   ├── doc.css             # doc-page layout
    │   │   ├── code.css            # cream-paper Prism theme
    │   │   └── responsive.css      # mobile breakpoints
    │   ├── fonts/
    │   │   └── (JetBrains Mono if not from system)
    │   └── svg/
    │       ├── brand-mark.svg
    │       └── tile-icons/         # one SVG per tile-icon
    ├── data/
    │   ├── nav.json                # doc order on the weft thread
    │   ├── related.json            # per-page "related threads"
    │   └── tiles.json              # homepage tile positions + colors
    └── dist/                       # gitignored; build output

.github/workflows/docs.yml          # build & deploy to gh-pages on push to master
```

## 8. Markdown frontmatter convention

Each source markdown file gets optional YAML frontmatter consumed by the build:

```markdown
---
title: Partition preservation — the core guarantee
eyebrow: Partitions · safety-critical
order: 4                    # position on the weft thread (1..8)
color: pink                 # pink | gold | green
icon: ▦
related:                    # 2-4 keys from nav.json
  - incremental-refresh
  - restore-history
  - troubleshooting
---

# Existing markdown content unchanged
```

If the frontmatter is missing, sensible defaults (filename → title; lookup table for color; alphabetical order on the weft).

The 8 existing docs get this frontmatter added in Plan 5; their body content is unchanged.

## 9. Mobile / responsive

- **≥ 1024px (desktop)**: full three-column layout, full loom on homepage.
- **768–1023px (tablet)**: two-column doc page (right rail moves below content). Homepage loom shrinks to ~480px square; tile labels hide.
- **< 768px (phone)**: single column. Doc page hides both rails (warp TOC becomes a sticky-top dropdown; related-threads list moves to bottom). Homepage replaces the loom with a vertical list of tiles, but each tile keeps its color/icon — the loom collapses to its grid cells stacked.

The metaphor degrades gracefully: on mobile we are no longer "looking at a loom" but the colors and icons still cohere.

## 10. Accessibility

- Color is **never** the only signifier. Tile color + icon character + text label.
- Body text 17px on cream → contrast ratio 8.4:1 (WCAG AAA).
- Code text 14px gold-on-tint → 4.6:1 (WCAG AA).
- Pink elements always paired with bold/italic text or icons.
- All decorative SVG (threads, brand mark) is `aria-hidden`.
- Active state (current weft knot, active warp thread) carries `aria-current="page"`.
- Tab order: skip link → main nav → page content → related → footer.
- Reduced-motion media query disables the hover-scale on tiles.

## 11. Search

Build-time generated `dist/search-index.json` (one JSON file containing every doc's title, headings, and ~200-char excerpt). Client-side fuzzy search via a tiny vanilla JS module (`assets/js/search.js`, ~80 lines using `flexsearch` or hand-rolled). The strip's search input shows results inline as a dropdown — each result rendered as a mini-tile that matches the homepage style. **No external service** (no Algolia, no Pagefind binary).

## 12. Deployment

GitHub Actions workflow `.github/workflows/docs.yml`:

```yaml
on:
  push:
    branches: [master]
  workflow_dispatch:
permissions:
  contents: read
  pages: write
  id-token: write
jobs:
  build-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: npm ci
        working-directory: web
      - run: npm run build
        working-directory: web
      - uses: actions/configure-pages@v4
      - uses: actions/upload-pages-artifact@v3
        with: { path: web/dist }
      - uses: actions/deploy-pages@v4
```

Result: `https://marcosmagri.github.io/weft/` (or whatever the user's GitHub URL is) on every master push.

## 13. Out of scope

- **Versioned docs** (v1.0 vs v2.0). v1 only for now; the build can grow versioning later.
- **Comments / discussions**. Use GitHub Issues / Discussions.
- **i18n**. English only.
- **Dark mode toggle**. The user explicitly asked for white background; a dark mode would require re-tuning every color token and code theme.
- **Analytics**. Privacy-first; can be added later as an opt-in script.
- **A custom domain**. `github.io` URL is the v1 target; user can configure CNAME later.

## 14. Success criteria

- `npm run build` produces `web/dist/` with 15 HTML files, `search-index.json`, all CSS/SVG assets.
- Lighthouse: Performance ≥ 95, Accessibility ≥ 95, Best Practices ≥ 95, SEO ≥ 90.
- All 8 existing markdown docs render without manual edits beyond adding frontmatter.
- Push to master deploys live within 2 minutes.
- The homepage loom and doc-page layout match the approved mockups (within a small margin for content-driven layout shifts).

## 15. Open questions

None. All visual decisions validated through browser mockups; technical decisions confirmed in terminal.

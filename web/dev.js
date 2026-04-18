// Weft docs — dev server.
// Watches templates, assets, docs, and nav data; re-runs build.js on change
// and serves web/dist/ over plain node http on localhost:8000.
import { spawn } from 'node:child_process';
import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { dirname, extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import chokidar from 'chokidar';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = __dirname;
const DIST = join(ROOT, 'dist');
const REPO = resolve(ROOT, '..');
const PORT = 8000;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ico': 'image/x-icon',
  '.txt': 'text/plain; charset=utf-8',
};

let building = false;
let pending = false;

function runBuild() {
  if (building) { pending = true; return; }
  building = true;
  console.log('[dev] building...');
  const child = spawn(process.execPath, [join(ROOT, 'build.js')], {
    stdio: 'inherit',
    cwd: ROOT,
  });
  child.on('exit', code => {
    building = false;
    if (code !== 0) console.error(`[dev] build failed (exit ${code})`);
    else console.log('[dev] build ok');
    if (pending) { pending = false; runBuild(); }
  });
}

// Initial build.
runBuild();

// Watchers.
const watcher = chokidar.watch([
  join(ROOT, 'templates'),
  join(ROOT, 'assets'),
  join(ROOT, 'data'),
  join(REPO, 'docs'),
], { ignored: /(^|[\\/])\../, ignoreInitial: true });

watcher.on('all', (event, path) => {
  console.log(`[dev] ${event} ${path}`);
  runBuild();
});

// HTTP server.
const server = createServer((req, res) => {
  try {
    const urlPath = decodeURIComponent(req.url.split('?')[0]);
    let fsPath = join(DIST, normalize(urlPath));
    if (!fsPath.startsWith(DIST)) { res.statusCode = 403; res.end('forbidden'); return; }
    if (existsSync(fsPath) && statSync(fsPath).isDirectory()) {
      fsPath = join(fsPath, 'index.html');
    }
    if (!existsSync(fsPath)) {
      res.statusCode = 404;
      res.setHeader('content-type', 'text/plain; charset=utf-8');
      res.end(`404 not found: ${urlPath}`);
      return;
    }
    res.setHeader('content-type', MIME[extname(fsPath)] || 'application/octet-stream');
    createReadStream(fsPath).pipe(res);
  } catch (err) {
    res.statusCode = 500;
    res.end(String(err));
  }
});

server.listen(PORT, () => {
  console.log(`[dev] serving ${DIST} at http://localhost:${PORT}`);
});

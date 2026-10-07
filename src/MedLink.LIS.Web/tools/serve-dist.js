#!/usr/bin/env node
/*
 * Простий статичний сервер для dist/spa з SPA-fallback (history mode) — для smoke-тестів без API.
 * Використання: node tools/serve-dist.js [port] [dir]
 */
const http = require('http');
const fs = require('fs');
const path = require('path');

const port = Number(process.argv[2] || 8085);
const root = path.resolve(process.argv[3] || path.join(__dirname, '..', 'dist', 'spa'));

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.eot': 'application/vnd.ms-fontobject',
  '.ico': 'image/x-icon'
};

http.createServer((req, res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  if (url.startsWith('/api/')) {
    // API відсутнє — імітуємо недоступність сервісу (502), щоб SPA показала банер
    res.writeHead(502, { 'Content-Type': 'application/problem+json' });
    res.end(JSON.stringify({ title: 'API недоступне', status: 502, detail: 'Статичний сервер без бекенду' }));
    return;
  }
  let file = path.join(root, url);
  if (!file.startsWith(root)) { res.writeHead(403); res.end(); return; }
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) {
    file = path.join(root, 'index.html');
  }
  const ext = path.extname(file).toLowerCase();
  res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
}).listen(port, () => {
  console.log(`MedLink LIS SPA: http://localhost:${port}  (root: ${root})`);
});

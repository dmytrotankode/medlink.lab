#!/usr/bin/env node
/*
 * Статичний сервер для dist/spa з SPA-fallback (history mode) — для smoke-тестів.
 * /api/* проксіюється на API_TARGET (напр. http://localhost:5055, реальний API або tools/mock-api.js);
 * без API_TARGET /api/* повертає 502 (імітація недоступного бекенду → банер у SPA).
 * Використання: API_TARGET=http://localhost:5055 node tools/serve-dist.js [port=8085] [dir=dist/spa]
 */
const http = require('http');
const fs = require('fs');
const path = require('path');
const url = require('url');

const port = Number(process.argv[2] || 8085);
const root = path.resolve(process.argv[3] || path.join(__dirname, '..', 'dist', 'spa'));
const target = process.env.API_TARGET ? url.parse(process.env.API_TARGET) : null;

const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.css': 'text/css', '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml', '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.eot': 'application/vnd.ms-fontobject', '.ico': 'image/x-icon' };

http.createServer((req, res) => {
  const pathname = decodeURIComponent(req.url.split('?')[0]);
  if (pathname.startsWith('/api/') || pathname === '/health') {
    if (!target) {
      res.writeHead(502, { 'Content-Type': 'application/problem+json' });
      res.end(JSON.stringify({ title: 'API недоступне', status: 502, detail: 'Статичний сервер без бекенду (API_TARGET не задано)' }));
      return;
    }
    const proxy = http.request({ hostname: target.hostname, port: target.port, path: req.url, method: req.method, headers: { ...req.headers, host: target.host } }, pr => {
      res.writeHead(pr.statusCode, pr.headers);
      pr.pipe(res);
    });
    proxy.on('error', () => { res.writeHead(502, { 'Content-Type': 'application/problem+json' }); res.end(JSON.stringify({ title: 'API недоступне', status: 502 })); });
    req.pipe(proxy);
    return;
  }
  let file = path.join(root, pathname);
  if (!file.startsWith(root)) { res.writeHead(403); res.end(); return; }
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(root, 'index.html');
  const ext = path.extname(file).toLowerCase();
  res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
}).listen(port, () => {
  console.log(`MedLink LIS SPA: http://localhost:${port}  (root: ${root}; API → ${target ? process.env.API_TARGET : 'немає (502)'})`);
});

// Tiny static server for the built web component (dev).
// Serves dist/ with CORS enabled so any host platform can load it cross-origin.
import http from 'node:http';
import { readFile } from 'node:fs/promises';
import path from 'node:path';

const dist = path.join(import.meta.dirname, '..', 'dist');
const port = Number(process.env.PORT ?? 5174);

const server = http.createServer((req, res) => {
  const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
  const pathname = url.pathname;

  if (pathname === '/healthz') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ service: 'scs-web', status: 'serving dist' }));
    return;
  }

  const requestPath = pathname === '/' ? '/scs-web.js' : pathname;
  const file = path.normalize(path.join(dist, requestPath));
  if (!file.startsWith(dist)) {
    res.writeHead(403);
    res.end('forbidden');
    return;
  }

  readFile(file)
    .then((buf) => {
      const contentType = file.endsWith('.js')
        ? 'text/javascript'
        : file.endsWith('.css')
          ? 'text/css'
          : 'application/octet-stream';
      res.writeHead(200, {
        'Content-Type': contentType,
        'Access-Control-Allow-Origin': '*',
        'Cache-Control': 'no-cache',
      });
      res.end(buf);
    })
    .catch(() => {
      res.writeHead(404, { 'Access-Control-Allow-Origin': '*' });
      res.end('not found');
    });
});

server.listen(port, () => {
  console.log(`[scs-web] serving dist at http://localhost:${port} (component: /scs-web.js)`);
});

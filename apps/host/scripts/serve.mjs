// Tiny static server for the built host SPA (Docker).
// Serves dist/ with SPA fallback: any non-file path returns index.html.
import http from 'node:http';
import { readFile } from 'node:fs/promises';
import path from 'node:path';

const dist = path.join(import.meta.dirname, '..', 'dist');
const port = Number(process.env.PORT ?? 8080);

const server = http.createServer((req, res) => {
  const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
  const pathname = url.pathname;

  if (pathname === '/healthz') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ service: 'scs-host', status: 'serving dist' }));
    return;
  }

  let file = path.normalize(path.join(dist, pathname));
  if (!file.startsWith(dist)) {
    res.writeHead(403);
    res.end('forbidden');
    return;
  }

  const respond = (filePath, status = 200) => {
    readFile(filePath)
      .then((buf) => {
        const ext = path.extname(filePath);
        const contentType =
          ext === '.js'
            ? 'text/javascript'
            : ext === '.css'
              ? 'text/css'
              : ext === '.html'
                ? 'text/html'
                : 'application/octet-stream';
        res.writeHead(status, { 'Content-Type': contentType, 'Cache-Control': 'no-cache' });
        res.end(buf);
      })
      .catch(() => {
        res.writeHead(404);
        res.end('not found');
      });
  };

  // SPA fallback: unknown paths (no extension) serve index.html.
  if (!path.extname(pathname)) {
    respond(path.join(dist, 'index.html'));
    return;
  }
  respond(file);
});

server.listen(port, () => {
  console.log(`[scs-host] serving dist at http://localhost:${port}`);
});

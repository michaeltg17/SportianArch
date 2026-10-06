// Starts the three SCS .NET services in the background for local dev.
// Usage: node scripts/run-apis.mjs           (start)
//        node scripts/run-apis.mjs stop      (stop)
import { spawn, spawnSync } from 'node:child_process';
import { writeFileSync, readFileSync, existsSync, openSync } from 'node:fs';
import path from 'node:path';

const root = path.join(import.meta.dirname, '..');
const services = [
  { name: 'scs-todos-api', dir: 'apps/scs-todos-api', dll: 'Todos.Api.dll', port: 8081 },
  { name: 'scs-notifications-api', dir: 'apps/scs-notifications-api', dll: 'Notifications.Api.dll', port: 8082 },
  { name: 'scs-bff', dir: 'apps/scs-bff', dll: 'Bff.Api.dll', port: 8083 },
];

const pidFile = path.join(root, 'scripts', '.api-pids.json');

function stop() {
  if (!existsSync(pidFile)) {
    console.log('No running API pids found.');
    return;
  }
  const pids = JSON.parse(readFileSync(pidFile, 'utf8'));
  for (const pid of Object.values(pids)) {
    try {
      process.kill(Number(pid));
      console.log(`stopped pid ${pid}`);
    } catch {
      console.log(`pid ${pid} not running`);
    }
  }
}

function start() {
  const pids = {};
  for (const s of services) {
    const logPath = path.join(root, 'scripts', `.${s.name}.log`);
    const out = spawnSync('dotnet', ['build', '-v', 'q', '--nologo'], { cwd: path.join(root, s.dir), stdio: 'inherit' });
    if (out.status !== 0) {
      console.error(`build failed for ${s.name}`);
      process.exit(1);
    }
    const logFd = openSync(logPath, 'a');
    const child = spawn('dotnet', ['bin/Debug/net10.0/' + s.dll], {
      cwd: path.join(root, s.dir),
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: `http://localhost:${s.port}` },
      detached: true,
      stdio: ['ignore', logFd, logFd],
    });
    child.unref();
    pids[s.name] = String(child.pid);
    console.log(`${s.name} started (pid ${child.pid}) on http://localhost:${s.port} — log: scripts/.${s.name}.log`);
  }
  writeFileSync(pidFile, JSON.stringify(pids, null, 2));
}

if (process.argv[2] === 'stop') stop();
else start();

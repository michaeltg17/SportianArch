// Mints a demo host-issued JWT (HS256) for local API testing.
// Usage: node scripts/mint-token.mjs [sub] [name]
import crypto from 'node:crypto';

const SECRET = process.env.SCS_JWT_SECRET ?? 'scs-demo-shared-secret-0123456789-0123456789';
const sub = process.argv[2] ?? 'ada';
const name = process.argv[3] ?? 'Ada Lovelace';

const b64url = (input) => Buffer.from(input).toString('base64url');
const header = b64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
const now = Math.floor(Date.now() / 1000);
const payload = b64url(JSON.stringify({ sub, name, iss: 'sportian-host', aud: 'scs', iat: now, exp: now + 3600 }));
const signature = crypto.createHmac('sha256', SECRET).update(`${header}.${payload}`).digest('base64url');

console.log(`${header}.${payload}.${signature}`);

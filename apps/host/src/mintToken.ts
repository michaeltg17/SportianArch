import { AUTH } from '@scs/shared';

export interface HostUser {
  id: string;
  name: string;
}

/**
 * Demo-only JWT minting, standing in for the host platform's real identity
 * provider. In production the host (or its OIDC IdP) signs the delegated
 * token with an asymmetric key and SCS APIs validate it via JWKS — see
 * docs/ARCHITECTURE.md. Here we sign HS256 with a shared demo secret so the
 * whole stack can run without an IdP.
 */
function base64url(input: Uint8Array | string): string {
  const bytes = typeof input === 'string' ? new TextEncoder().encode(input) : input;
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export async function mintHostJwt(user: HostUser, ttlSeconds = 3600): Promise<string> {
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    'raw',
    encoder.encode(AUTH.demoSecret),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign'],
  );

  const headerB64 = base64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const now = Math.floor(Date.now() / 1000);
  const payloadB64 = base64url(
    JSON.stringify({
      iss: AUTH.issuer,
      aud: AUTH.audience,
      sub: user.id,
      name: user.name,
      iat: now,
      exp: now + ttlSeconds,
    }),
  );

  const signingInput = `${headerB64}.${payloadB64}`;
  const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(signingInput));
  return `${signingInput}.${base64url(new Uint8Array(signature))}`;
}

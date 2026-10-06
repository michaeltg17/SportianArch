import { useCallback, useEffect, useRef, useState } from 'react';
import { AUTH, type ScsEventPayloadMap } from '@scs/shared';
import { loadComponent } from './loadComponent';
import { mintHostJwt, type HostUser } from './mintToken';

declare module 'react' {
  namespace JSX {
    interface IntrinsicElements {
      'scs-todo-app': React.DetailedHTMLProps<
        React.HTMLAttributes<HTMLElement>,
        HTMLElement
      > & {
        'scs-web-url'?: string;
        'scs-token'?: string;
        'scs-user'?: string;
        'scs-user-id'?: string;
      };
    }
  }
}

const SCS_WEB_URL =
  (import.meta.env.VITE_SCS_WEB_URL as string | undefined) ?? 'http://localhost:5174';
const BFF_URL = (import.meta.env.VITE_BFF_URL as string | undefined) ?? 'http://localhost:8083';

const DEMO_USERS: HostUser[] = [
  { id: 'ada', name: 'Ada Lovelace' },
  { id: 'grace', name: 'Grace Hopper' },
];

interface ScsLogEntry {
  id: number;
  time: string;
  type: string;
  detail: string;
}

const MAX_LOG_ENTRIES = 100;
let logId = 0;

export default function App() {
  const [user, setUser] = useState<HostUser>(DEMO_USERS[0]);
  const [token, setToken] = useState<string | null>(null);
  const [componentLoaded, setComponentLoaded] = useState(false);
  const [componentError, setComponentError] = useState<string | null>(null);
  const [logs, setLogs] = useState<ScsLogEntry[]>([]);
  const scsRef = useRef<HTMLElement | null>(null);

  // 1) Load the SCS component bundle from its own origin (once).
  useEffect(() => {
    let cancelled = false;
    loadComponent(`${SCS_WEB_URL}/scs-web.js`)
      .then(() => {
        if (!cancelled) setComponentLoaded(true);
      })
      .catch((err: unknown) => {
        if (!cancelled) setComponentError(err instanceof Error ? err.message : String(err));
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // 2) Mint a delegated JWT whenever the signed-in host user changes.
  useEffect(() => {
    let cancelled = false;
    setToken(null);
    mintHostJwt(user)
      .then((jwt) => {
        if (!cancelled) setToken(jwt);
      })
      .catch((err: unknown) => {
        if (!cancelled) setComponentError(err instanceof Error ? err.message : String(err));
      });
    return () => {
      cancelled = true;
    };
  }, [user]);

  const addLog = useCallback((type: string, detail: unknown) => {
    setLogs((prev) =>
      [
        {
          id: ++logId,
          time: new Date().toLocaleTimeString([], { hour12: false }),
          type,
          detail: JSON.stringify(detail ?? {}, null, 2),
        },
        ...prev,
      ].slice(0, MAX_LOG_ENTRIES),
    );
  }, []);

  // 3) Observe the scs:* channel on the embedded element.
  useEffect(() => {
    const el = scsRef.current;
    if (!el) return;

    const handler = (e: Event) => {
      const custom = e as CustomEvent<ScsEventPayloadMap[keyof ScsEventPayloadMap]>;
      if (typeof custom.type !== 'string' || !custom.type.startsWith('scs:')) return;
      addLog(custom.type, custom.detail);
    };

    const types: Array<keyof ScsEventPayloadMap> = [
      'scs:ready',
      'scs:todo-added',
      'scs:todo-completed',
      'scs:error',
    ];
    types.forEach((t) => el.addEventListener(t, handler));
    return () => {
      types.forEach((t) => el.removeEventListener(t, handler));
    };
  }, [addLog, componentLoaded, token, user]);

  return (
    <div className="host-page">
      <header className="host-header">
        <div>
          <h1>Sportian Host Platform</h1>
          <p className="host-subtitle">
            Embedding the Todo SCS as a web component — the host owns identity, the SCS owns
            behavior.
          </p>
        </div>
        <div className="host-user-switch">
          <label htmlFor="host-user">Signed in as</label>
          <select
            id="host-user"
            value={user.id}
            onChange={(e) => {
              const next = DEMO_USERS.find((u) => u.id === e.target.value);
              if (next) setUser(next);
            }}
          >
            {DEMO_USERS.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name}
              </option>
            ))}
          </select>
        </div>
      </header>

      <div className="host-columns">
        <section className="host-panel">
          <div className="host-panel-title">Embedded SCS</div>
          {!componentLoaded && !componentError && (
            <p className="host-status">Loading SCS component from {SCS_WEB_URL}…</p>
          )}
          {componentError && <p className="host-status host-error">{componentError}</p>}
          {componentLoaded && !token && <p className="host-status">Minting delegated JWT…</p>}
          {componentLoaded && token && (
            <scs-todo-app
              key={user.id}
              ref={(el) => {
                scsRef.current = el;
              }}
              scs-web-url={BFF_URL}
              scs-token={token}
              scs-user={user.name}
              scs-user-id={user.id}
            />
          )}
          <p className="host-meta">
            Component: <code>{SCS_WEB_URL}/scs-web.js</code> · BFF: <code>{BFF_URL}</code> · JWT
            issuer: <code>{AUTH.issuer}</code> (demo HS256)
          </p>
        </section>

        <section className="host-panel host-console">
          <div className="host-panel-title">scs:* event console</div>
          {logs.length === 0 ? (
            <p className="host-status">No events yet. Interact with the SCS…</p>
          ) : (
            <ol className="host-log">
              {logs.map((entry) => (
                <li key={entry.id} className="host-log-entry">
                  <div className="host-log-head">
                    <code>{entry.type}</code>
                    <time>{entry.time}</time>
                  </div>
                  <pre>{entry.detail}</pre>
                </li>
              ))}
            </ol>
          )}
        </section>
      </div>
    </div>
  );
}

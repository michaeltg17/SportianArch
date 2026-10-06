import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type {
  CreateTodoRequest,
  DashboardResponse,
  ScsEventPayloadMap,
  TodoDto,
} from '@scs/shared';
import type { NotificationDto } from '@scs/shared';
import { ScsApi, ScsApiError } from './api';

export const SCS_NAME = 'scs-todo-app';
export const SCS_VERSION = '1.0.0';

/** Event names on the host <-> SCS channel (see @scs/shared). */
export const SCS_EVENTS = {
  ready: 'scs:ready',
  todoAdded: 'scs:todo-added',
  todoCompleted: 'scs:todo-completed',
  error: 'scs:error',
} as const;

export interface ScsHostInfo {
  name: string;
  userId: string;
}

interface TodoAppProps {
  api: ScsApi;
  host: ScsHostInfo;
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

/**
 * The entire user interface of the SCS Todo module.
 * Renders INSIDE the shadow root: the host platform's CSS can't touch it.
 */
export function TodoApp({ api, host }: TodoAppProps) {
  const [todos, setTodos] = useState<TodoDto[]>([]);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [title, setTitle] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastSync, setLastSync] = useState<string | null>(null);

  const rootRef = useRef<HTMLDivElement | null>(null);

  /**
   * The ONLY integration surface with the host platform: CustomEvents,
   * dispatched on an element inside the shadow root. They are `composed`,
   * so they retarget and cross the shadow boundary to reach the host.
   */
  const postToHost = useCallback(
    <K extends keyof ScsEventPayloadMap>(type: K, payload: ScsEventPayloadMap[K]) => {
      const target = rootRef.current;
      if (!target) return;
      target.dispatchEvent(
        new CustomEvent<ScsEventPayloadMap[K]>(type, {
          bubbles: true,
          composed: true,
          detail: payload,
        }),
      );
    },
    [],
  );

  const emitError = useCallback(
    (err: unknown) => {
      const status = err instanceof ScsApiError ? err.status : undefined;
      const message = err instanceof Error ? err.message : 'Unknown error';
      postToHost(SCS_EVENTS.error, { message, status });
      setError(message);
    },
    [postToHost],
  );

  const refresh = useCallback(async () => {
    try {
      const dashboard: DashboardResponse = await api.getDashboard();
      setTodos(dashboard.todos);
      setNotifications(dashboard.notifications);
      setError(dashboard.warnings.length > 0 ? dashboard.warnings.join('; ') : null);
      setLastSync(new Date().toISOString());
    } catch (err) {
      emitError(err);
    } finally {
      setLoading(false);
    }
  }, [api, emitError]);

  // Announce readiness once mounted, then poll the dashboard on an interval:
  // notifications arrive asynchronously (RabbitMQ consumer), so the UI
  // refreshes on a timer instead of requiring a push channel.
  useEffect(() => {
    postToHost(SCS_EVENTS.ready, { name: SCS_NAME, version: SCS_VERSION });
    void refresh();
    const timer = setInterval(() => void refresh(), 10_000);
    return () => clearInterval(timer);
  }, [postToHost, refresh]);

  const handleAdd = useCallback(
    async (event: React.FormEvent) => {
      event.preventDefault();
      const trimmed = title.trim();
      if (!trimmed || busy) return;
      setBusy(true);
      setError(null);
      try {
        const body: CreateTodoRequest = { title: trimmed };
        const created = await api.createTodo(body);
        setTitle('');
        postToHost(SCS_EVENTS.todoAdded, {
          todoId: created.id,
          title: created.title,
        });
        await refresh();
      } catch (err) {
        emitError(err);
      } finally {
        setBusy(false);
      }
    },
    [api, busy, emitError, postToHost, refresh, title],
  );

  const handleComplete = useCallback(
    async (todo: TodoDto) => {
      setError(null);
      try {
        await api.completeTodo(todo.id);
        postToHost(SCS_EVENTS.todoCompleted, {
          todoId: todo.id,
          title: todo.title,
        });
        await refresh();
      } catch (err) {
        emitError(err);
      }
    },
    [api, emitError, postToHost, refresh],
  );

  const handleDelete = useCallback(
    async (todo: TodoDto) => {
      setError(null);
      try {
        await api.deleteTodo(todo.id);
        await refresh();
      } catch (err) {
        emitError(err);
      }
    },
    [api, emitError, refresh],
  );

  const completedCount = useMemo(
    () => todos.filter((t) => t.isCompleted).length,
    [todos],
  );

  return (
    <div className="scs-card" ref={rootRef}>
      <div className="scs-header">
        <div>
          <div className="scs-header-title">SCS Todo Module</div>
          <div className="scs-header-sub">
            {todos.length - completedCount} open · {todos.length} total
          </div>
        </div>
        <span className="scs-user-badge">@{host.name}</span>
      </div>

      <form className="scs-form" onSubmit={handleAdd}>
        <input
          className="scs-todo-input"
          data-testid="scs-todo-input"
          placeholder="What needs doing?"
          value={title}
          maxLength={200}
          onChange={(e) => setTitle(e.target.value)}
        />
        <button className="scs-add-btn" type="submit" disabled={busy || !title.trim()}>
          Add
        </button>
      </form>

      {loading ? (
        <p className="scs-loading" data-testid="scs-loading">Loading…</p>
      ) : todos.length === 0 ? (
        <p className="scs-empty" data-testid="scs-empty">
          Nothing here yet — add your first task.
        </p>
      ) : (
        <ul className="scs-todo-list" data-testid="scs-todo-list">
          {todos.map((todo) => (
            <li
              key={todo.id}
              className={`scs-todo-item${todo.isCompleted ? ' is-completed' : ''}`}
              data-testid={`scs-todo-item-${todo.id}`}
            >
              <span className="scs-todo-title">{todo.title}</span>
              {!todo.isCompleted ? (
                <button
                  className="scs-complete-btn"
                  data-testid={`scs-complete-${todo.id}`}
                  onClick={() => void handleComplete(todo)}
                >
                  Done
                </button>
              ) : (
                <span className="scs-complete-done" data-testid={`scs-done-${todo.id}`}>
                  ✓
                </span>
              )}
              <button
                className="scs-delete-btn"
                data-testid={`scs-delete-${todo.id}`}
                onClick={() => void handleDelete(todo)}
              >
                ✕
              </button>
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p className="scs-error" role="alert" data-testid="scs-error">
          {error}
        </p>
      )}

      <div className="scs-notifications">
        <div className="scs-notifications-title">
          Notifications
          {notifications.length > 0 && (
            <span className="scs-bell-count" data-testid="scs-notification-count">
              {notifications.length}
            </span>
          )}
        </div>
        {notifications.length === 0 ? (
          <p className="scs-empty">No notifications.</p>
        ) : (
          <div data-testid="scs-notification-list">
            {notifications.map((n) => (
              <div className="scs-notification" key={n.id} data-testid="scs-notification-item">
                {n.message}
                <time>{formatTime(n.createdAt)}</time>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="scs-status">
        <span>
          {lastSync ? (
            <>
              last sync <time>{formatTime(lastSync)}</time>
            </>
          ) : (
            'syncing…'
          )}
        </span>
        <span className="scs-live">● live</span>
      </div>
    </div>
  );
}

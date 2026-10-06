// Shadow-DOM scoped styles. Everything is prefixed with "scs-" and themed via
// CSS custom properties, so nothing can leak into (or be broken by) the host
// platform's stylesheet.
export const SCS_STYLES = `
:host {
  --scs-bg: #ffffff;
  --scs-surface: #f8fafc;
  --scs-border: #e2e8f0;
  --scs-text: #0f172a;
  --scs-muted: #64748b;
  --scs-accent: #2563eb;
  --scs-accent-contrast: #ffffff;
  --scs-danger: #dc2626;
  --scs-success: #16a34a;
  display: block;
  font-family: ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  line-height: 1.5;
}
:host([data-theme="dark"]) {
  --scs-bg: #0f172a;
  --scs-surface: #1e293b;
  --scs-border: #334155;
  --scs-text: #e2e8f0;
  --scs-muted: #94a3b8;
  --scs-accent: #3b82f6;
  --scs-accent-contrast: #ffffff;
  --scs-danger: #f87171;
  --scs-success: #4ade80;
}
*, *::before, *::after { box-sizing: border-box; }
.scs-card {
  background: var(--scs-bg);
  color: var(--scs-text);
  border: 1px solid var(--scs-border);
  border-radius: 12px;
  padding: 16px;
  max-width: 640px;
  font-size: 14px;
}
.scs-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}
.scs-header-title { font-size: 15px; font-weight: 700; }
.scs-header-sub { color: var(--scs-muted); font-size: 12px; }
.scs-user-badge {
  background: var(--scs-surface);
  border: 1px solid var(--scs-border);
  border-radius: 999px;
  padding: 2px 10px;
  font-size: 12px;
  color: var(--scs-muted);
  white-space: nowrap;
}
.scs-form { display: flex; gap: 8px; margin-bottom: 12px; }
.scs-todo-input {
  flex: 1;
  padding: 8px 10px;
  border: 1px solid var(--scs-border);
  border-radius: 8px;
  background: var(--scs-surface);
  color: var(--scs-text);
  font: inherit;
}
.scs-todo-input:focus { outline: 2px solid var(--scs-accent); outline-offset: -1px; }
.scs-add-btn {
  padding: 8px 14px;
  border: none;
  border-radius: 8px;
  background: var(--scs-accent);
  color: var(--scs-accent-contrast);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}
.scs-add-btn:disabled { opacity: .6; cursor: default; }
.scs-todo-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.scs-todo-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border: 1px solid var(--scs-border);
  border-radius: 8px;
  background: var(--scs-surface);
}
.scs-todo-item.is-completed .scs-todo-title {
  text-decoration: line-through;
  color: var(--scs-muted);
}
.scs-todo-title { flex: 1; word-break: break-word; }
.scs-complete-btn, .scs-delete-btn {
  border: 1px solid var(--scs-border);
  background: transparent;
  color: var(--scs-muted);
  border-radius: 6px;
  padding: 3px 8px;
  cursor: pointer;
  font-size: 12px;
}
.scs-complete-btn:hover { border-color: var(--scs-success); color: var(--scs-success); }
.scs-delete-btn:hover { border-color: var(--scs-danger); color: var(--scs-danger); }
.scs-empty, .scs-loading { color: var(--scs-muted); padding: 12px 4px; }
.scs-error { color: var(--scs-danger); padding: 8px 0; font-size: 13px; }
.scs-notifications {
  margin-top: 14px;
  border-top: 1px solid var(--scs-border);
  padding-top: 10px;
}
.scs-notifications-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 8px;
}
.scs-bell-count {
  background: var(--scs-accent);
  color: var(--scs-accent-contrast);
  border-radius: 999px;
  font-size: 11px;
  padding: 0 7px;
}
.scs-notification {
  font-size: 12px;
  color: var(--scs-muted);
  padding: 4px 0;
  border-bottom: 1px dashed var(--scs-border);
}
.scs-notification:last-child { border-bottom: none; }
.scs-notification time { opacity: .7; margin-left: 6px; font-size: 11px; }
.scs-status {
  font-size: 11px;
  color: var(--scs-muted);
  margin-top: 10px;
  display: flex;
  justify-content: space-between;
}
.scs-live { color: var(--scs-success); }
`;

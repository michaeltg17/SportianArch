import { createRoot, type Root } from 'react-dom/client';
import { ScsApi } from './api';
import { SCS_EVENTS, TodoApp, type ScsHostInfo } from './TodoApp';
import { SCS_STYLES } from './styles';

/**
 * <scs-todo-app> — the SCS web component.
 *
 * Configuration via attributes (set by the host platform before/after mount):
 *   scs-web-url   BFF origin, e.g. http://localhost:8083 (required)
 *   scs-token     Host-issued JWT (required) — delegated auth
 *   scs-user      Display name shown in the widget
 *   scs-user-id   Subject (sub claim) of the user
 *
 * The component renders its entire UI inside a Shadow Root, so host CSS
 * neither leaks in nor ours leaks out. It communicates with the host only
 * through composed CustomEvents ("scs:*"), defined in @scs/shared.
 */
const TAG = 'scs-todo-app';

interface ScsWebConfig {
  bffUrl: string;
  token: string;
  host: ScsHostInfo;
}

function readConfig(el: HTMLElement): ScsWebConfig | { error: string } {
  const bffUrl = el.getAttribute('scs-web-url') ?? '';
  const token = el.getAttribute('scs-token') ?? '';
  if (!bffUrl) return { error: 'Missing scs-web-url attribute (BFF origin).' };
  if (!token) return { error: 'Missing scs-token attribute (host-issued JWT).' };
  return {
    bffUrl,
    token,
    host: {
      name: el.getAttribute('scs-user') ?? 'guest',
      userId: el.getAttribute('scs-user-id') ?? 'unknown',
    },
  };
}

class ScsTodoAppElement extends HTMLElement {
  private root: Root | null = null;
  private container: HTMLDivElement | null = null;
  private renderedFor = '';

  static readonly observedAttributes = ['scs-web-url', 'scs-token', 'scs-user', 'scs-user-id'];

  connectedCallback(): void {
    if (!this.shadowRoot) {
      this.attachShadow({ mode: 'open' });
    }
    const shadow = this.shadowRoot!;
    shadow.innerHTML = `<style>${SCS_STYLES}</style><div class="scs-mount"></div>`;
    this.container = shadow.querySelector('.scs-mount');
    this.render();
  }

  disconnectedCallback(): void {
    this.root?.unmount();
    this.root = null;
    this.renderedFor = '';
  }

  /** Re-renders when the host updates the configuration attributes. */
  attributeChangedCallback(): void {
    if (this.isConnected) this.render();
  }

  private render(): void {
    const container = this.container;
    if (!container) return;

    const signature = JSON.stringify(readConfig(this));
    if (signature === this.renderedFor && this.root) return;
    this.renderedFor = signature;

    this.root?.unmount();
    this.root = null;
    container.innerHTML = '';

    const config = readConfig(this);
    if ('error' in config) {
      const card = document.createElement('div');
      card.className = 'scs-card';
      const errorEl = document.createElement('p');
      errorEl.className = 'scs-error';
      errorEl.setAttribute('role', 'alert');
      errorEl.textContent = config.error;
      card.appendChild(errorEl);
      container.appendChild(card);
      this.dispatchEvent(
        new CustomEvent(SCS_EVENTS.error, {
          bubbles: true,
          composed: true,
          detail: { message: config.error },
        }),
      );
      return;
    }

    const api = new ScsApi(config.bffUrl, config.token);
    this.root = createRoot(container);
    this.root.render(<TodoApp api={api} host={config.host} />);
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'scs-todo-app': ScsTodoAppElement;
  }
}

if (!customElements.get(TAG)) {
  customElements.define(TAG, ScsTodoAppElement);
}

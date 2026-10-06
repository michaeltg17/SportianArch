import type { CreateTodoRequest, DashboardResponse, TodoDto } from '@scs/shared';

export class ScsApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ScsApiError';
  }
}

/**
 * Talks to the SCS BFF only. The widget has exactly one origin to trust.
 * Every request carries the host-issued JWT as a bearer token (delegated auth).
 */
export class ScsApi {
  constructor(
    private readonly bffUrl: string,
    private readonly token: string,
  ) {}

  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(`${this.bffUrl.replace(/\/$/, '')}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${this.token}`,
      },
    });

    if (!response.ok) {
      let message = `Request failed (HTTP ${response.status})`;
      try {
        const body = (await response.json()) as { error?: string };
        if (body?.error) message = body.error;
      } catch {
        // non-JSON error body: keep the default message
      }
      throw new ScsApiError(response.status, message);
    }

    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  }

  getDashboard(): Promise<DashboardResponse> {
    return this.request<DashboardResponse>('/api/v1/dashboard');
  }

  createTodo(body: CreateTodoRequest): Promise<TodoDto> {
    return this.request<TodoDto>('/api/v1/todos', {
      method: 'POST',
      body: JSON.stringify(body),
    });
  }

  completeTodo(id: string): Promise<TodoDto> {
    return this.request<TodoDto>(`/api/v1/todos/${id}/complete`, { method: 'POST' });
  }

  deleteTodo(id: string): Promise<void> {
    return this.request<void>(`/api/v1/todos/${id}`, { method: 'DELETE' });
  }
}

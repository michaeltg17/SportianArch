/**
 * Shared contracts for the Todo SCS.
 *
 * This package is the single source of truth for:
 *  - REST API DTOs (the versioned /api/v1 contract)
 *  - The RabbitMQ integration event payload
 *  - The host <-> SCS web component custom event channel
 *
 * Consumed by: @scs/web (widget), @scs/host (platform), e2e tests (conceptually).
 * The .NET services implement the same shapes (camelCase JSON) but are decoupled
 * on purpose: only the JSON contract binds them.
 */

// ---------------------------------------------------------------------------
// Todo bounded context (scs-todos-api)
// ---------------------------------------------------------------------------

export interface TodoDto {
  id: string;
  title: string;
  description: string | null;
  isCompleted: boolean;
  completedAt: string | null;
  createdAt: string;
}

export interface CreateTodoRequest {
  title: string;
  description?: string;
}

// ---------------------------------------------------------------------------
// Notification bounded context (scs-notifications-api)
// ---------------------------------------------------------------------------

export interface NotificationDto {
  id: string;
  type: string;
  message: string;
  sourceId: string | null;
  createdAt: string;
}

// ---------------------------------------------------------------------------
// BFF (scs-bff) - the single origin the widget talks to
// ---------------------------------------------------------------------------

export interface DashboardResponse {
  todos: TodoDto[];
  notifications: NotificationDto[];
  warnings: string[];
  serverTime: string;
}

// ---------------------------------------------------------------------------
// Integration event (RabbitMQ): published by scs-todos-api on the
// "scs.todos" topic exchange, routing key "todo.completed".
// ---------------------------------------------------------------------------

export interface TodoCompletedEvent {
  messageId: string;
  todoId: string;
  title: string;
  userId: string;
  completedAt: string;
}

export const MQ = {
  exchange: 'scs.todos',
  routingKeyTodoCompleted: 'todo.completed',
  consumerQueue: 'notifications.todo.completed',
} as const;

// ---------------------------------------------------------------------------
// Auth contract (delegated JWT minted by the host platform)
// ---------------------------------------------------------------------------

export const AUTH = {
  issuer: 'sportian-host',
  audience: 'scs',
  /** Demo-only shared HS256 secret. In production the host IdP signs with an
   *  asymmetric key and SCS APIs validate via JWKS (see docs/ARCHITECTURE.md). */
  demoSecret: 'scs-demo-shared-secret-0123456789-0123456789',
} as const;

// ---------------------------------------------------------------------------
// Host <-> SCS web component channel (CustomEvents named "scs:<name>")
// ---------------------------------------------------------------------------

export interface ScsReadyPayload {
  name: string;
  version: string;
}

export interface ScsTodoAddedPayload {
  todoId: string;
  title: string;
}

export interface ScsTodoCompletedPayload {
  todoId: string;
  title: string;
}

export interface ScsErrorPayload {
  message: string;
  status?: number;
}

/**
 * Maps each custom event NAME (as dispatched, "scs:*") to its payload shape.
 * `keyof ScsEventPayloadMap` is therefore the set of valid event names.
 */
export interface ScsEventPayloadMap {
  'scs:ready': ScsReadyPayload;
  'scs:todo-added': ScsTodoAddedPayload;
  'scs:todo-completed': ScsTodoCompletedPayload;
  'scs:error': ScsErrorPayload;
}

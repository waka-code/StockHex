/**
 * La API responde los errores como ProblemDetails (RFC 7807). Aquí se traducen a
 * un error tipado para que un formulario pueda pintar el mensaje junto al campo
 * que lo causó en lugar de mostrar un cartel genérico.
 */
export interface ProblemPayload {
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly traceId?: string;
  /** Errores por campo, con la clave tal como la nombra la API (PascalCase). */
  readonly fieldErrors: Record<string, string[]>;

  constructor(status: number, payload: ProblemPayload | null, fallback: string) {
    super(payload?.detail || payload?.title || fallback);
    this.name = 'ApiError';
    this.status = status;
    this.code = payload?.code;
    this.traceId = payload?.traceId;
    this.fieldErrors = payload?.errors ?? {};
  }

  get isValidation(): boolean { return this.status === 400; }
  get isUnauthorized(): boolean { return this.status === 401; }
  get isForbidden(): boolean { return this.status === 403; }
  get isNotFound(): boolean { return this.status === 404; }
  get isConflict(): boolean { return this.status === 409; }
  get isRateLimited(): boolean { return this.status === 429; }

  /**
   * Mensaje del campo indicado. Se busca sin distinguir mayúsculas porque la API
   * usa PascalCase ("Name") y los formularios camelCase ("name").
   */
  fieldError(field: string): string | undefined {
    const key = Object.keys(this.fieldErrors)
      .find((k) => k.toLowerCase() === field.toLowerCase());
    return key ? this.fieldErrors[key]?.[0] : undefined;
  }

  /** Todos los mensajes de campo, para un resumen. */
  get allFieldErrors(): string[] {
    return Object.values(this.fieldErrors).flat();
  }
}

/** Error de red o de CORS: la petición no llegó a obtener respuesta. */
export class NetworkError extends Error {
  constructor(cause?: unknown) {
    super('No se pudo conectar con el servidor. Revisa tu conexión.');
    this.name = 'NetworkError';
    this.cause = cause;
  }
}

export async function readProblem(response: Response): Promise<ProblemPayload | null> {
  const type = response.headers.get('content-type') ?? '';
  if (!type.includes('json')) return null;
  try {
    return (await response.json()) as ProblemPayload;
  } catch {
    // Un cuerpo vacío o mal formado no debe tapar el status real.
    return null;
  }
}

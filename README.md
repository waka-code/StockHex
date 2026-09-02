# 📦 StockHex — Sistema de Gestión de Inventario

Sistema de gestión de inventario con auditoría completa de movimientos de stock,
autenticación JWT y autorización por roles.

- **API REST** en .NET 8, arquitectura limpia en cuatro capas.
- **Interfaz web** en React + Vite + TypeScript → [`StockHex.Web/`](StockHex.Web/README.md)

```
[██████████] MVP funcional  ·  131/131 tests de API  ·  verificado en navegador real
```

---

## Idea central

**El stock nunca se edita directamente.** La única forma de cambiarlo es registrar un
movimiento en `POST /api/inventory-movements`. El movimiento y el nuevo stock se
guardan en un solo `SaveChanges`, así que el historial y el stock no pueden
desincronizarse, y cada cambio queda atribuido al usuario que lo hizo.

---

## Stack

| Componente | Tecnología |
|---|---|
| Framework | .NET 8 / ASP.NET Core |
| Base de datos | SQL Server 2022 |
| ORM | Entity Framework Core 8 |
| Autenticación | JWT Bearer + BCrypt |
| Validación | FluentValidation |
| Logging | Serilog |
| Tests | xUnit + FluentAssertions |
| Frontend | React 19 + Vite + TypeScript |
| Estado del servidor | TanStack Query |
| Contenedores | Docker Compose |

---

## Inicio rápido

### Todo con un solo comando

```bash
cp .env.example .env      # y edita los valores
docker compose up -d --build
```

Y eso es todo: **http://localhost:8080**. Levanta los tres servicios —SQL Server,
la API y el frontend— aplica las migraciones y crea el administrador inicial.

| Ruta | Qué es |
|---|---|
| http://localhost:8080 | La aplicación |
| http://localhost:8080/swagger | Documentación de la API |
| http://localhost:8080/health/ready | Salud, incluida la base de datos |

nginx sirve el frontend y hace de **proxy de `/api`** hacia la API, así que todo
vive en un solo origen: el navegador nunca hace una petición cruzada y **no hay
CORS que configurar**.

El `.env` es obligatorio: el compose no arranca sin `MSSQL_SA_PASSWORD`,
`JWT_KEY` (mínimo 32 caracteres) y `SEED_ADMIN_PASSWORD`. Genera una clave con:

```bash
openssl rand -base64 48
```

```bash
docker compose logs -f api      # ver los logs de la API
docker compose ps               # estado y salud de cada servicio
docker compose down             # bajar todo
docker compose down -v          # bajar y borrar la base de datos
```

### Desarrollo: el frontend con recarga en caliente

Con el stack arriba, para trabajar en la interfaz conviene el servidor de Vite:

```bash
cd StockHex.Web
cp .env.example .env      # VITE_API_URL apunta a la API
npm install
npm run dev               # http://localhost:5173
```

Aquí sí hay dos orígenes, así que el `.env` de la raíz ya trae
`CORS_ALLOWED_ORIGINS=http://localhost:5173`. Detalles en el
[README del frontend](StockHex.Web/README.md).

### La API en local, sin Docker

```bash
cd "StockHex API/StockHex API"
dotnet restore
dotnet run          # aplica migraciones y siembra el admin al arrancar
```

En `Development` se usa `appsettings.Development.json`, que ya trae valores locales.

### Tests

```bash
cd "StockHex API" && dotnet test     # 131 tests de la API

cd StockHex.Web
npx playwright install chromium      # una vez
npm run e2e:proxy                    # el despliegue: un origen, sin CORS, límites
APP_URL=http://localhost:8080 npm run e2e   # recorrido en navegador real
```

---

## Arquitectura

```
StockHex/
├── docker-compose.yml  los tres servicios: base de datos, API y frontend
├── .env.example        plantilla de configuración
├── StockHex API/       la API (detalle abajo)
├── StockHex.Web/       la interfaz web (ver su README)
└── .github/workflows/  CI

StockHex API/
├── Api/                          Controladores, middleware, mapeo Result → HTTP
│   ├── Controllers/
│   ├── Middleware/               Manejo global de excepciones → ProblemDetails
│   └── Extensions/               CORS, Swagger+JWT, ResultExtensions, Roles
├── Application/                  Casos de uso, DTOs, validadores
│   ├── UseCases/                 Un caso de uso = una clase
│   ├── DTOs/                     Request/Response segregados
│   ├── Validators/               FluentValidation
│   ├── Mappings/                 Entidad → DTO, explícito
│   └── Abstractions/             IPasswordHasher, ITokenService, ICurrentUser
├── Domain/                       Sin dependencias externas
│   ├── Entities/
│   ├── Enums/                    UserRole, MovementType
│   ├── Common/                   Result<T>, Error, PagedResult<T>, PageRequest
│   ├── Exceptions/
│   └── Interfaces/               Repositorios + IUnitOfWork
└── Infrastructure/
    ├── Persistence/              DbContext, configuraciones EF, seeder
    ├── Repositories/
    └── Security/                 JWT, BCrypt, CurrentUser
```

**Reglas que sostienen la separación:**

- `Domain` no referencia nada de fuera.
- Los repositorios sólo marcan cambios; la use case decide cuándo confirmar vía `IUnitOfWork`.
- Las use cases devuelven `Result<T>`; nunca conocen HTTP.
- Los controladores no tienen lógica: traducen `Result<T>` a status codes.

---

## Manejo de errores

Todas las respuestas de error son **ProblemDetails (RFC 7807)** con
`Content-Type: application/problem+json`.

| Situación | Status |
|---|---|
| Validación de entrada | `400` con detalle por campo |
| Sin token o token inválido | `401` |
| Rol insuficiente | `403` |
| Recurso inexistente | `404` |
| Duplicado, stock insuficiente, borrado que rompe integridad | `409` |
| Error inesperado | `500` con `traceId` (sin stack trace en producción) |

---

## Roles

| Rol | Permisos |
|---|---|
| `Admin` | Todo, incluida la gestión de usuarios |
| `Manager` | Catálogo, clientes, proveedores, movimientos, reportes |
| `Operator` | Consulta y registro de movimientos |

El auto-registro (`POST /api/auth/register`) **siempre** crea `Operator`: el rol no
se lee del body, así nadie puede registrarse como administrador.

---

## Endpoints

### Autenticación — `/api/auth`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/login` | público | Devuelve access token + refresh token + perfil |
| POST | `/register` | público | Auto-registro como `Operator` |
| POST | `/refresh` | público | Canjea el refresh token por un par nuevo |
| POST | `/logout` | autenticado | Revoca el refresh token (`allSessions` cierra todas) |
| GET | `/me` | autenticado | Perfil del portador del token |

**Sesiones.** El access token dura 60 minutos; el refresh token, 14 días. Cada canje
**rota** el refresco: el usado queda revocado y se emite otro. Si aparece un token ya
rotado se asume robo y se invalida la cadena completa de esa sesión. En la base sólo
se guarda el SHA-256 del token, nunca el valor en claro.

**Límite de intentos.** `/api/auth` acepta 10 peticiones por minuto y por IP
(configurable en `RateLimiting`). Al superarlo responde `429` con cabecera `Retry-After`.

> Detrás de un proxy inverso hay que activar `ForwardedHeaders:Enabled` y declarar el
> proxy en `KnownProxies` o `KnownNetworks`. Sin eso, `RemoteIpAddress` es la del proxy
> y **todos los usuarios comparten un mismo cupo**: un solo atacante bloquearía el login
> de todo el mundo. Viene desactivado por defecto porque confiar en la cabecera sin
> proxy delante permitiría a cualquier cliente falsear su IP y eludir el límite.

**Mantenimiento.** Un servicio en background purga a diario los refresh tokens
caducados o revocados hace más de 30 días; sin eso la tabla crece indefinidamente.

### Movimientos de inventario — `/api/inventory-movements`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| GET | `/` | autenticado | Historial paginado y filtrable |
| GET | `/{id}` | autenticado | Un movimiento |
| POST | `/` | autenticado | Registra movimiento y ajusta stock |
| POST | `/{id}/reverse` | Admin, Manager | Corrige un movimiento registrando su inverso |

Filtros de `GET`: `productId`, `clientId`, `supplierId`, `userId`, `movementType`, `from`, `to`, `search`, `page`, `pageSize`.

**Contraparte.** `supplierId` y `clientId` son la contraparte del movimiento y son
**mutuamente excluyentes**. No están atados al tipo, porque ambas combinaciones son
legítimas: una devolución a proveedor es una salida con proveedor, y una devolución
de cliente es una entrada con cliente. En una entrada sin contraparte explícita se
hereda el proveedor del producto.

Tipos de movimiento:

| Tipo | Efecto sobre el stock |
|---|---|
| `In` | Suma `quantity` |
| `Out` | Resta `quantity`; falla con `409` si el stock no alcanza |
| `Adjustment` | Fija el stock en `quantity` (para conteos físicos) |

**Correcciones.** Un movimiento equivocado no se edita ni se borra: `POST /{id}/reverse`
registra el movimiento inverso. Invierte la **variación neta** del original, así que es
exacto para entradas, salidas y ajustes, y sigue siendo correcto aunque haya habido
movimientos posteriores. Conserva la contraparte del original. Un movimiento sólo puede
revertirse una vez, y una reversión no se puede revertir.

**Concurrencia.** `Product.RowVersion` impide que dos movimientos simultáneos pisen el
mismo stock. Para que esa protección no se traduzca en rechazos, las operaciones que
mueven stock se reintentan hasta 5 veces releyendo el producto, con espera y jitter.
Medido: 25 movimientos en paralelo sobre un mismo producto terminan en 25 éxitos y 0
conflictos, con el stock exactamente igual al número de movimientos registrados.

### Productos — `/api/products`

| Método | Ruta | Rol |
|---|---|---|
| GET | `/` | autenticado |
| GET | `/{id}` | autenticado |
| POST | `/` | Admin, Manager |
| PUT | `/{id}` | Admin, Manager |
| DELETE | `/{id}` | Admin, Manager |

Filtros: `categoryId`, `supplierId`, `isActive`, `lowStockOnly`, `search`, `page`, `pageSize`.

El producto se crea con **stock 0** y `PUT` **no** modifica el stock: para eso están los movimientos.
`DELETE` sobre un producto con historial lo **desactiva** en lugar de borrarlo.

### Categorías, Proveedores, Clientes

`/api/categories`, `/api/suppliers`, `/api/clients` — CRUD completo, mismo esquema:
lectura para autenticados, escritura para `Admin` y `Manager`. No se puede eliminar
una entidad que tenga registros dependientes (responde `409`).

### Usuarios — `/api/users`

| Método | Ruta | Rol |
|---|---|---|
| GET | `/` | Admin |
| GET | `/{id}` | Admin |
| POST | `/` | Admin |
| PUT | `/{id}` | Admin |
| DELETE | `/{id}` | Admin |
| POST | `/me/change-password` | autenticado |

Guardias: no se puede degradar ni desactivar al único administrador, ni eliminar la
propia cuenta. Las respuestas **nunca** incluyen el hash de la contraseña.

### Reportes — `/api/reports`

| Ruta | Descripción |
|---|---|
| `/inventory-summary` | Totales de productos, stock bajo y valorización |
| `/low-stock` | Productos en o bajo su mínimo, paginado y ordenado por déficit |
| `/movement-summary?from=&to=` | Actividad por tipo de movimiento (30 días por defecto) |

### Salud

| Ruta | Descripción |
|---|---|
| `/health/live` | Liveness: no toca la base |
| `/health/ready` | Readiness: verifica la conexión a la base |

---

## Modelo de datos

```
Category ─┬─< Product >─┬─ Supplier
          │             │           │
          │             └─< InventoryMovement >─┬─ User ─< RefreshToken
          │                                     ├─ Client
          │                                     └─ (auto-referencia: reversión)
```

| Entidad | Campos |
|---|---|
| `Category` | Id, Name*, Description, CreatedAt, UpdatedAt |
| `Supplier` | Id, Name*, Description, PhoneNumber, Email, CreatedAt, UpdatedAt |
| `Client` | Id, Name, Address, PhoneNumber, Email*, CreatedAt, UpdatedAt |
| `Product` | Id, Name, Description, Sku*, Price, StockQuantity, MinimumStock, IsActive, CategoryId, SupplierId, RowVersion, CreatedAt, UpdatedAt |
| `User` | Id, Name, Email*, PasswordHash, Role, IsActive, EmailConfirmed, CreatedAt, UpdatedAt, LastLoginAt |
| `InventoryMovement` | Id, MovementType, ProductId, Quantity, UnitPrice, StockBefore, StockAfter, MovementDate, UserId, ClientId, SupplierId, ReversalOfMovementId, Comment |
| `RefreshToken` | Id, TokenHash*, UserId, ExpiresAt, CreatedAt, RevokedAt, RevokedReason, ReplacedByTokenId |

Los errores de negocio esperados viajan como `Result<T>` desde las use cases; el
middleware de excepciones cubre sólo lo que `Result` no puede expresar (violación de
constraint por carrera, conflicto de concurrencia agotado, fallo inesperado). Por eso
no hay una jerarquía de excepciones de dominio: sería una segunda vía para lo mismo.

`*` = índice único. Todas las FK son `Guid` con `DeleteBehavior.Restrict` (salvo
`Product.SupplierId`, que es `SetNull`), de modo que el historial de auditoría
nunca desaparece por un borrado en cascada.

`Product.RowVersion` es un token de concurrencia: dos movimientos simultáneos sobre
el mismo producto no pueden pisarse el stock, el segundo recibe `409`.

`InventoryMovement.ReversalOfMovementId` tiene un índice único filtrado: la base
garantiza que un movimiento no pueda revertirse dos veces, no sólo la comprobación
previa del caso de uso. `RefreshToken` es la única relación en cascada del modelo:
los tokens de un usuario borrado no son auditoría.

---

## Configuración

Toda clave se puede pasar por variable de entorno usando `__` como separador
(`Jwt__Key`, `ConnectionStrings__DefaultConnection`).

| Clave | Obligatoria | Descripción |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | sí | Cadena de conexión a SQL Server |
| `Jwt:Key` | sí | Clave de firma, mínimo 32 caracteres |
| `Jwt:Issuer` / `Jwt:Audience` | sí | Emisor y audiencia del token |
| `Jwt:AccessTokenMinutes` | no | Vigencia del access token (por defecto 60) |
| `Jwt:RefreshTokenDays` | no | Vigencia del refresh token (por defecto 14) |
| `RateLimiting:AuthPermitLimit` | no | Peticiones a `/api/auth` por ventana (por defecto 10) |
| `RateLimiting:AuthWindowSeconds` | no | Duración de la ventana en segundos (por defecto 60) |
| `ForwardedHeaders:Enabled` | no | Confiar en `X-Forwarded-For` (por defecto `false`) |
| `ForwardedHeaders:KnownProxies` | no | IPs de los proxies de confianza |
| `ForwardedHeaders:KnownNetworks` | no | Redes de confianza en CIDR, p. ej. `10.0.0.0/8` |
| `RefreshTokenCleanup:IntervalHours` | no | Frecuencia de la purga de tokens (por defecto 24) |
| `RefreshTokenCleanup:RetentionDays` | no | Margen antes de borrar un token (por defecto 30) |
| `Cors:AllowedOrigins` | no | Orígenes permitidos, arreglo o lista separada por `;`; vacío = cualquiera sin credenciales |
| `Swagger:Enabled` | no | Exponer Swagger UI (por defecto: sólo en `Development`) |
| `ForwardedHeaders:Enabled` | no | Confiar en `X-Forwarded-For` (el compose lo activa para nginx) |
| `Database:MigrateOnStartup` | no | Aplicar migraciones al arrancar (por defecto `true`) |
| `Database:MigrationTimeoutSeconds` | no | Límite de espera por la base (por defecto 60) |
| `Seed:AdminEmail` / `Seed:AdminPassword` | no | Administrador inicial, sólo si no existe ninguno |

`Jwt:Key` se valida al arrancar: si falta o es corta, la aplicación **no levanta**
en lugar de emitir tokens inseguros.

La API arranca aunque la base todavía no responda; el estado se consulta en
`/health/ready`.

**No hay secretos en el repositorio.** `appsettings.json` los deja vacíos,
`appsettings.Development.json` sólo tiene valores locales y `.env` está en `.gitignore`.

---

## Migraciones

```bash
cd "StockHex API/StockHex API"

dotnet ef migrations add NombreMigracion
dotnet ef database update
dotnet ef migrations remove
```

Se aplican automáticamente al arrancar salvo que se ponga `Database:MigrateOnStartup=false`.

---

## Tests

121 tests, todos en verde.

| Suite | Cubre |
|---|---|
| `UseCases/CreateMovementTests` | Semántica de `In`/`Out`/`Adjustment`, stock insuficiente, producto inactivo, acumulación |
| `UseCases/ReverseMovementTests` | Reversión de cada tipo, doble reversión, reversión de reversión, stock insuficiente |
| `UseCases/RefreshTokenTests` | Rotación, detección de reutilización, expiración, logout y logout global |
| `UseCases/MovementSupplierTests` | Contraparte del movimiento, herencia desde el producto, invariante |
| `UseCases/ConcurrencyRetryTests` | Reintento por conflicto, tope de intentos, qué no se reintenta |
| `UseCases/RefreshTokenPurgeTests` | Qué se borra y qué se conserva en la purga |
| `UseCases/ConfigurationGuardTests` | Cadena de conexión ausente o vacía rechazada al arrancar |
| `UseCases/CreateProductTests` | SKU duplicado, normalización, FK inexistentes, stock inicial |
| `UseCases/DeleteEntityGuardTests` | Borrados que romperían integridad o perderían auditoría |
| `UseCases/LoginTests` | Credenciales, cuenta inactiva, no filtrar qué emails existen |
| `UseCases/UserGuardTests` | Último administrador, auto-eliminación, cambio de contraseña |
| `Security/PasswordHasherTests` | Roundtrip BCrypt, salt por hash, hashes corruptos |
| `Security/TokenServiceTests` | Claims del JWT, `ClaimTypes.Role`, `jti` único |
| `Common/PageRequestTests` | Acotado de paginación, `Result<T>` |
| `Integration/ApiEndpointsTests` | Pipeline HTTP completo: auth, roles, validación, flujo de inventario |
| `Integration/AuthLifecycleTests` | Login, refresh, reutilización, logout y reversión por HTTP |
| `Integration/RateLimitingTests` | 429 al superar el límite, `Retry-After`, alcance de la política |

Los tests de integración levantan la API real con `WebApplicationFactory` y el
proveedor InMemory de EF.

---

## Despliegue

El mismo `docker-compose.yml` sirve para un servidor real; sólo cambian las
variables:

```bash
JWT_KEY=$(openssl rand -base64 48)
SWAGGER_ENABLED=false            # no expongas Swagger en producción
MSSQL_SA_PASSWORD=…              # una contraseña de verdad
SEED_ADMIN_PASSWORD=…            # cámbiala tras el primer ingreso
WEB_PORT=80
```

La imagen del frontend **no lleva la URL de la API compilada dentro**: se resuelve
al arrancar el contenedor, así que la misma imagen sirve para cualquier entorno.
Por omisión usa el mismo origen y pasa por el proxy; `WEB_API_URL` la apunta a otro
dominio si hiciera falta.

Falta un terminador TLS delante (nginx, Traefik o el balanceador del proveedor):
los contenedores hablan HTTP en la red interna.

## Pendiente (post-MVP)

- HTTPS/TLS delante del contenedor
- Confirmación de email y recuperación de contraseña (`EmailConfirmed` existe pero no hay flujo)
- Logs persistentes: Serilog sólo escribe a consola
- `CreatedBy` / `UpdatedBy` en el catálogo (los movimientos ya guardan el autor)
- Caching (Redis o memoria) en los listados

---

## Licencia

Sin licencia definida. Añade un `LICENSE` antes de publicar.

## Desarrollador

**Waddini** — Arquitectura & Development

# 📦 StockHex — Sistema de Gestión de Inventario

Sistema de gestión de inventario con auditoría completa de movimientos de stock,
autenticación JWT y autorización por roles.

- **API REST** en .NET 8, arquitectura limpia en cuatro capas.
- **Interfaz web** en React + Vite + TypeScript → [`StockHex.Web/`](StockHex.Web/README.md)

```
[██████████] MVP funcional  ·  194/194 tests de API  ·  418 comprobaciones en navegador real
```

> **Antes de implementar cualquier cosa, lee [`CLAUDE.md`](CLAUDE.md)**: son las
> reglas del proyecto y la fuente de verdad. Incluye el estado de cumplimiento
> declarado, con lo que hoy **no** cumple.

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
cd "StockHex API" && dotnet test     # 194 tests de la API

cd StockHex.Web
npx playwright install chromium      # una vez
npm run e2e                          # las 6 suites en navegador real
npm run e2e:proxy                    # el despliegue: un origen, sin CORS, límites
```

`npm run e2e` **espera entre suite y suite**: el limitador de `/api/auth` acepta 10
intentos por minuto y una tanda completa hace más de 10 logins, así que encadenarlas
sin pausa dejaba a las últimas sin poder entrar. Por eso la tanda tarda varios
minutos; una suite suelta (`npm run e2e:filters`) es inmediata.

La sexta suite, **`e2e:stress`**, recorre las diez pantallas una a una y las maltrata:
teclea letra a letra, hace doble clic por impaciencia, cierra con Escape, navega con
el botón atrás, refresca a media faena, manipula la URL, abre seis pestañas a la vez y
prueba seis tamaños de ventana entre 1920px y 390px. Cada pantalla se audita sola —sin
errores de consola, sin excepciones, sin 5xx, sin desbordes horizontales del documento
y sin filas apiladas— y lo que crea lo borra al terminar.

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
│   └── Extensions/               CORS, Swagger+JWT, ResultExtensions,
│                                 RequirePermission, rate limiting, forwarded headers
├── Application/                  Casos de uso, DTOs, validadores
│   ├── UseCases/                 Un caso de uso = una clase
│   ├── DTOs/                     Request/Response segregados
│   ├── Validators/               FluentValidation
│   ├── Mappings/                 Entidad → DTO, explícito
│   └── Abstractions/             IPasswordHasher, ITokenService, ICurrentUser
├── Domain/                       Sin dependencias externas
│   ├── Entities/
│   ├── Enums/                    MovementType
│   ├── Authorization/            Permissions.cs — el catálogo, única fuente
│   ├── Common/                   Result<T>, Error, PagedResult<T>, PageRequest
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
| Permiso insuficiente | `403` |
| Recurso inexistente | `404` |
| Duplicado, stock insuficiente, borrado que rompe integridad | `409` |
| Error inesperado | `500` con `traceId` (sin stack trace en producción) |

---

## Roles y permisos

Los **roles son datos**: se crean, editan y eliminan desde la interfaz. Los
**permisos tienen una sola fuente**, el código: 31 claves en 9 módulos declaradas en
`Domain/Authorization/Permissions.cs`. No hay tabla de permisos: un permiso se
declara junto al código que lo hace valer, así que agregar uno es un cambio de
código, igual que el endpoint que lo comprueba.

```
dashboard.view    products.view      categories.view     users.view
                  products.create    categories.create   users.create
movements.view    products.edit      categories.edit     users.edit
movements.create  products.delete    categories.delete   users.delete
movements.reverse                                        users.change_password
                  suppliers.view     clients.view
reports.view      suppliers.create   clients.create      roles.view
reports.export    suppliers.edit     clients.edit        roles.create
                  suppliers.delete   clients.delete      roles.edit
                                                         roles.delete
```

Nueve módulos: `dashboard`, `products`, `movements`, `reports`, `categories`,
`suppliers`, `clients`, `users` y `roles`. Los tres permisos **especiales** —los que
sólo tiene un módulo— son `movements.reverse`, `reports.export` y
`users.change_password`.

La migración inicial crea tres roles equivalentes al modelo anterior, y se pueden
editar o complementar con los que haga falta:

| Rol | Permisos | Notas |
|---|---|---|
| `Administrador` | 31 de 31 | Rol de **sistema**: no se elimina ni se queda sin los permisos críticos |
| `Jefe de bodega` | 22 | Todo menos usuarios y roles |
| `Bodeguero` | 5 | Dashboard, productos (ver), movimientos (ver y crear), reportes |

Cada endpoint exige su permiso con `[RequirePermission("x.y")]` y responde `403` si
falta. El frontend usa los mismos permisos para no ofrecer acciones que van a fallar,
**nunca como control de acceso**.

Dos claves del catálogo no cuelgan de un endpoint, y conviene saber por qué:

- **`dashboard.view`** sólo habilita la pantalla de inicio. No hay un endpoint de
  dashboard: la pantalla compone `/api/reports/*` y `/api/inventory-movements`, y
  cada uno exige su propio permiso, así que la autorización real sigue en la API.
- **`reports.export`** está declarada y concedida a dos roles, pero **hoy no la
  comprueba nada**: el endpoint de exportación todavía no existe (ver
  [Pendiente](#pendiente-post-mvp)). Es la única clave del catálogo sin uso.

**Cuándo surte efecto un cambio de permisos.** El JWT lleva sólo el id del rol, no la
lista de permisos: si la llevara, quitarle un permiso a alguien no surtiría efecto
hasta que su token se renovara, hasta 60 minutos después. Se resuelven por petición
con una caché de 30 segundos que se invalida al editar el rol, así que el cambio se
aplica de inmediato y sin cerrar la sesión de nadie.

El auto-registro (`POST /api/auth/register`) usa el rol configurado en
`Auth:RegistrationRoleName` (por omisión `Bodeguero`). El rol nunca se lee del body,
así nadie puede registrarse con permisos elevados.

---

## Endpoints

### Autenticación — `/api/auth`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/login` | público | Devuelve access token + refresh token + perfil |
| POST | `/register` | público | Auto-registro con el rol de `Auth:RegistrationRoleName` (por omisión `Bodeguero`) |
| POST | `/refresh` | público | Canjea el refresh token por un par nuevo |
| POST | `/logout` | autenticado | Revoca el refresh token (`allSessions` cierra todas) |
| GET | `/me` | autenticado | Perfil del portador del token, con sus permisos efectivos |

**Sesiones.** El access token dura 60 minutos y el refresh token 14 días (en
`Development` el access sube a 480 minutos para no renovar cada rato mientras se
desarrolla). Cada canje
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

| Método | Ruta | Permiso | Descripción |
|---|---|---|---|
| GET | `/` | `movements.view` | Historial paginado y filtrable |
| GET | `/{id}` | `movements.view` | Un movimiento |
| POST | `/` | `movements.create` | Registra movimiento y ajusta stock |
| POST | `/{id}/reverse` | `movements.reverse` | Corrige un movimiento registrando su inverso |

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
mueven stock se reintentan hasta 8 veces releyendo el producto, con espera
exponencial y jitter.
Verificado como test contra SQL Server real: 25 movimientos en paralelo sobre un
mismo producto terminan en 25 éxitos y 0
conflictos, con el stock exactamente igual al número de movimientos registrados.

### Productos — `/api/products`

| Método | Ruta | Permiso |
|---|---|---|
| GET | `/` | `products.view` |
| GET | `/{id}` | `products.view` |
| POST | `/` | `products.create` |
| PUT | `/{id}` | `products.edit` |
| DELETE | `/{id}` | `products.delete` |

Filtros: `categoryId`, `supplierId`, `isActive`, `lowStockOnly`, `search`, `page`, `pageSize`.

El producto se crea con **stock 0** y `PUT` **no** modifica el stock: para eso están los movimientos.
`DELETE` sobre un producto con historial lo **desactiva** en lugar de borrarlo.

### Categorías, Proveedores, Clientes

`/api/categories`, `/api/suppliers`, `/api/clients` — CRUD completo, mismo esquema:
cada verbo exige el permiso de su módulo (`categories.view`, `categories.create`,
`categories.edit`, `categories.delete`, y lo equivalente en los otros dos). No se
puede eliminar una entidad que tenga registros dependientes (responde `409`).

### Usuarios — `/api/users`

| Método | Ruta | Permiso |
|---|---|---|
| GET | `/` | `users.view` |
| GET | `/{id}` | `users.view` |
| POST | `/` | `users.create` |
| PUT | `/{id}` | `users.edit` |
| DELETE | `/{id}` | `users.delete` |
| POST | `/{id}/reset-password` | `users.change_password` |
| POST | `/me/change-password` | autenticado |

Filtros de `GET`: `roleId`, `isActive`, `search`, `page`, `pageSize`.

`reset-password` cambia la contraseña de **otro** usuario: no pide la actual, porque
quien la cambia no la conoce, y admite revocar sus sesiones para que tenga que entrar
con la nueva. La propia contraseña se cambia en `me/change-password`, que sí exige la
actual.

**`me/change-password` cierra todas las sesiones y devuelve un `AuthResponse`.** Es
lo que hace alguien que cree que le robaron la cuenta: dejar vivos los refrescos
anteriores lo dejaría dentro hasta catorce días más. Como eso también mataría la
sesión de quien está cambiando la contraseña, se emite un par nuevo en la misma
respuesta — **el cliente tiene que guardarlo**, o su refresco quedará revocado y caerá
al renovar. En el frontend eso lo hace `useAuth().changeOwnPassword(…)`, no la llamada
directa al endpoint.

Guardias: no se puede dejar el sistema **sin ningún usuario activo** con `roles.edit`
y `users.edit`, ni eliminar la propia cuenta. Las respuestas **nunca** incluyen el
hash de la contraseña.

**Desactivar o eliminar echa de inmediato.** El JWT no se puede revocar, así que cada
petición autenticada comprueba que la cuenta siga viva antes de mirar permisos
(`IActiveUserResolver`, caché de 30 s invalidada al desactivar). Sin eso, el access
token en curso seguiría abriendo la API hasta una hora después de la baja.

### Roles y permisos — `/api/roles` y `/api/permissions`

| Método | Ruta | Permiso |
|---|---|---|
| GET | `/api/permissions` | autenticado |
| GET | `/api/roles` | `roles.view` |
| GET | `/api/roles/{id}` | `roles.view` |
| POST | `/api/roles` | `roles.create` |
| PUT | `/api/roles/{id}` | `roles.edit` |
| DELETE | `/api/roles/{id}` | `roles.delete` |

`GET /api/permissions` devuelve el catálogo completo agrupado por módulo. Es la única
fuente: el frontend lo consume y no lo redeclara.

`PUT` reemplaza el conjunto completo de permisos del rol; una clave que no esté en el
catálogo se rechaza con `400`. Un rol de sistema no se elimina ni se queda sin los
permisos críticos, y uno con usuarios asignados tampoco se elimina (`409`).

**Nadie concede un permiso que él mismo no tiene** (`403`). Sin este guardia,
`roles.edit` alcanzaba para todo: bastaba editar el rol propio, marcar el resto de la
matriz y salir con permisos que nadie había concedido. Sólo se juzgan las **altas**,
de modo que quitar permisos —o renombrar un rol más poderoso que el propio reenviando
su lista tal cual— sigue funcionando.

**El rol de sistema concede siempre el catálogo completo.** No se guarda como una
foto: se reconcilia al arrancar contra `Permissions.All`. De lo contrario, agregar un
permiso al código dejaría sin él al rol descrito como «acceso total», y el endpoint
nuevo responderá `403` hasta al administrador hasta que alguien marcara la casilla
a mano. El mismo paso borra de todos los roles las claves que ya salieron del
catálogo: no conceden nada, pero la matriz las mostraba marcadas.

### Reportes — `/api/reports`

| Ruta | Descripción |
|---|---|
| `/inventory-summary` | Totales de productos, stock bajo y valorización |
| `/low-stock` | Productos en o bajo su mínimo, paginado y ordenado por déficit |
| `/movement-summary?from=&to=` | Actividad por tipo de movimiento (30 días por defecto) |

### Paginación

Todos los `GET` de listado aceptan `page`, `pageSize` y `search`, y **resuelven el
filtrado, la búsqueda y la paginación en SQL**: nunca devuelven el conjunto completo
para que el cliente lo recorte.

Los tamaños que la interfaz ofrece son **10, 15 y 25**, con **15** por omisión.
Están definidos una sola vez, en `PageRequest.AllowedPageSizes`
(`Domain/Common/PageRequest.cs`). Aparte de eso, `MaxPageSize = 100` es un techo
duro que acota cualquier petición para que nadie pueda pedir un listado ilimitado
escribiendo la URL.

La respuesta es un `PagedResponse<T>` con `items`, `page`, `pageSize`, `totalCount`,
`totalPages`, `hasPrevious` y `hasNext`.

### Salud

| Ruta | Descripción |
|---|---|
| `/health/live` | Liveness: no toca la base |
| `/health/ready` | Readiness: verifica la conexión a la base |

---

## Modelo de datos

```
Category ─┬─< Product >─┬─ Supplier
          │             │
          │             └─< InventoryMovement >─┬─ User >─ Role ─< RolePermission
          │                                     │         └─< RefreshToken
          │                                     ├─ Client
          │                                     └─ (auto-referencia: reversión)
```

| Entidad | Campos |
|---|---|
| `Category` | Id, Name*, Description, CreatedAt, UpdatedAt |
| `Supplier` | Id, Name*, Description, PhoneNumber, Email, CreatedAt, UpdatedAt |
| `Client` | Id, Name, Address, PhoneNumber, Email*, CreatedAt, UpdatedAt |
| `Product` | Id, Name, Description, Sku*, Price, StockQuantity, MinimumStock, IsActive, CategoryId, SupplierId, RowVersion, CreatedAt, UpdatedAt |
| `User` | Id, Name, Email*, PasswordHash, RoleId, IsActive, EmailConfirmed, CreatedAt, UpdatedAt, LastLoginAt |
| `Role` | Id, Name*, Description, IsSystem, CreatedAt, UpdatedAt |
| `RolePermission` | Id, RoleId, Permission (clave del catálogo; único por rol) |
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

`InventoryMovement.ReversalOfMovementId` tiene un índice único filtrado
(`WHERE [ReversalOfMovementId] IS NOT NULL`): la base garantiza que un movimiento no
pueda revertirse dos veces, no sólo la comprobación previa del caso de uso.

Las **dos únicas relaciones en cascada** son `User → RefreshToken` y
`Role → RolePermission`: ni los tokens de un usuario borrado ni los permisos de un rol
borrado son auditoría. Todo lo demás es `Restrict`, salvo `Product.SupplierId`, que es
`SetNull` para que dar de baja a un proveedor no arrastre sus productos.

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
| `ForwardedHeaders:Enabled` | no | Confiar en `X-Forwarded-For` (por defecto `false`; el compose lo activa para nginx) |
| `ForwardedHeaders:KnownProxies` | no | IPs de los proxies de confianza |
| `ForwardedHeaders:KnownNetworks` | no | Redes de confianza en CIDR, p. ej. `10.0.0.0/8` |
| `RefreshTokenCleanup:IntervalHours` | no | Frecuencia de la purga de tokens (por defecto 24) |
| `RefreshTokenCleanup:RetentionDays` | no | Margen antes de borrar un token (por defecto 30) |
| `Cors:AllowedOrigins` | no | Orígenes permitidos, arreglo o lista separada por `;`; vacío = cualquiera sin credenciales |
| `Swagger:Enabled` | no | Exponer Swagger UI (por defecto: sólo en `Development`) |
| `Database:MigrateOnStartup` | no | Aplicar migraciones al arrancar (por defecto `true`) |
| `Database:MigrationTimeoutSeconds` | no | Límite de espera por la base (por defecto 60) |
| `Seed:AdminEmail` / `Seed:AdminPassword` | no | Administrador inicial, sólo si no hay nadie con permiso para administrar roles |
| `Auth:RegistrationRoleName` | no | Rol del auto-registro público (por defecto `Bodeguero`) |

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

194 tests, todos en verde. Trece de ellos corren contra un SQL Server real que
levanta Testcontainers; sin Docker se omiten en lugar de fallar.

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
| `UseCases/ResetPasswordTests` | Restablecer la contraseña de otro usuario y revocar sus sesiones |
| `UseCases/RoleCrudTests` | Crear, editar y borrar roles; rol de sistema; rol con usuarios; permisos críticos; guardia de escalada |
| `UseCases/PermissionSyncTests` | El rol de sistema se deriva del catálogo; se purgan las claves obsoletas; idempotencia |
| `UseCases/CorsOriginsTests` | Lectura de orígenes: arreglo, lista con `;`, barra final, duplicados |
| `Authorization/PermissionCatalogTests` | El catálogo: claves únicas, módulos, permisos críticos, normalización |
| `Security/PasswordHasherTests` | Roundtrip BCrypt, salt por hash, hashes corruptos |
| `Security/TokenServiceTests` | Claims del JWT, `ClaimTypes.Role`, `jti` único |
| `Common/PageRequestTests` | Acotado de paginación, tamaños ofrecidos y su defecto, `Result<T>` |
| `Integration/ApiEndpointsTests` | Pipeline HTTP completo: auth, roles, validación, flujo de inventario |
| `Integration/AuthLifecycleTests` | Login, refresh, reutilización, logout y reversión por HTTP |
| `Integration/RateLimitingTests` | 429 al superar el límite, `Retry-After`, alcance de la política |
| `Database/SchemaConstraintTests` | **SQL Server real**: índices únicos, índice filtrado de reversión, colación del SKU, `Restrict` y cascada |
| `Database/ConcurrencyOnSqlServerTests` | **SQL Server real**: el `rowversion` que genera el motor, 25 movimientos en paralelo, salidas que compiten por el último stock |

Los tests de integración levantan la API real con `WebApplicationFactory` y el
proveedor InMemory de EF.

**Los de `Database/` corren contra un SQL Server real** que levanta Testcontainers
con la misma imagen del compose. Existen porque el proveedor InMemory **no es
relacional**: ignora los índices únicos, los índices filtrados, las colaciones y el
`rowversion`. Es decir, las garantías que el diseño delega a propósito en la base
—que un movimiento no se revierta dos veces, que dos SKU no colisionen, que dos
movimientos simultáneos no se pisen el stock— eran justo las que ningún test
cubría. Requieren Docker; sin él se **omiten** en lugar de fallar
(`[RequiresDockerFact]`), así que `dotnet test` sigue funcionando en una máquina que
no lo tenga. En CI siempre se ejecutan.

Lo primero que encontraron: el reintento de concurrencia se agotaba con 25
movimientos en paralelo. El tope de 5 intentos con espera lineal se veía holgado
porque InMemory nunca provocaba el conflicto que lo pone a prueba. Ahora son 8 con
espera exponencial, y el escenario del README es un test.

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

Lo que exigen las reglas del proyecto y todavía no está, en
[`CLAUDE.md`](CLAUDE.md#trabajo-pendiente-que-derivan-estas-reglas):

- **Selectores con búsqueda en el servidor** (hoy descargan un lote fijo de 100)
- **KPIs de Movimientos agregados en la API** (hoy se calculan sobre la página cargada)

Y el resto:

- **Exportación de reportes** — `reports.export` ya existe en el catálogo y está
  concedida a dos roles, pero no hay endpoint que la exija. Al implementarla hay que
  exigir **esa** clave, no declarar una nueva
- HTTPS/TLS delante del contenedor
- Confirmación de email y recuperación de contraseña (`EmailConfirmed` existe pero no hay flujo)
- Logs persistentes: Serilog sólo escribe a consola
- `CreatedBy` / `UpdatedBy` en el catálogo (los movimientos ya guardan el autor)
- Caching (Redis o memoria) en los listados

---

## ¿Encontraste un error? Rómpelo y cuéntamelo

Este proyecto se apoya en 194 tests de API y 418 comprobaciones en navegador, y aun
así **cada revisión seria ha encontrado algo**: una tabla que se comprimía en vez de
desplazarse en pantallas estrechas, un reintento de concurrencia que se agotaba con
25 movimientos en paralelo, un `?categoryId=` corrupto que dejaba el listado con un
aviso de error. Ninguno lo cazó la suite que ya existía. Si rompes algo, ese hallazgo
vale.

### Reportarlo

Abre un [issue](https://github.com/waka-code/StockHex/issues/new) con lo necesario
para **reproducirlo**, no sólo con lo que viste:

- **Qué hiciste**, paso a paso, y con qué rol.
- **Qué esperabas** y qué ocurrió en su lugar.
- **La evidencia**: la respuesta completa de la API —el cuerpo `ProblemDetails` trae
  `code` y, en los errores inesperados, el `traceId` con el que encontrarlo en el
  log—, la petición que la provocó, una captura si es visual, y el ancho de ventana
  si es de maquetación.
- **Dónde**: `docker compose up`, el servidor de Vite, o un despliegue propio.

Un reporte sin forma de reproducirlo se queda en anécdota.

### Mandar el arreglo

Los PR van contra **`main`**. La rama `gh-pages` es sólo el sitio publicado: ahí no
va código.

```bash
git checkout -b fix/lo-que-arreglas
```

El listón es el que ya cumple el resto del proyecto: **un test que falla antes de tu
cambio y pasa después**. No es burocracia — es lo que separa un arreglo de una
casualidad, y lo que impide que el mismo error vuelva dentro de seis meses. Según
dónde esté:

| Si el error está en… | El test va en… |
|---|---|
| Una regla de negocio o un caso de uso | `StockHex API.Tests/UseCases/` |
| El pipeline HTTP: autenticación, permisos, validación | `StockHex API.Tests/Integration/` |
| Algo que sólo el motor detecta: índices, `rowversion`, colaciones | `StockHex API.Tests/Database/`, contra SQL Server real |
| La interfaz o el comportamiento en el navegador | `StockHex.Web/e2e/`, reutilizando `harness.mjs` |

Antes de abrirlo, pasa lo mismo que pasa el CI:

```bash
cd "StockHex API" && dotnet build "StockHex API.sln" -c Release -warnaserror && dotnet test
cd StockHex.Web && npm run typecheck && npm run lint && npm run build
```

`-warnaserror` no es negociable: la solución compila con **0 warnings** y tiene que
seguir así. El CI además audita las dependencias y construye las imágenes del compose,
así que un PR que rompa cualquiera de esas cosas se pone en rojo solo.

Y lee [`CLAUDE.md`](CLAUDE.md) antes de escribir la primera línea: son las reglas del
proyecto, e incluyen el estado de cumplimiento declarado —con lo que hoy **no**
cumple, para que nadie repita una deuda que ya está anotada.

---

## Licencia

Sin licencia definida. Añade un `LICENSE` antes de publicar.

## Desarrollador

**Waddini** — Arquitectura & Development

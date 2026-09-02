# Estado del MVP

Actualizado: 2 de septiembre de 2026 (sexta tanda · reglas del proyecto)

## Checklist original — completo

**Inmediato**
- [x] Refactorizar `Domain/Services` → `Application/UseCases` — la capa `Domain/Services`
      se eliminó; sus clases implementaban la interfaz del repositorio *y* la inyectaban.
- [x] Crear `Domain/Exceptions/` — `DomainException`, `NotFoundException`,
      `ConflictException`, `ValidationException`, `UnauthorizedException`.
- [x] Implementar patrón `Result<T>` — `Domain/Common/Result.cs` con `Error` y `ErrorType`;
      `Api/Extensions/ResultExtensions.cs` lo traduce a status codes.
- [x] Crear DTOs segregados Request/Response — `Application/DTOs/`, uno por módulo.
      Las entidades ya no se serializan directamente.

**Corto plazo**
- [x] FluentValidation — validadores para los 6 módulos, aplicados por
      `AddFluentValidationAutoValidation()` antes de llegar al controlador.
- [x] Mapeo entidad → DTO — resuelto con métodos de extensión explícitos en
      `Application/Mappings/`, no con AutoMapper. Motivo: es verificado por el
      compilador (un campo nuevo rompe el build en vez de llegar null a producción)
      y AutoMapper pasó a licencia comercial desde la v15.
- [x] Serilog — logging estructurado a consola, configurable por `appsettings`.
- [x] Middleware de error global — `ExceptionHandlingMiddleware` → ProblemDetails (RFC 7807).
- [x] Tests unitarios — 164 tests en verde, muy por encima del 20% pedido.

**Mediano plazo**
- [x] Autenticación / autorización JWT — `POST /api/auth/login`, BCrypt (work factor 12).
      Los 3 roles fijos con `[Authorize(Roles = ...)]` fueron **reemplazados** en la
      quinta tanda por permisos: los roles pasaron a ser datos editables.
- [x] Paginación en todos los Get — `PageRequest`, con techo duro de 100 y tamaño
      elegible por el usuario (10/15/25) desde la sexta tanda.
- [x] Tests de integración — `WebApplicationFactory` levanta la API real.
- [x] CI/CD pipeline — `.github/workflows/ci.yml`, desde la segunda tanda.
- [ ] Caching (Redis o memoria) — pendiente, ver abajo.

## Añadido fuera del checklist

Faltaba el núcleo del negocio: era un sistema de inventario **sin movimientos de inventario**.

- **`InventoryMovement`** implementado completo (entidad, repositorio, use cases,
  controlador, migración). Es la única vía por la que cambia el stock; el movimiento
  y el nuevo stock se guardan en un solo `SaveChanges`.
- **`Client`** y **`Supplier`**: existían como entidades sueltas sin DbSet, repositorio
  ni controlador. `IClientRepository` no tenía implementación.
- **Reportes** computados: resumen de inventario, stock bajo y actividad por período.
  Reemplazan a la entidad `ReportInventory`, que sólo guardaba un `Details` de texto.
- **Modelo de datos rehecho**: FKs `Guid` reales con relaciones y `DeleteBehavior`
  explícito, `DateTime` en vez de `string` para las fechas, índices únicos en BD,
  `RowVersion` en `Product` como token de concurrencia. Migración limpia, `InitialSchema`.
- **Health checks**: `/health/live` y `/health/ready`.
- **Secretos fuera del repositorio**: `appsettings.json` vacío, `.env` en `.gitignore`,
  `.env.example` como plantilla. `Jwt:Key` se valida al arrancar.

## Bugs bloqueantes corregidos

| Problema | Efecto |
|---|---|
| `AddUserServices()` nunca se llamaba en `Program.cs` | Todo `/api/user/*` respondía 500 |
| `UseCors("AllowAngularApp")` sin política definida | Cero headers CORS, preflight 405: ningún frontend podía consumir la API |
| `UserService` generaba un salt aleatorio y lo descartaba | Ninguna contraseña podía verificarse: login imposible |
| Entidades serializadas sin DTO | `GET /api/user` devolvía el hash de la contraseña |
| Sin manejo global de excepciones | Todo "no encontrado" salía como 500 |
| `[Route("${id}")]` con `$` literal | El endpoint real era `/api/product/${id}` |
| `id` por querystring en update/delete | `PUT /api/product/update?id=...` |
| Doble `AddAsync` antes de comprobar duplicados | Entidad agregada dos veces al tracker |
| `Migrate()` con `EnableRetryOnFailure` antes de escuchar | El arranque se colgaba ~60s si la base no estaba lista |
| `README.md` en UTF-16 | Ilegible en GitHub y en el IDE |
| `.gitignore` copiado del `.dockerignore` | Ignoraba `README.md` y `docker-compose*` |

## Segunda tanda: de "funciona" a "se puede usar a diario"

- [x] **Refresh tokens con rotación y revocación.** `POST /api/auth/refresh` y
      `/api/auth/logout`. El access token dura 60 min y el refresco 14 días; cada
      canje rota el token y revoca el anterior. Si llega uno ya rotado se asume robo
      y se invalida la cadena completa. En la base sólo se guarda el SHA-256.
- [x] **Rate limiting en `/api/auth`.** 10 peticiones por minuto y por IP,
      configurable. Responde `429` con `Retry-After` y el mismo formato ProblemDetails
      que el resto de la API.
- [x] **Reversión de movimientos.** `POST /api/inventory-movements/{id}/reverse`.
      No edita ni borra el original: registra el movimiento inverso. Invierte la
      variación neta, así que es exacto para entradas, salidas y ajustes incluso si
      hubo movimientos posteriores. Índice único filtrado en `ReversalOfMovementId`
      para que la base impida una segunda reversión.
- [x] **Proveedor en los movimientos de entrada.** `InventoryMovement.SupplierId`
      con FK e índice, filtro `?supplierId=` en el historial, y herencia del
      proveedor del producto cuando no se indica. Antes una entrada no dejaba
      constancia de a quién se le compró.
- [x] **CI/CD** — `.github/workflows/ci.yml`: build en Release con `-warnaserror`,
      tests con cobertura y build de la imagen Docker.

### Bug propio encontrado y corregido en esta tanda

`AddSecurity` y `AddConfiguredRateLimiting` leían su configuración **al registrar**
los servicios, mientras que `TokenService` la leía por `IOptions` al resolverse.
Con el JWT eso hacía que se firmara con una clave y se validara con otra; con el
limitador, que la configuración del entorno no lo sustituyera. Ambos pasaron a leer
por `IOptions` (`JwtBearerOptionsSetup` y resolución por petición en la política).
Lo detectaron los tests de integración.

## Tercera tanda: auditoría del propio trabajo

Una revisión crítica de lo entregado encontró seis defectos. Todos corregidos.

- [x] **La API arrancaba sin cadena de conexión.** `appsettings.json` deja la clave
      en blanco y el `?? throw` sólo capturaba `null`, así que en Production sin la
      variable de entorno la app quedaba en pie devolviendo 500 opacos. Ahora falla
      al arrancar con el mensaje que dice qué falta.
- [x] **`GET /api/users/{id}` cargaba todos los movimientos del usuario.** El
      `Include(u => u.Movements)` existía sólo para que `DeleteUser` contara; ahora
      es un `COUNT` en base de datos.
- [x] **Concurrencia sin reintento.** El `RowVersion` protegía de escrituras
      perdidas, pero medido daban 5 éxitos y 7 conflictos con 12 peticiones en
      paralelo: 58% de fallos sin que nadie reintentara. Las operaciones que mueven
      stock se reintentan hasta 5 veces releyendo el producto, con jitter. Medido
      después: 25 en paralelo → 25 éxitos, 0 conflictos, stock consistente.
- [x] **`Domain/Exceptions/` era código muerto.** Cero `throw` de las cinco clases:
      `Result<T>` las había vuelto redundantes. Eliminadas, y el middleware quedó
      reducido a lo que de verdad ocurre. Era un ítem del checklist original que
      quedó superado por una decisión posterior.
- [x] **La reversión violaba el invariante del validador.** Revertir una compra
      producía una salida con proveedor, y ese mismo estado por el endpoint normal
      daba `400`. El invariante pasó a ser "a lo sumo una contraparte", sin atarlo
      al tipo: una devolución a proveedor es una salida con proveedor y una
      devolución de cliente es una entrada con cliente.
- [x] **`UseForwardedHeaders` no estaba configurado.** El rate limiting particiona
      por IP, así que detrás de un proxy todos compartían cupo y un solo atacante
      bloqueaba el login de todos. Configurable, desactivado por defecto (confiar en
      la cabecera sin proxy delante permitiría falsear la IP), con aviso si se activa
      sin declarar proxies de confianza.
- [x] **Menores.** `/api/reports/low-stock` ahora pagina y ordena por déficit en la
      base; servicio en background que purga los refresh tokens caducados; miembros
      declarados y nunca usados eliminados.

### El patrón que se repitió tres veces

`AddSecurity`, `AddConfiguredRateLimiting` y `AddPersistence` leían su configuración
**al registrar** los servicios. En ese momento `builder.Configuration` todavía no
incluye las fuentes que añade un host externo, así que:

- con el JWT se firmaba con una clave y se validaba con otra;
- el límite de intentos ignoraba la configuración del entorno;
- el override de la cadena de conexión en los tests nunca se aplicaba — **los tests
  pasaban gracias al bug**.

Las tres pasaron a leer por `IOptions` o desde el `IServiceProvider` al resolverse.
La validación de arranque usa `app.Configuration`, que sí está completa.

## Cuarta tanda: la interfaz web

Diseño aprobado en un canvas de 16 pantallas antes de escribir una línea, e
implementado en **React 19 + Vite + TypeScript** en `StockHex.Web/`.

- [x] **Las 11 pantallas del diseño**: login, dashboard, productos, detalle de
      producto, registro de movimiento, historial con reversión, categorías,
      proveedores, clientes, usuarios y reportes. La quinta tanda sumó dos más:
      roles y editor de permisos.
- [x] **Renovación de sesión transparente** con una sola renovación en vuelo. Sin
      eso, varias peticiones con 401 simultáneo canjearían el mismo refresh token
      y la API lo interpretaría como robo, cortando la sesión completa.
- [x] **Roles reflejados en la interfaz**, con las rutas escritas a mano bloqueadas
      por una guarda. Los tres roles fijos de esta tanda desaparecieron en la
      quinta: el menú pasó a derivarse de permisos.
- [x] **Errores por campo**: los `ProblemDetails` de la API se traducen y el mensaje
      del validador aparece junto al campo que lo causó, no en un cartel genérico.
- [x] **Tema claro y oscuro** con variables CSS; ningún componente conoce un color
      literal.
- [x] **Suite E2E en navegador real** (Playwright): 22 pasos de recorrido, los tres
      roles y cuatro escenarios de renovación de sesión.
- [x] **CI** amplía a tres jobs: API, web y build de las dos imágenes.
- [x] **Todo con un solo comando.** `docker compose up -d --build` en la raíz
      levanta base de datos, API y frontend. nginx sirve la interfaz y hace de
      proxy de `/api`, así que todo queda en un solo origen y **no hay CORS que
      configurar** en el despliegue. La imagen del frontend no lleva la URL de la
      API compilada dentro: se resuelve al arrancar el contenedor, de modo que la
      misma imagen sirve para cualquier entorno.

### Una afirmación que el test desmintió

Escribí en el compose que reenviar `X-Forwarded-For` hacía que el límite de
intentos partiera por IP real. Al comprobarlo, cambiar la IP declarada **no**
daba un cupo nuevo, y mi primera lectura fue que estaba roto. Era lo contrario:
nginx añade el peer real al final de la cabecera y ASP.NET procesa una sola
entrada, así que la API toma el peer real y descarta lo que inyecte el cliente.
Es decir, la cabecera **no es falsificable**, que es justo lo que se quiere. El
comentario del compose se corrigió y la comprobación quedó en `e2e/proxy.mjs`.

### Bug de la API encontrado al conectar el frontend

`Cors__AllowedOrigins__0` sólo admitía **un** origen por variable de entorno, así
que el puerto de Vite (5173) quedaba fuera y el navegador bloqueaba todo. Ahora
`Cors:AllowedOrigins` acepta también una lista separada por `;` o `,`, se descarta
la barra final (el header `Origin` nunca la envía) y se ignoran duplicados. Con
tests.

## Quinta tanda: RBAC y las reglas del proyecto

El equipo fijó ocho reglas y pidió que vivieran en un archivo que se lee antes de
implementar nada: [`CLAUDE.md`](CLAUDE.md). Con eso el modelo de autorización cambió
de raíz.

- [x] **Los roles son datos.** Tablas `Roles` y `RolePermissions`, CRUD completo,
      y `enum UserRole` eliminado del proyecto. La migración es **escrita a mano**:
      la que generó EF borraba la columna `Role` antes de crear nada y dejaba a
      todos los usuarios con `Guid.Empty`. La escrita crea las tablas, siembra los
      tres roles, hace el backfill con un `CASE` sobre la columna vieja y sólo
      entonces la borra. Verificado en SQL Server: nadie perdió acceso.
- [x] **Los permisos tienen una sola fuente, y es el código.** 31 claves en 9
      módulos en `Domain/Authorization/Permissions.cs`. **No hay tabla de permisos**,
      porque un permiso existe únicamente si un endpoint lo comprueba. Se exponen en
      `GET /api/permissions` y el frontend los consume sin redeclararlos.
- [x] **31 `[RequirePermission]`** y cero `[Authorize(Roles = …)]`. La interfaz usa
      los mismos permisos para no ofrecer acciones que van a fallar, nunca como
      control de acceso.
- [x] **Un cambio de permisos se aplica en segundos, sin cerrar sesiones.** El JWT
      lleva sólo el id del rol; si llevara la lista, quitar un permiso no surtiría
      efecto hasta que el token se renovara, hasta 60 minutos después. Se resuelven
      por petición con una caché de 30 s que se invalida al editar el rol.
- [x] **Restablecer la contraseña de otro usuario** con el permiso
      `users.change_password`, y opción de revocar sus sesiones.
- [x] **Editor de permisos**: matriz de 9 módulos × 4 acciones más una columna de
      especiales. Marcar Crear arrastra Ver; quitar Ver limpia el módulo.

### Un guardia que desapareció al hacerlo bien

`activeAdmins` contaba los administradores **de la página cargada** para decidir si
deshabilitaba el botón de eliminar: con más de una página el número era falso. Con
el RBAC dejó de existir en el frontend — el guardia lo impone la API comprobando que
quede al menos un usuario activo con `roles.edit` y `users.edit`.

### Dos bugs de EF que costó ver

`_context.Roles.Update(role)` marcaba **todo el grafo** como modificado, incluidos
los `RolePermission` que no habían cambiado, y saltaba `DbUpdateConcurrencyException`.
Los siete repositorios que exponen `Update` pasaron a comprobar el estado de la
entidad antes de llamarlo (el de movimientos no lo tiene: un movimiento nunca se
modifica, se corrige con su inverso).

Y un `RolePermission` nuevo se guardaba como *Modified* en vez de *Added*, porque su
`Id` se asignaba en el inicializador y EF lo tomaba por una entidad existente. Se vio
volcando el change tracker. Se corrigió con un `Add` explícito.

## Sexta tanda: filtros en la URL y paginación elegible

- [x] **Los filtros viven en la URL** (regla 4). Un único hook,
      `lib/urlFilters.ts`, y las 9 pantallas con tabla migradas a él: no queda un
      solo `useState` espejo de la URL. Refrescar conserva la consulta y copiar el
      enlace reconstruye la pantalla. El hook **consolidó y borró** `useDebounced` y
      `useResetPageOnFilterChange` en vez de convivir con ellos.
- [x] **El usuario elige cuántas filas ve**: 10, 15 o 25, con 15 por omisión. Los
      valores están definidos una sola vez, en `PageRequest.AllowedPageSizes`, y el
      frontend los refleja. El selector lo pinta `Pager`, así que se agregó a las 9
      tablas de una vez. El E2E no mira sólo la URL: **observa la petición** y
      confirma que sale con el `pageSize` elegido.
- [x] **`strict: true` declarado** en `tsconfig.app.json`. La auditoría anterior daba
      por hecho que TypeScript lo activaba por defecto: es falso, sin declararlo
      `noImplicitAny` está apagado. Al activarlo el proyecto compiló limpio.

### Tres fallos del propio andamiaje

- El contenedor `web` llevaba horas `unhealthy` con el sitio funcionando perfecto:
  dentro del contenedor `localhost` resuelve primero a `::1` y nginx sólo escucha en
  IPv4, así que la sonda daba «connection refused». Ahora usa `127.0.0.1`.
- `npm run e2e` **no podía terminar en verde nunca**: el propio paso de credenciales
  incorrectas provoca un 401 y el harness lo contaba como error, cortando la cadena
  antes de las otras suites. Y una tanda completa se pasaba del límite de 10 logins
  por minuto, con un timeout que parecía un fallo del producto. `e2e/run.mjs` espacia
  las suites.
- Tres suites apuntaban al servidor de Vite y tres al contenedor: una tanda «en
  verde» no verificaba un despliegue, sino dos a medias. El destino ahora es único.

## Pendiente (post-MVP)

Lo único que exigen las reglas y todavía no está son los dos puntos de la regla 3:

- **Selectores con búsqueda en el servidor** — 11 consultas con `pageSize: 100`
  alimentan `<select>` que filtran en memoria. Pasados los 100 registros, las
  opciones que faltan son invisibles.
- **KPIs de Movimientos agregados en la API** — hoy se suman sobre la página cargada.
  Están rotulados «en la página», así que no engañan, pero la agregación le
  corresponde a la base.

Y el resto, que nunca fue del MVP:

- **Confirmación de email y recuperación de contraseña** — `EmailConfirmed` existe
  pero sólo lo pone el seeder; no hay flujo.
- **Logs persistentes** — Serilog sólo escribe a consola.
- **`CreatedBy` / `UpdatedBy` en el catálogo** — los movimientos ya guardan el autor,
  pero no se sabe quién cambió un precio.
- **HTTPS en el contenedor**.
- **Caching** — conviene medir antes: los listados ya paginan y agregan en base de datos.

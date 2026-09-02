# Reglas del proyecto StockHex

**Fuente de verdad. Se lee antes de implementar cualquier funcionalidad nueva.**

Este archivo se carga automáticamente al abrir el proyecto en Claude Code, así que
las reglas de aquí aplican sin tener que recordarlas. Cada regla nueva que se acuerde
durante el desarrollo **se agrega a este archivo** y pasa a regir desde ese momento.

La sección [Estado de cumplimiento](#estado-de-cumplimiento) dice qué partes del
código ya respetan estas reglas y cuáles no. Se mantiene al día: una regla sin estado
declarado es una regla que nadie puede verificar.

---

## Regla 0 · Antes de implementar

**No se escribe código de inmediato. Primero se entiende cómo funciona el proyecto.**

Orden de preferencia, siempre:

> **Reutilizar > Extender > Refactorizar > Crear algo nuevo**

Crear una implementación nueva es la última opción y hay que justificar por qué no
se pudo reutilizar lo existente.

Checklist antes de escribir la primera línea:

1. Leer estas reglas.
2. Revisar la arquitectura existente ([Patrones establecidos](#patrones-establecidos)).
3. Buscar funcionalidad similar o reutilizable.
4. Revisar componentes, hooks, servicios, endpoints y utilidades que ya existen.
5. Evaluar si se puede extender algo en lugar de duplicarlo.
6. Verificar que filtrado, búsqueda y paginación se resuelvan **en el backend**, y
   que el tamaño de página salga del catálogo (regla 8), no de un número suelto.
7. Verificar que el estado relevante persista en **query params**.
8. Verificar qué **permiso** exige la funcionalidad: declararlo en
   `Domain/Authorization/Permissions.cs`, exigirlo con `[RequirePermission]` en el
   endpoint, y reflejarlo en el frontend con `can(P.…)` para no ofrecer lo que va a
   fallar. La interfaz nunca es la capa de seguridad.
9. Implementar siguiendo los patrones ya establecidos.

---

## Regla 1 · Tipado

- **Prohibido `any`, sin excepciones.** Ni `: any`, ni `<any>`, ni `as any`, ni `any[]`.
- Prohibido `@ts-ignore`, `@ts-nocheck`, `@ts-expect-error` y cualquier otra forma de
  silenciar el compilador.
- Prohibido `as unknown as T` para forzar una conversión.
- Todo debe estar tipado correctamente.
- Ante la duda sobre un tipo: investigar la estructura existente y **definir el tipo
  apropiado**. `any` no es una solución rápida, es deuda.

**Dónde viven los tipos:** los DTOs de la API son el contrato y están en
[`StockHex.Web/src/api/types.ts`](StockHex.Web/src/api/types.ts), espejo de los
`record` de `Application/DTOs/`. Si un endpoint cambia, se actualizan los dos.
El contrato real se puede extraer del Swagger de la API en cualquier momento:

```bash
curl -s http://localhost:8080/swagger/v1/swagger.json
```

Nunca escribir un tipo de memoria cuando se puede leer del contrato.

**Cuando no se conoce la forma de un dato externo:** se tipa como `unknown` y se
estrecha con comprobaciones explícitas. Ejemplo real en el proyecto:
[`api/problem.ts`](StockHex.Web/src/api/problem.ts) recibe el cuerpo de un error,
lo tipa como `ProblemPayload | null` y comprueba antes de leerlo.

---

## Regla 2 · No duplicar funcionalidad

Antes de crear una función, componente, servicio, hook, endpoint o utilidad:

1. Buscar si ya existe algo equivalente.
2. Si existe, reutilizarlo o extenderlo.
3. No crear una segunda implementación de lo mismo con otro nombre.
4. Si se encuentran **dos** implementaciones que hacen lo mismo, **señalarlo antes**
   de crear una tercera.
5. Priorizar consolidación sobre duplicación.

No hacer esto:

```ts
// Ya existe users.list(query) en api/endpoints.ts
export function fetchAllUsers() { /* … */ }   // ✗ segunda implementación
```

Hacer esto: evaluar si `users.list()` admite el parámetro que falta, y si no,
extenderlo.

**Antes de crear algo, buscar:**

```bash
# ¿existe ya este endpoint?
grep -rn "nombreDelRecurso" StockHex.Web/src/api/endpoints.ts

# ¿existe ya este componente o hook?
ls StockHex.Web/src/components StockHex.Web/src/lib

# ¿existe ya este caso de uso en la API?
ls "StockHex API/StockHex API/Application/UseCases"
```

**Puntos de consolidación que ya existen y hay que respetar:**

| Concepto | Fuente única | No volver a definirlo |
|---|---|---|
| Metadatos de tipo de movimiento (etiqueta, color, icono) | `components/tokens.ts` → `MOVEMENT` | en ninguna pantalla |
| Colores y tonos | `styles/tokens.css` (variables CSS) | ningún componente lleva un color literal |
| Iconos | `components/Icon.tsx` | nunca emoji, nunca un SVG suelto |
| Formato de dinero y fechas | `lib/format.ts` | ningún `toLocaleString` disperso |
| Menú y permiso que lo habilita | `auth/roles.ts` → `NAV`, `navFor(permisos)` | ninguna pantalla decide por su cuenta |
| Claves de permiso que nombra la interfaz | `auth/permissions.ts` → `P` | ninguna cadena literal suelta en una pantalla |
| Matriz de permisos de un rol | `components/PermissionMatrix.tsx` | ninguna rejilla a mano |
| Catálogo de permisos | `Domain/Authorization/Permissions.cs`, expuesto en `GET /api/permissions` | ni el frontend ni una tabla lo redeclaran (regla 7) |
| Traducción de errores de la API | `api/problem.ts` + `components/Toast.tsx` | ningún `catch` con mensaje propio |
| Patrón CRUD de tabla + modal | `pages/CrudPage.tsx` | Categorías, Proveedores y Clientes lo usan |
| Tabla densa y paginación | `components/DataTable.tsx` | ninguna `<table>` a mano |
| Columna de acciones de una fila | última columna, con `key: 'actions'` | ninguna otra clave: es la que `DataTable` fija a la derecha |

---

## Regla 3 · Filtrado, búsqueda, orden y paginación: en el backend

Toda operación de **filtrado, búsqueda, ordenamiento, paginación, selección de
registros** o cualquier consulta sobre volúmenes de datos se resuelve **en el backend**.

- El frontend **no descarga todos los registros** para filtrarlos, buscarlos u
  ordenarlos en memoria.
- El backend procesa los parámetros de consulta y devuelve **sólo lo necesario**.
- Las agregaciones (totales, conteos, sumas) las calcula la base de datos, no el
  cliente.

**Cómo se hace en este proyecto:** `PageRequest` en
[`Domain/Common/PageRequest.cs`](StockHex%20API/StockHex%20API/Domain/Common/PageRequest.cs)
acota `pageSize` a 100 y expone `Search`. Los filtros específicos lo extienden
(`ProductFilter`, `MovementFilter`). Los repositorios traducen esos filtros a SQL:

```csharp
// ProductRepository: el filtro se aplica en la base, no en memoria
if (filter.LowStockOnly)
    query = query.Where(p => p.StockQuantity <= p.MinimumStock);

var total = await query.CountAsync(cancellationToken);
var items = await query.Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
```

**Un selector con muchas opciones también es una consulta.** Un `<select>` que
descarga un lote fijo y filtra en el navegador viola esta regla: necesita búsqueda
contra el servidor.

**Si una pantalla necesita un total que la API no expone, se agrega el endpoint o el
campo.** No se calcula sobre la página cargada: el resultado sería incorrecto en
cuanto haya más de una página.

---

## Regla 4 · Query params y persistencia del estado

Los filtros y parámetros relevantes de cada pantalla se reflejan en la **URL**:

```
/usuarios?page=2&pageSize=25&search=juan&status=active
```

Consecuencias obligatorias:

- Aplicar un filtro actualiza su query param.
- Buscar se refleja en la URL.
- Cambiar de página se refleja en la URL.
- Cambiar el tamaño de página se refleja en la URL.
- **Refrescar el navegador conserva el estado de la consulta.**
- **Copiar y compartir la URL reconstruye la misma pantalla** para quien tenga los
  permisos necesarios.

El estado de la UI se **deriva de los query params** cuando corresponda. No se
mantiene el mismo dato en dos lugares (un `useState` y la URL) porque se desincronizan.

**Cómo se hace en este proyecto:** hay **un solo hook**,
[`lib/urlFilters.ts`](StockHex.Web/src/lib/urlFilters.ts). Una pantalla declara su
esquema y recibe los valores ya tipados por clave:

```ts
const filters = useUrlFilters({
  page: numberParam(1, { min: 1, pagination: true }),
  pageSize: numberParam(20, { min: 1, max: 100, pagination: true }),
  search: stringParam(),
  status: enumParam(['active', 'inactive', 'all'] as const, 'active'),
  lowStockOnly: boolParam(),
  from: dateParam(),
});
const { page, search, status } = filters.values;   // number, string, 'active' | …

// El input escribe en la URL con retardo; la URL sigue siendo la única fuente.
const [searchInput, setSearchInput] = useDebouncedParam(
  search, (value) => filters.set('search', value));
```

Lo que el hook garantiza, y por eso no se replica a mano:

- **Los valores por defecto no se escriben en la URL.** Sin filtros la URL queda
  limpia, y un enlace compartido sólo lleva lo que de verdad se cambió.
- **Cambiar un filtro devuelve la paginación a la página 1.** Los parámetros
  marcados `pagination: true` (`page`, `pageSize`) no disparan ese descarte.
- **Navega con `replace`.** Teclear en un buscador no llena el historial de
  estados intermedios, así que «atrás» vuelve a la pantalla anterior.
- **Parseo tolerante.** `?page=-7&status=inventado&pageSize=99999` no rompe nada:
  cada parámetro acota su valor o vuelve a su default.
- `filters.set`, `filters.setMany` (varios de golpe, como limpiar `partyId` al
  cambiar `partyKind`), `filters.reset` e `filters.isFiltered`.

Factorías disponibles: `stringParam`, `numberParam`, `boolParam`, `enumParam`,
`dateParam`. **Si falta un tipo, se agrega una factoría al hook** — no se sincroniza
la URL a mano en una pantalla.

Verificado en navegador real: [`e2e/filters.mjs`](StockHex.Web/e2e/filters.mjs).

---

## Regla 5 · RBAC flexible

La aplicación usa **control de acceso basado en roles** y **no** asume que los roles
son un conjunto fijo predefinido.

El sistema debe permitir:

- Crear roles personalizados.
- Editar roles.
- Eliminar roles cuando esté permitido.
- Asignar permisos a cada rol.
- Asignar roles a usuarios.
- Definir qué funcionalidades puede usar cada rol.
- Definir qué módulos puede ver cada rol.
- Definir qué acciones puede ejecutar cada rol.

Los permisos son granulares y controlan acciones concretas:

```
users.view          products.view          reports.view
users.create        products.create        reports.export
users.edit          products.edit
users.delete        products.delete
users.change_password
```

**El backend valida los permisos.** Ocultar un botón en el frontend no es seguridad:
es comodidad. Cada endpoint exige su permiso y responde `403` si falta, sin importar
lo que muestre la interfaz.

El frontend usa los permisos para **no ofrecer acciones que van a fallar**, nunca
como control de acceso.

---

## Regla 6 · Administración de contraseñas

Un usuario con el permiso correspondiente puede **cambiar o restablecer la contraseña
de otro usuario**.

Esa capacidad está representada por un permiso propio:

```
users.change_password
```

No se asume que cualquier administrador tiene acceso absoluto: si el sistema permite
configurar permisos de forma granular, esta acción se rige por su permiso como
cualquier otra.

## Regla 7 · Una sola fuente de permisos

**El catálogo de permisos tiene una única fuente de verdad: el código.**

- **No hay tabla de permisos.** No se siembra el catálogo en la base de datos.
- El catálogo se declara una vez, como constantes, en
  `Domain/Authorization/Permissions.cs`.
- La API lo expone en **`GET /api/permissions`**. El frontend lo consume desde ahí y
  **nunca lo vuelve a declarar**.
- Los **roles** guardan las **claves** de los permisos que conceden
  (`RolePermissions` con la clave como texto), validadas contra el catálogo al escribir.

**Por qué el código y no la base:** un permiso existe únicamente porque un endpoint
lo comprueba. Una fila en una tabla que ningún `[RequirePermission]` mire no protege
nada, y una constante sin fila haría que el rol no se pudiera configurar. Mantener
las dos cosas es mantener dos verdades que se desincronizan en el primer despliegue
a medias.

Consecuencia directa: **los permisos no se crean, editan ni eliminan desde la
interfaz.** Los roles sí. Añadir un permiso nuevo es un cambio de código, igual que
el endpoint que lo comprueba — porque son la misma cosa.

Esto es la Regla 2 aplicada al modelo de autorización: una sola implementación, un
solo lugar.

**Dos excepciones al «un permiso = un endpoint», verificadas en el código:**

- **`dashboard.view`** habilita sólo la pantalla de inicio, que compone
  `/api/reports/*` e `/api/inventory-movements`. No hay endpoint de dashboard porque
  cada pieza ya exige su permiso: la autorización real sigue estando en la API.
- **`reports.export`** está en el catálogo y concedida a dos roles, pero **nada la
  comprueba todavía** — el endpoint de exportación no existe. Es la única clave sin
  uso, y al implementar la exportación hay que exigirla con `[RequirePermission]`,
  no declarar una nueva.

Antes de agregar una clave al catálogo: **el endpoint que la exige se escribe en el
mismo cambio.** Una clave sin nada que la mire es una promesa que la interfaz cree y
la API no cumple.

---

## Regla 8 · Tamaño de página elegible

El usuario elige **cuántos registros ve por página**. Las opciones son **10, 15 y 25**,
y ninguna más.

- **Todos los GET de listado** aceptan el tamaño de página.
- **El backend hace la paginación.** El frontend sólo envía el parámetro; no descarga
  de más para después recortar en memoria.
- **El tamaño va en la URL**, igual que los filtros (regla 4): refrescar lo conserva y
  el enlace compartido abre la tabla con el mismo tamaño.

**Dónde está definido:** en el backend, y una sola vez —
`PageRequest.AllowedPageSizes` y `PageRequest.DefaultPageSize` en
[`Domain/Common/PageRequest.cs`](StockHex%20API/StockHex%20API/Domain/Common/PageRequest.cs).
El defecto es **15**. El frontend lo refleja como `PAGE_SIZES` y
`DEFAULT_PAGE_SIZE` en [`api/types.ts`](StockHex.Web/src/api/types.ts), el archivo
que ya es espejo del contrato de la API: si cambia allá, se cambia acá.

Los dos números tienen que coincidir. Cuando la URL no trae `pageSize`, el frontend
**no lo envía** y responde el defecto del backend; si no coincidieran, la primera
página mostraría un número de filas que el selector no puede volver a elegir.

**Cómo se usa:** el selector lo pinta `Pager`, no cada pantalla:

```tsx
const filters = useUrlFilters({
  page: numberParam(1, { min: 1, pagination: true }),
  pageSize: pageSizeParam(),   // acotado al catálogo; fuera de él cae al defecto
  // …
});

<Pager
  data={list.data}
  onPage={(p) => filters.set('page', p)}
  pageSize={pageSize}
  onPageSize={(size) => filters.set('pageSize', size)}
/>
```

`pageSizeParam` **no** es `pagination`: cambiar el tamaño descarta la página, porque
la página 2 de 10 en 10 no tiene equivalente de 25 en 25.

`MaxPageSize = 100` sigue existiendo y es otra cosa: el techo duro que impide que
alguien pida el listado completo escribiendo la URL. No es una opción de la interfaz.

---

## Estado de cumplimiento

Verificado sobre el código actual. **No todo cumple**: lo que falta está declarado
como trabajo pendiente, no escondido.

Última verificación: 2 de septiembre de 2026, con las reglas 4 a 8 implementadas.
**194 tests de API** (build Release con 0 warnings) y **95 comprobaciones en
navegador real** (`npm run e2e` + `npm run e2e:proxy`), todo en verde.

| Regla | Estado | Detalle |
|---|---|---|
| 1 · Tipado | ✅ **cumple** | 0 usos de `any`, `@ts-ignore` o `as unknown as`. `"strict": true` y `noImplicitOverride` ya están **declarados** en `tsconfig.app.json`. Corrección a la auditoría anterior: no estaban activos por defecto — `strict` es `false` si no se declara, así que un parámetro sin tipo se colaba como `any` implícito. Al declararlo el proyecto compiló limpio sin un solo cambio de tipos. |
| 2 · No duplicar | ✅ **cumple** | `MOVEMENT_HELP` en [`pages/Login.tsx`](StockHex.Web/src/pages/Login.tsx) ya deriva etiqueta, icono y color de `MOVEMENT` (`components/tokens.ts`) y sólo añade la explicación. `useDebounced` y `useResetPageOnFilterChange` se consolidaron en `useUrlFilters`: los dos hooks se **borraron**, no quedaron en paralelo. |
| 3 · Backend | ⚠️ **cumple en las tablas, faltan dos puntos** | Ver abajo. |
| 4 · Query params | ✅ **implementado** | Un único hook, [`lib/urlFilters.ts`](StockHex.Web/src/lib/urlFilters.ts), y **las 9 pantallas con filtros migradas** (el Dashboard no tiene: son resúmenes de tamaño fijo). La URL es la única fuente del filtro: no hay `useState` espejo. Verificado en navegador real con [`e2e/filters.mjs`](StockHex.Web/e2e/filters.mjs) — 14 comprobaciones, incluidas «refrescar conserva el estado» y «otra sesión abre el enlace y ve la misma pantalla». |
| 5 · RBAC flexible | ✅ **implementado** | Tablas `Roles` y `RolePermissions`, CRUD de roles, **31 `[RequirePermission]`** y **0 `[Authorize(Roles = …)]`**. El `enum UserRole` ya no existe en ningún archivo. El menú y los botones se derivan de permisos. |
| 6 · Contraseñas | ✅ **implementado** | `POST /api/users/{id}/reset-password` con el permiso `users.change_password`, y opción de revocar las sesiones del afectado. La propia contraseña sigue exigiendo la actual. |
| 7 · Fuente única de permisos | ✅ **implementado**, con una clave sin uso | 31 permisos en 9 módulos, constantes en `Domain/Authorization/Permissions.cs`. **Sin tabla de permisos.** Se exponen en `GET /api/permissions` y el frontend los consume sin redeclararlos. La salvedad: `reports.export` está concedida a dos roles y **nada la comprueba** — el endpoint de exportación no existe. Detalle en la regla 7. |
| 8 · Tamaño de página | ✅ **implementado** | 10/15/25 definidos en `PageRequest.AllowedPageSizes`, defecto 15. Selector en `Pager`, en las **9 pantallas** con tabla. Va en la URL y la API recibe el `pageSize` elegido: verificado observando la petición en `e2e/filters.mjs`. |

### Detalle de la regla 3

Las tablas paginadas están bien: filtran, buscan, ordenan y paginan en SQL. El filtro
por rol de la pantalla de Usuarios también, con `UserFilter.RoleId`. Faltan dos cosas:

1. **Selectores que descargan un lote fijo.** 11 consultas con `pageSize: 100` en
   `MovementForm`, `Products`, `Movements` y `Users` alimentan `<select>` donde el
   usuario busca en memoria. Con más de 100 registros, las opciones que faltan son
   **invisibles**. Necesitan búsqueda contra el servidor.
2. **KPIs de Movimientos calculados sobre la página.** `pageTotals` en
   [`pages/Movements.tsx`](StockHex.Web/src/pages/Movements.tsx) suma con `reduce`
   los 20 registros cargados. Están rotulados «en la página», así que no engañan,
   pero la agregación le corresponde a la base de datos.

Corregido: **`activeAdmins`** contaba los administradores de la página actual para
decidir si deshabilitaba el botón de eliminar. Con el RBAC desapareció: el guardia lo
impone la API comprobando que quede alguien activo con los permisos críticos.

### Trabajo pendiente que derivan estas reglas

Queda **un solo frente abierto**: los dos puntos de la regla 3.

1. **Selectores con búsqueda en servidor** — 11 consultas con `pageSize: 100`
   alimentan `<select>` que filtran en memoria. Con más de 100 registros las
   opciones que faltan son invisibles. Pide un componente de autocompletado que
   consulte la API con `search`, y `useDebouncedParam` ya resuelve el retardo.
2. **KPIs de Movimientos agregados en la API** — `pageTotals` suma con `reduce` la
   página cargada. Necesita un endpoint o un campo de totales del filtro completo.

Y una deuda del catálogo de permisos: **`reports.export` no la exige nadie**. Al
implementar la exportación hay que colgarla de esa clave.

---

## Patrones establecidos

Lo que hay que conocer antes de tocar código. Detalle completo en
[`README.md`](README.md) y [`StockHex.Web/README.md`](StockHex.Web/README.md).

### Principio del dominio

**El stock nunca se edita a mano.** Sólo cambia registrando un movimiento en
`POST /api/inventory-movements`. El movimiento y el nuevo stock se guardan en un
único `SaveChanges`, así que historial y stock no pueden desincronizarse. Cualquier
funcionalidad que altere existencias pasa por ahí.

Un movimiento equivocado **no se edita ni se borra**: se corrige con
`POST /api/inventory-movements/{id}/reverse`, que registra el inverso.

### Autorización

```
JWT → OnTokenValidated → IActiveUserResolver → caché 30 s → DB   (¿la cuenta sigue viva?)
    → [RequirePermission("x.y")] → IPermissionResolver → caché 30 s → DB   (¿puede?)
```

El token lleva **sólo el id del rol**, nunca la lista de permisos: si la llevara,
quitarle un permiso a alguien no surtiría efecto hasta que su token se renovara,
hasta 60 minutos después. Se resuelven por petición con una caché de 30 segundos que
se invalida explícitamente al editar un rol, así que el cambio se aplica de inmediato
y sin cerrar la sesión de nadie.

**La cuenta se comprueba antes que el permiso.** Un JWT no se puede revocar: vale
hasta que expira. Sin `IActiveUserResolver`, desactivar o borrar a alguien no lo echa
— su refresco falla, pero el access token en curso sigue abriendo todo hasta una hora
después (ocho en `Development`). Va en `OnTokenValidated` y no en el filtro de
permisos porque así cubre también los endpoints que sólo llevan `[Authorize]`.

Guardias que protegen el acceso a la administración, todos en la API:

- Un rol **de sistema** no se elimina ni se queda sin los permisos críticos, y
  **concede siempre el catálogo completo**: `PermissionSynchronizer` lo reconcilia al
  arrancar. Es derivado, no almacenado — si fuera una foto de la migración que lo
  creó, agregar un permiso nuevo dejaría al administrador sin él, en silencio.
- **Nadie concede un permiso que él mismo no tiene** (`PermissionEscalationGuard`).
  Sin esto `roles.edit` alcanza para todo: se edita el rol propio, se marca el resto
  de la matriz y se sale siendo superusuario. Sólo se juzgan las **altas**, así que
  quitar permisos o renombrar un rol más poderoso que el propio sigue funcionando.
- Un rol **con usuarios asignados** no se elimina.
- No se puede dejar el sistema **sin ningún usuario activo** con `roles.edit` y
  `users.edit`. «El último administrador» dejó de ser un valor y pasó a ser una
  capacidad.
- Nadie elimina su propia cuenta ni restablece su propia contraseña por el atajo que
  no pide la actual.
- **Cambiar la contraseña cierra todas las sesiones** y devuelve un par de tokens
  nuevo en la misma respuesta. Es lo que hace quien cree que le robaron la cuenta;
  dejar vivos los refrescos anteriores lo dejaría dentro catorce días más. El par
  devuelto evita que el propio dispositivo quede con un refresco muerto.

### API · flujo de una petición

```
Controller → UseCase → Repository → DbContext
     ↑          ↓
  Result<T>  IUnitOfWork
```

- El **controlador** no tiene lógica: traduce `Result<T>` a status HTTP con
  `ToOk()` / `ToCreated()` / `ToNoContent()`.
- La **use case** es una clase por operación en `Application/UseCases/<Módulo>UseCases/`
  (`ProductUseCases`, `RoleUseCases`, …),
  devuelve `Result<T>` y nunca conoce HTTP.
- El **repositorio** sólo marca cambios; la use case confirma con `IUnitOfWork`.
- Los errores esperados viajan como `Result<T>`. El middleware de excepciones
  cubre sólo lo que `Result` no puede expresar (constraint por carrera,
  concurrencia agotada, fallo inesperado). **No hay jerarquía de excepciones de
  dominio a propósito**: serían una segunda vía para lo mismo.

Las operaciones que mueven stock se envuelven en
`IUnitOfWork.ExecuteWithConcurrencyRetryAsync`, porque compiten por el
`RowVersion` del producto.

### Frontend · dónde va cada cosa

| Necesito… | Va en… |
|---|---|
| Llamar a un endpoint | `api/endpoints.ts` (una función por endpoint) |
| Un tipo de la API | `api/types.ts` |
| Interpretar un error | ya está resuelto: `api/problem.ts` + `useToast().fromError()` |
| Un color, radio o sombra | una variable de `styles/tokens.css` |
| Una tabla | `components/DataTable.tsx` + `Pager` |
| Un formulario | `components/Field.tsx` (`Field`, `Input`, `Select`, `Toggle`) |
| Un CRUD sencillo | `pages/CrudPage.tsx`, parametrizado |
| Un filtro, una búsqueda o una página | `lib/urlFilters.ts` (`useUrlFilters`) — **nunca `useState`** |
| Retardar lo que se teclea | `useDebouncedParam`, del mismo archivo |
| Formatear dinero o fecha | `lib/format.ts` |
| Saber si el usuario puede algo | `useAuth().can(P.modulo.accion)` |
| Nombrar un permiso | `auth/permissions.ts` (`P.products.create`) |
| El catálogo de permisos | `usePermissionCatalog()`, que lo pide a la API |

Reglas de estilo del frontend:

- Estilos en línea apuntando a variables CSS. Sin Tailwind, sin clases sueltas.
- **Nunca `dangerouslySetInnerHTML`**: es lo que sostiene que guardar los tokens en
  `localStorage` sea aceptable.
- Toda mutación invalida las queries afectadas. Un movimiento invalida `products`,
  `movements` y `reports`, porque los tres cambian.
- Español de Chile. Pesos sin decimales con punto de miles (`lib/format.ts`).
  Fechas en 24 horas.
- **Una tabla que no cabe se desplaza; no se comprime.** `DataTable` le pone a la
  `<table>` un `minWidth` igual a la suma de los anchos de sus columnas, porque
  `width` a secas es una sugerencia que el navegador ignora bajo presión: el
  resultado era una tabla encogida con el texto partido en tres líneas y filas de
  93 píxeles, en vez del desplazamiento horizontal que el contenedor ya pedía. Al
  declarar el `width` de una columna se está fijando ese mínimo, así que conviene
  que sea el ancho real que necesita el contenido.

### Verificación mínima antes de dar algo por hecho

```bash
# API
cd "StockHex API" && dotnet build "StockHex API.sln" -c Release -warnaserror && dotnet test

# Dependencias: el CI falla si aparece una vulnerabilidad conocida, así que
# conviene verlo antes de empujar. Las transitivas cuentan: las que había
# llegaban por Microsoft.Data.SqlClient, no de una referencia directa.
dotnet list "StockHex API.sln" package --vulnerable --include-transitive

# Frontend
cd StockHex.Web && npm run typecheck && npm run lint && npm run build && npm audit --omit=dev

# El stack completo, y el recorrido en navegador real
docker compose up -d --build
cd StockHex.Web && APP_URL=http://localhost:8080 npm run e2e && npm run e2e:proxy
```

`npm run e2e` corre las 5 suites con [`e2e/run.mjs`](StockHex.Web/e2e/run.mjs), que
**espera entre una y otra**: el limitador de `/api/auth` acepta 10 intentos por minuto
y una tanda completa hace más de 10 logins. Encadenarlas con `&&` dejaba a las últimas
sin poder entrar, con un timeout que parecía un fallo del producto. Por eso la tanda
completa tarda varios minutos; una suite suelta (`npm run e2e:filters`) es inmediata.

`-warnaserror` no es opcional: la solución compila con **0 warnings** y debe seguir así.

`dotnet test` incluye los tests de `Database/`, que levantan un **SQL Server real**
con Testcontainers. Son los únicos que verifican lo que el proveedor InMemory no
puede: índices únicos, el índice filtrado de la reversión, la colación del SKU y el
`rowversion`. **Sin Docker se omiten**, no fallan, así que un «13 skipped» en local
significa que falta levantar Docker, no que algo esté roto.

**El stack se levanta con un solo comando y los tres contenedores tienen que quedar
`healthy`.** Comprobarlo, porque un `unhealthy` con el sitio aparentemente funcionando
es fácil de ignorar:

```bash
docker compose ps --format '{{.Service}} {{.State}} {{.Health}}'
```

Dentro de un contenedor, `localhost` resuelve **primero a `::1`**. Una sonda que use
`localhost` contra un servidor que sólo escucha en IPv4 —el `listen 80;` de nginx—
falla con «connection refused» mientras el servicio atiende de sobra. Las sondas van
contra `127.0.0.1`.

---

## Registro de reglas

| Fecha | Regla | Origen |
|---|---|---|
| 2026-09-02 | Reglas 0 a 6 (tipado, no duplicar, backend, query params, RBAC, contraseñas, proceso) | Acordadas con el equipo |
| 2026-09-02 | Regla 7 · una sola fuente de permisos, en el código | Acordada al revisar el diseño del RBAC |
| 2026-09-02 | Regla 8 · tamaño de página elegible (10/15/25), definido en el backend y reflejado en la URL | Pedida al cerrar la regla 4 |

Reglas 4 a 8 implementadas el 2 de septiembre de 2026 (API y frontend), verificadas
con **194 tests de API** y **95 comprobaciones en navegador real** repartidas en seis
suites de Playwright.

Cada regla nueva se agrega arriba con su fecha y se refleja en
[Estado de cumplimiento](#estado-de-cumplimiento).

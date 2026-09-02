# StockHex · Interfaz web

Frontend de [StockHex](../README.md), en **React 19 + Vite + TypeScript**.
Implementa el diseño aprobado en el canvas de maquetas.

```
[██████████] MVP funcional  ·  verificado en navegador real contra la API
```

---

## Arrancar

Para **usar** la aplicación no hace falta nada de esto: `docker compose up -d --build`
en la raíz levanta todo. Lo de abajo es para **desarrollar** la interfaz con recarga
en caliente, con la API ya corriendo.

```bash
cp .env.example .env      # ajusta VITE_API_URL si tu API no está en :8080
npm install
npm run dev               # http://localhost:5173
```

`5173` ya está en los orígenes permitidos de la API en desarrollo. Si cambias el
puerto, agrégalo a `Cors:AllowedOrigins` (acepta una lista separada por `;`).

| Script | Qué hace |
|---|---|
| `npm run dev` | Servidor de desarrollo con recarga en caliente |
| `npm run build` | Compila tipos y genera `dist/` |
| `npm run typecheck` | Sólo verifica tipos |
| `npm run lint` | oxlint sobre `src/` |
| `npm run e2e` | Las 5 suites en navegador real, espaciadas (ver abajo) |
| `npm run e2e:<suite>` | Una suite suelta: `smoke`, `roles`, `refresh`, `rbac`, `filters`, `proxy` |

---

## En Docker

`Dockerfile` compila con Node y sirve el resultado con nginx, que además hace de
**proxy de `/api`** hacia el contenedor de la API. Eso tiene tres consecuencias:

- **No hay CORS.** El navegador ve un solo origen.
- **Recargar `/productos` no da 404.** nginx cae a `index.html` y el router
  resuelve la ruta.
- **La imagen no lleva la URL de la API dentro.** Vite congela `import.meta.env`
  al compilar, así que una imagen construida con `VITE_API_URL` quedaría atada a
  ese entorno. En su lugar el contenedor escribe `/config.js` al arrancar y el
  cliente lo lee antes que a `import.meta.env`. La misma imagen sirve para
  cualquier despliegue.

| Variable del contenedor | Para qué |
|---|---|
| `API_HOST` / `API_PORT` | A dónde apunta el proxy (en el compose: `api:8080`) |
| `API_URL` | Vacío = mismo origen. Se define sólo si la API vive en otro dominio |

Cacheo: los archivos de `/assets/` llevan hash en el nombre y se cachean un año;
`index.html` y `config.js` van con `no-store`.

## Decisiones que conviene conocer

**Variables CSS en lugar de Tailwind.** El diseño tiene un sistema de tokens con
tema claro y oscuro, y eso mapea uno a uno a custom properties: el tema oscuro
redefine variables y **ningún componente conoce un color literal**. Con Tailwind
habría que reescribir cada valor y pelear con el `dark:` en cada clase.

**Estilos en línea, no clases.** Los componentes llevan `style={{}}` que apunta a
las variables (`color: 'var(--ink2)'`). Sin cascada que sorprenda y sin un archivo
de CSS que crezca en paralelo a los componentes. `base.css` sólo tiene el reset, la
tipografía y el responsive del cascarón.

**Los filtros viven en la URL, no en `useState`.** `lib/urlFilters.ts` es el único
hook que sincroniza filtros, búsqueda y paginación con los query params: refrescar
conserva la consulta y copiar el enlace reconstruye la pantalla. Los valores por
defecto no se escriben en la URL, y cambiar un filtro descarta la página. El
selector de filas por página (**10, 15 o 25**, 15 por omisión) lo pinta `Pager`, y
el tamaño elegido se le pide a la API: la tabla nunca recorta en memoria.
Detalle en las reglas 4 y 8 de [`CLAUDE.md`](../CLAUDE.md).

**TanStack Query para el estado del servidor.** La paginación, el caché y la
invalidación tras cada mutación no se escriben a mano. Un movimiento invalida
`products`, `movements` y `reports` de una vez, porque los tres cambian.

**Los tokens viven en `localStorage`.** Sobrevive a recargar la pestaña, pero queda
expuesto a XSS. La mitigación es no tener XSS: React escapa por defecto y **no se
usa `dangerouslySetInnerHTML` en ninguna parte** (los iconos son JSX, no cadenas de
HTML). La alternativa robusta es una cookie `HttpOnly`, y eso lo tiene que emitir la
API — queda como pendiente.

---

## Renovación de sesión

El access token dura 60 minutos y el refresco 14 días —en `Development` la API sube
el access a 480 minutos—. `src/api/client.ts` lo maneja solo:

1. **Renovación anticipada** — si al access token le queda menos de un minuto, se
   canjea antes de salir, sin gastar un 401 y una segunda ida al servidor.
2. **Reintento tras 401** — si igual llega un 401, se renueva y se repite la
   petición original una vez.
3. **Una sola renovación en vuelo** — es lo importante. Sin esto, cinco peticiones
   que reciben 401 a la vez dispararían cinco canjes del **mismo** refresh token; el
   primero lo rota y los otros cuatro llegan con un token ya usado, lo que la API
   interpreta como robo y **corta la sesión completa**.
4. **Si el refresco falla** se limpia la sesión y se redirige al login.

Verificado en navegador: tres peticiones fallando simultáneamente producen
**una sola** llamada a `/api/auth/refresh`.

---

## Roles y permisos

El menú, los botones y las rutas se derivan de los **permisos**, no del nombre del
rol: un rol nuevo aparece en el menú correcto sin tocar el frontend.

```tsx
const { can } = useAuth();
const canCreate = can(P.products.create);
```

| Pieza | Qué hace |
|---|---|
| `auth/permissions.ts` → `P` | Las claves que la interfaz necesita nombrar, como constantes |
| `auth/roles.ts` → `NAV` | Cada sección declara el permiso que la habilita |
| `useAuth().can(clave)` | Comprueba un permiso del usuario en curso |
| `RequireAuth permission={…}` | Bloquea la ruta escrita a mano |
| `usePermissionCatalog()` | Pide el catálogo a `GET /api/permissions` |
| `components/PermissionMatrix` | La rejilla de módulos × acciones del editor de roles |

Con los tres roles que crea la migración: `Administrador` ve 9 secciones,
`Jefe de bodega` 7 y `Bodeguero` 4.

**El catálogo no se declara aquí.** `P` es sólo el subconjunto de claves que el código
de la interfaz menciona; el catálogo completo vive en el backend y se consume de
`GET /api/permissions` (regla 7 de [`CLAUDE.md`](../CLAUDE.md)).

**La interfaz sólo esconde lo que el permiso no habilita; la autorización real la
impone la API**, que responde `403` si se pide el endpoint directamente. Nunca se
confía en el frontend para eso.

### Editor de permisos

`pages/RoleEditor.tsx` monta la matriz: 9 módulos × 4 acciones estándar, más una
columna de **especiales** para las tres que sólo tiene un módulo (`movements.reverse`,
`reports.export`, `users.change_password`). Sin esa columna la rejilla se llenaría de
guiones.

Marcar *Crear*, *Editar* o *Eliminar* arrastra el *Ver* del módulo: sin él la pantalla
no se puede abrir y el permiso quedaría inalcanzable. Quitar *Ver* limpia el módulo
entero.

---

## Errores

Todo error de la API llega como `ProblemDetails` y se traduce en `src/api/problem.ts`:

| Status | Qué ve el usuario |
|---|---|
| `400` | El mensaje **junto al campo** que lo causó, con el texto del validador |
| `401` | Renovación automática; si falla, vuelta al login |
| `403` | Aviso "Sin permiso" |
| `404` | Estado vacío explicando que el recurso no existe |
| `409` | Aviso ámbar, no rojo: no es culpa del usuario sino una regla de negocio |
| `429` | Aviso con el tiempo de espera |
| `500` | Aviso con el `traceId` para buscar en los logs |

---

## Estructura

```
src/
├─ api/
│  ├─ client.ts      fetch + renovación de sesión + construcción de query
│  ├─ endpoints.ts   una función por endpoint
│  ├─ types.ts       espejo de los DTOs de la API
│  └─ problem.ts     ProblemDetails → ApiError tipado
├─ auth/
│  ├─ AuthContext.tsx   sesión en React, sincronizada con el cliente HTTP
│  ├─ RequireAuth.tsx   guarda de ruta, por autenticación y por permiso
│  ├─ roles.ts          menú derivado de permisos
│  └─ storage.ts        persistencia de la sesión
├─ components/
│  ├─ Shell.tsx      barra lateral + barra superior + responsive
│  ├─ DataTable.tsx  tabla densa + paginación con selector de filas
│  ├─ Field.tsx      controles de formulario con error por campo
│  ├─ Modal.tsx      modal y confirmación
│  ├─ Toast.tsx      avisos, con traducción de errores de API
│  ├─ Icon.tsx       iconos SVG en JSX
│  ├─ PermissionMatrix.tsx  módulos × acciones del editor de roles
│  ├─ tokens.ts     cómo se ve cada tipo de movimiento, en un solo lugar
│  ├─ ThemeToggle.tsx
│  └─ ui.tsx         botón, chip, tarjeta, KPI, aviso, estado vacío
├─ pages/
│  ├─ Login.tsx  Dashboard.tsx  Products.tsx  ProductDetail.tsx
│  ├─ Movements.tsx  MovementForm.tsx  Reports.tsx  Users.tsx
│  ├─ Roles.tsx      RoleEditor.tsx  la matriz de permisos por rol
│  ├─ CrudPage.tsx   patrón compartido de Categorías/Proveedores/Clientes
│  ├─ Catalog.tsx    las tres pantallas que usan ese patrón
│  └─ NoAccess.tsx   NotFound.tsx
├─ lib/
│  ├─ format.ts      pesos chilenos, fechas en 24 h, iniciales
│  ├─ urlFilters.ts  filtros, búsqueda y paginación derivados de la URL
│  └─ hooks.ts       cabecera de página
└─ styles/
   ├─ tokens.css  el sistema de diseño, claro y oscuro
   └─ base.css    reset, tipografía, responsive del cascarón
```

---

## Tests en navegador real

No hay tests unitarios de componentes: para una interfaz que es sobre todo
composición y llamadas a la API, un recorrido real en un navegador encuentra más
que un test de render aislado. La suite usa Playwright directamente.

```bash
npx playwright install chromium   # una vez

npm run e2e                                   # contra el stack dockerizado (:8080)
APP_URL=http://localhost:5173 npm run e2e     # contra el servidor de Vite
npm run e2e:proxy                             # sólo tiene sentido en Docker
```

El destino es **uno para toda la tanda**. Antes cada archivo traía su propio defecto
y tres apuntaban a Vite mientras las otras iban al contenedor: una tanda «en verde»
no verificaba un despliegue, sino dos a medias.

| Archivo | Qué verifica |
|---|---|
| `e2e/smoke.mjs` | 22 pasos: login, error de credenciales, las 9 secciones, filtro de stock bajo, aviso de stock insuficiente antes de enviar, registro real de un movimiento, tema oscuro, vista móvil de 390 px |
| `e2e/roles.mjs` | Los tres roles: cuántas secciones ve cada uno, qué botones aparecen, y que las rutas escritas a mano queden bloqueadas |
| `e2e/refresh.mjs` | Token corrupto → renueva sin expulsar; tres fallos simultáneos → **una sola** renovación; refresco inválido → login; logout → token revocado en el servidor |
| `e2e/proxy.mjs` | El despliegue dockerizado: un solo origen, sin cabeceras CORS, fallback de SPA, y que el límite de intentos **no se eluda** falsificando `X-Forwarded-For` |
| `e2e/rbac.mjs` | Roles y permisos: la matriz, que marcar Crear arrastre Ver, guardar contra la API, crear un rol partiendo de otro, el reset de contraseña, el filtro por rol resuelto en el servidor, y que un rol sin permisos vea menos |
| `e2e/filters.mjs` | Que los filtros vivan en la URL (regla 4): refrescar conserva el estado, el enlace compartido reconstruye la pantalla, cambiar un filtro descarta la página, y el selector de filas por página pide el tamaño a la API |

Las capturas quedan en `e2e/shots/`.

> Una tanda completa hace más de 10 logins y el limitador de `/api/auth` acepta 10
> por minuto, así que `npm run e2e` **espera entre suite y suite** (`e2e/run.mjs`).
> Por eso tarda varios minutos; una suite suelta es inmediata. Si corres dos tandas
> seguidas, deja pasar un minuto entre ellas.

---

## Pendiente

- Tests unitarios de `format.ts` y del cliente HTTP
- Cookie `HttpOnly` para el refresh token, en vez de `localStorage`
- Cambio de la **propia** contraseña desde la interfaz (el endpoint existe:
  `POST /api/users/me/change-password`; el reset de otro usuario ya está)
- Cierre de sesión en todos los dispositivos (el endpoint lo soporta con `allSessions`)
- Exportar reportes a CSV — el permiso `reports.export` ya existe y espera su endpoint
- Servir el frontend con HTTP/2 y TLS (hoy el contenedor habla HTTP)

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
| `npm run e2e` | Suite en navegador real (ver abajo) |

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

El access token dura 60 minutos y el refresco 14 días. `src/api/client.ts` lo maneja
solo:

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

## Roles

El menú y los botones se arman según el rol del token:

| Rol | Secciones | Escribe catálogo | Revierte movimientos |
|---|---|---|---|
| `Admin` | 8 | sí | sí |
| `Manager` | 7 (sin Usuarios) | sí | sí |
| `Operator` | 4 | no | no |

`RequireAuth` bloquea además las rutas escritas a mano. **La interfaz sólo esconde
lo que el rol no puede usar; la autorización real la impone la API**, que responde
`403` si se pide el endpoint directamente. Nunca se confía en el frontend para eso.

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
│  ├─ RequireAuth.tsx   guarda de ruta, por autenticación y por rol
│  ├─ roles.ts          menú y permisos por rol
│  └─ storage.ts        persistencia de la sesión
├─ components/
│  ├─ Shell.tsx      barra lateral + barra superior + responsive
│  ├─ DataTable.tsx  tabla densa + paginación
│  ├─ Field.tsx      controles de formulario con error por campo
│  ├─ Modal.tsx      modal y confirmación
│  ├─ Toast.tsx      avisos, con traducción de errores de API
│  ├─ Icon.tsx       iconos SVG en JSX
│  ├─ ThemeToggle.tsx
│  └─ ui.tsx         botón, chip, tarjeta, KPI, aviso, estado vacío
├─ pages/
│  ├─ Login.tsx  Dashboard.tsx  Products.tsx  ProductDetail.tsx
│  ├─ Movements.tsx  MovementForm.tsx  Reports.tsx  Users.tsx
│  ├─ CrudPage.tsx   patrón compartido de Categorías/Proveedores/Clientes
│  └─ Catalog.tsx    las tres pantallas que usan ese patrón
├─ lib/
│  ├─ format.ts   pesos chilenos, fechas en 24 h, iniciales
│  └─ hooks.ts    cabecera de página, debounce, reinicio de paginación
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

# Contra el servidor de desarrollo (:5173)
npm run e2e

# Contra el stack dockerizado (:8080)
APP_URL=http://localhost:8080 npm run e2e
npm run e2e:proxy
```

| Archivo | Qué verifica |
|---|---|
| `e2e/smoke.mjs` | 21 pasos: login, error de credenciales, las 8 secciones, filtro de stock bajo, aviso de stock insuficiente antes de enviar, registro real de un movimiento, tema oscuro, vista móvil de 390 px |
| `e2e/roles.mjs` | Los tres roles: cuántas secciones ve cada uno, qué botones aparecen, y que las rutas escritas a mano queden bloqueadas |
| `e2e/refresh.mjs` | Token corrupto → renueva sin expulsar; tres fallos simultáneos → **una sola** renovación; refresco inválido → login; logout → token revocado en el servidor |
| `e2e/proxy.mjs` | El despliegue dockerizado: un solo origen, sin cabeceras CORS, fallback de SPA, y que el límite de intentos **no se eluda** falsificando `X-Forwarded-For` |

Las capturas quedan en `e2e/shots/`.

> La suite consume el límite de intentos de `/api/auth` (10 por minuto). Si la
> corres varias veces seguidas verás `429`; espera un minuto.

---

## Pendiente

- Tests unitarios de `format.ts` y del cliente HTTP
- Cookie `HttpOnly` para el refresh token, en vez de `localStorage`
- Cambio de contraseña desde la interfaz (el endpoint existe: `POST /api/users/me/change-password`)
- Cierre de sesión en todos los dispositivos (el endpoint lo soporta con `allSessions`)
- Exportar reportes a CSV
- Servir el frontend con HTTP/2 y TLS (hoy el contenedor habla HTTP)

/**
 * Comprobaciones del despliegue con Docker: un solo origen, sin CORS, y el
 * límite de intentos que no se puede eludir falsificando cabeceras.
 *
 * Requiere el stack arriba: `docker compose up -d --build` en la raíz.
 */
const APP = process.env.APP_URL ?? 'http://localhost:8080';

let failures = 0;
const check = (ok, label, detail) => {
  console.log(`  ${ok ? '✓' : '✗'} ${label}${detail ? ` — ${detail}` : ''}`);
  if (!ok) failures++;
};

const status = async (path, init) =>
  (await fetch(`${APP}${path}`, init)).status;

// ─────────────────────────────────────────── un solo origen
console.log('\n  Un solo origen');
check(await status('/') === 200, 'la SPA responde en /');
check(await status('/productos') === 200, 'recargar una ruta interna no da 404 (fallback de SPA)');
check(await status('/healthz') === 200, 'salud del frontend');
check(await status('/health/ready') === 200, 'salud de la API a través del proxy');
check(await status('/api/products') === 401, 'la API exige token a través del proxy');

const config = await (await fetch(`${APP}/config.js`)).text();
check(
  config.includes('__STOCKHEX_CONFIG__'),
  'config.js se genera al arrancar el contenedor',
  config.trim(),
);

// ─────────────────────────────────────────── nada de CORS
console.log('\n  Sin CORS');
const preflight = await fetch(`${APP}/api/auth/login`, {
  method: 'OPTIONS',
  headers: {
    Origin: APP,
    'Access-Control-Request-Method': 'POST',
    'Access-Control-Request-Headers': 'content-type',
  },
});
check(
  !preflight.headers.get('access-control-allow-origin'),
  'no se emiten cabeceras CORS porque el origen es el mismo',
  'el navegador ni siquiera hace preflight',
);

// ─────────────────────────────────────────── cabecera no falsificable
console.log('\n  El límite de intentos no se elude falsificando X-Forwarded-For');
const attempt = (xff) => fetch(`${APP}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'X-Forwarded-For': xff },
  body: JSON.stringify({ email: 'nadie@stockhex.local', password: 'incorrecta' }),
}).then((r) => r.status);

// Se agota el cupo declarando una IP inventada.
let exhausted = false;
for (let i = 0; i < 14 && !exhausted; i++) {
  if (await attempt('203.0.113.10') === 429) exhausted = true;
}
check(exhausted, 'se alcanza el 429 tras superar el límite');

// Cambiar la IP declarada NO debería dar un cupo nuevo: nginx añade el peer
// real al final de la cabecera y la API toma ése, no el que inyecta el cliente.
const spoofed = await attempt('198.51.100.20');
check(
  spoofed === 429,
  'cambiar la IP declarada no otorga un cupo nuevo',
  `respondió ${spoofed}; con 401 la cabecera sería falsificable`,
);

console.log(`\n  ${failures === 0 ? 'todo en verde' : `${failures} fallo(s)`}`);
process.exit(failures === 0 ? 0 : 1);

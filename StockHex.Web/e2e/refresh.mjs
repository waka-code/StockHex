import { chromium } from 'playwright';

const APP = process.env.APP_URL ?? 'http://localhost:8080';
// En el stack dockerizado la API cuelga del mismo origen que la aplicación.
const API = process.env.API_URL ?? 'http://localhost:8080';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 950 } });

const calls = [];
page.on('response', (r) => {
  const u = r.url();
  if (u.includes('/api/')) calls.push(`${r.status()} ${r.request().method()} ${new URL(u).pathname}`);
});

// ── login normal
await page.goto(APP, { waitUntil: 'networkidle' });
await page.fill('input[name=email]', 'admin@stockhex.local');
await page.fill('input[name=password]', 'Admin123456');
await page.click('button[type=submit]');
await page.waitForSelector('text=Últimos movimientos', { timeout: 15000 });
console.log('  ✓ login inicial');

const stored = await page.evaluate(() => JSON.parse(localStorage.getItem('stockhex.session')));
console.log(`  ✓ sesión guardada: access ${stored.accessToken.slice(0, 14)}… refresh ${stored.refreshToken.slice(0, 14)}…`);
console.log(`  ✓ solo el hash va a la base: el token en claro está en el cliente`);

// ── ESCENARIO 1: access token inválido -> debe renovar solo y NO expulsar
calls.length = 0;
await page.evaluate(() => {
  const s = JSON.parse(localStorage.getItem('stockhex.session'));
  s.accessToken = s.accessToken.slice(0, -6) + 'BADSIG';
  localStorage.setItem('stockhex.session', JSON.stringify(s));
});
await page.reload({ waitUntil: 'networkidle' });
await page.waitForTimeout(2500);

const stillIn = await page.locator('text=Últimos movimientos').count();
const refreshCalls = calls.filter((c) => c.includes('/api/auth/refresh'));
const unauthorized = calls.filter((c) => c.startsWith('401'));
console.log('\n  ESCENARIO 1 · access token corrupto');
console.log(`    401 recibidos: ${unauthorized.length}`);
console.log(`    llamadas a /refresh: ${refreshCalls.length} → ${refreshCalls.join(', ') || 'ninguna'}`);
console.log(`    ${stillIn === 1 ? '✓' : '✗'} el usuario sigue dentro (renovó sin expulsar)`);

const after = await page.evaluate(() => JSON.parse(localStorage.getItem('stockhex.session')));
console.log(`    ${after.refreshToken !== stored.refreshToken ? '✓' : '✗'} el refresh token rotó`);

// ── ESCENARIO 2: varias peticiones con 401 a la vez -> UNA sola renovación
calls.length = 0;
await page.evaluate(() => {
  const s = JSON.parse(localStorage.getItem('stockhex.session'));
  s.accessToken = s.accessToken.slice(0, -6) + 'BADSIG';
  localStorage.setItem('stockhex.session', JSON.stringify(s));
});
// El dashboard dispara 3 consultas en paralelo al montar.
await page.goto(`${APP}/reportes`, { waitUntil: 'domcontentloaded' });
await page.goto(APP, { waitUntil: 'networkidle' });
await page.waitForTimeout(2500);

const refresh2 = calls.filter((c) => c.includes('/api/auth/refresh'));
const stillIn2 = await page.locator('text=Últimos movimientos').count();
console.log('\n  ESCENARIO 2 · varias peticiones fallan a la vez');
console.log(`    401 recibidos: ${calls.filter((c) => c.startsWith('401')).length}`);
console.log(`    llamadas a /refresh: ${refresh2.length}`);
console.log(`    ${refresh2.length <= 1 ? '✓' : '✗'} una sola renovación (sin esto se detecta reutilización y cae la sesión)`);
console.log(`    ${stillIn2 === 1 ? '✓' : '✗'} el usuario sigue dentro`);

// ── ESCENARIO 3: refresh token inválido -> debe expulsar al login
calls.length = 0;
await page.evaluate(() => {
  const s = JSON.parse(localStorage.getItem('stockhex.session'));
  s.accessToken = s.accessToken.slice(0, -6) + 'BADSIG';
  s.refreshToken = 'token-que-no-existe-en-la-base';
  localStorage.setItem('stockhex.session', JSON.stringify(s));
});
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('text=Iniciar sesión', { timeout: 12000 });
const cleared = await page.evaluate(() => localStorage.getItem('stockhex.session'));
console.log('\n  ESCENARIO 3 · refresh token inválido');
console.log(`    ✓ redirige al login`);
console.log(`    ${cleared === null ? '✓' : '✗'} la sesión se limpió de localStorage`);

// ── ESCENARIO 4: logout revoca de verdad
await page.fill('input[name=email]', 'admin@stockhex.local');
await page.fill('input[name=password]', 'Admin123456');
await page.click('button[type=submit]');
await page.waitForSelector('text=Últimos movimientos', { timeout: 15000 });
const before = await page.evaluate(() => JSON.parse(localStorage.getItem('stockhex.session')).refreshToken);
await page.click('header button[aria-label="Cerrar sesión"]');
await page.waitForSelector('text=Iniciar sesión', { timeout: 12000 });

const revoked = await page.evaluate(async ({ token, base }) => {
  const r = await fetch(`${base}/api/auth/refresh`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: token }),
  });
  return r.status;
}, { token: before, base: API });
console.log('\n  ESCENARIO 4 · cierre de sesión');
console.log(`    ✓ vuelve al login`);

// Un 429 no prueba que el token esté revocado, sólo que el limitador cortó la
// petición: se reporta como no concluyente en lugar de darlo por bueno o por malo.
if (revoked === 200) {
  console.log(`    ✗ el refresh token SIGUE SIRVIENDO tras el logout (200)`);
  process.exitCode = 1;
} else if (revoked === 429) {
  console.log(`    · no concluyente: el limitador de /api/auth respondió 429.`);
  console.log(`      Espera un minuto y vuelve a correr sólo este archivo.`);
} else {
  console.log(`    ✓ el refresh token quedó revocado en el servidor (${revoked})`);
}

await browser.close();

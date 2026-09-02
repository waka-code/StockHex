// Regla 4: los filtros de pantalla viven en la URL.
//
// Dos invariantes que este archivo comprueba en un navegador real:
//   1. Refrescar mantiene el estado de la consulta (filtros + página).
//   2. Copiar la URL y abrirla en otra sesión reconstruye la misma pantalla.
//
// Y dos que se derivan del diseño del hook: los valores por defecto no
// aparecen en la URL (queda limpia y compartible), y cambiar un filtro
// devuelve la paginación a la página 1.
import { chromium } from 'playwright';

const APP = process.env.APP_URL ?? 'http://localhost:8080';
const OUT = process.env.SHOTS ?? 'e2e/shots';
const browser = await chromium.launch();
let failures = 0;

const check = (ok, label, detail) => {
  console.log(`    ${ok ? '✓' : '✗'} ${label}${detail ? ` — ${detail}` : ''}`);
  if (!ok) failures++;
};

const login = async () => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 980 } });
  page.on('pageerror', (e) => { console.log(`    ✗ JS: ${e.message}`); failures++; });
  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.fill('input[name=email]', 'admin@stockhex.local');
  await page.fill('input[name=password]', 'Admin123456');
  await page.click('button[type=submit]');
  await page.waitForSelector('text=Últimos movimientos', { timeout: 20000 });
  return page;
};

const params = (page) => new URL(page.url()).searchParams;

/**
 * Localiza un <select> por el valor de una de sus opciones. Con el índice
 * (`select >> nth=N`) cualquier picker nuevo en la barra desplazaba el
 * objetivo y `.catch()` convertía el fallo en un ✓ silencioso.
 */
const selectByOption = (page, value) =>
  page.locator(`select:has(option[value="${value}"])`).first();

// ───────────────────────────── 1. La URL limpia no lleva los valores por defecto
console.log('\n  Filtros en la URL (regla 4)');
const page = await login();

await page.click('nav a:has-text("Productos")');
await page.waitForSelector('table tbody tr', { timeout: 15000 });
check(
  new URL(page.url()).search === '',
  'sin filtros la URL queda limpia',
  page.url().replace(APP, '') || '/',
);

// ───────────────────────────── 2. Escribir en el buscador se refleja en la URL
await page.fill('input[placeholder*="Buscar"]', 'teclado');
await page.waitForFunction(
  () => new URL(location.href).searchParams.get('search') === 'teclado',
  null,
  { timeout: 5000 },
).catch(() => {});
check(params(page).get('search') === 'teclado', 'el buscador escribe ?search en la URL',
  `?${new URL(page.url()).searchParams}`);

// ───────────────────────────── 3. Un select también, y no acumula historial
const before = await page.evaluate(() => history.length);
await selectByOption(page, 'inactive').selectOption('inactive');
await page.waitForTimeout(600);
const after = await page.evaluate(() => history.length);
check(
  after === before && params(page).get('status') === 'inactive',
  'un select escribe en la URL con replace, sin llenar el historial',
  `history.length ${before} → ${after} · ?${params(page)}`,
);

// ───────────────────────────── 4. Refrescar mantiene el estado completo
const shared = page.url();
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('input[placeholder*="Buscar"]', { timeout: 15000 });
const restored = await page.inputValue('input[placeholder*="Buscar"]');
check(page.url() === shared && restored === 'teclado',
  'refrescar reconstruye filtros y buscador', `"${restored}"`);
await page.screenshot({ path: `${OUT}/filters-01-refresh.png` });

// ───────────────────────────── 5. La URL compartida reconstruye la pantalla
//
// Se reusa la sesión en una pestaña limpia en vez de volver a hacer login: lo
// que se prueba es que la URL sola reconstruya la pantalla, y cada login extra
// consume el limitador de /api/auth (10 por minuto) que comparte toda la suite.
const otherContext = await browser.newContext({
  viewport: { width: 1440, height: 980 },
  storageState: await page.context().storageState(),
});
const other = await otherContext.newPage();
other.on('pageerror', (e) => { console.log(`    ✗ JS: ${e.message}`); failures++; });
await other.goto(shared, { waitUntil: 'networkidle' });
await other.waitForSelector('input[placeholder*="Buscar"]', { timeout: 15000 });
const shares = await other.inputValue('input[placeholder*="Buscar"]');
check(shares === 'teclado' && other.url() === shared,
  'otra sesión abre el enlace y ve el mismo estado', `"${shares}"`);
await other.screenshot({ path: `${OUT}/filters-02-compartida.png` });
await otherContext.close();

// ───────────────────────────── 6. Movimientos: rango de fechas y filtros combinados
await page.goto(
  `${APP}/movimientos?type=In&from=2020-01-01&to=2030-12-31&pageSize=25`,
  { waitUntil: 'networkidle' },
);
await page.waitForSelector('table tbody tr, :text("Sin movimientos")', { timeout: 15000 });
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('table tbody tr, :text("Sin movimientos")', { timeout: 15000 });
const mv = params(page);
check(
  mv.get('type') === 'In' && mv.get('from') === '2020-01-01'
    && mv.get('to') === '2030-12-31' && mv.get('pageSize') === '25',
  'movimientos: tipo, rango de fechas y tamaño de página sobreviven al refresh',
  `?${mv}`,
);
await page.screenshot({ path: `${OUT}/filters-03-movimientos.png` });

// ───────────────────────────── 7. Cambiar un filtro vuelve a la página 1
await page.goto(`${APP}/movimientos?page=2`, { waitUntil: 'networkidle' });
await page.waitForSelector('select', { timeout: 15000 });
await selectByOption(page, 'Adjustment').selectOption('In');
await page.waitForTimeout(600);
check(
  params(page).get('page') === null && params(page).get('type') === 'In',
  'cambiar un filtro descarta la página anterior',
  `?${params(page)}`,
);

// ───────────────────────────── 8. Un valor inválido en la URL no rompe la pantalla
await page.goto(`${APP}/productos?page=-7&status=inventado&pageSize=99999`,
  { waitUntil: 'networkidle' });
await page.waitForSelector('table tbody tr, :text("Sin productos")', { timeout: 15000 });
check(true, 'la pantalla resiste parámetros inválidos en la URL',
  'sin errores de JS y con datos en pantalla');

// ───────────────────────────── 9. El tamaño de página lo elige el usuario
//
// Lo importante no es sólo que la URL lo lleve: la petición a la API tiene que
// salir con ese pageSize. Si el front recortara en memoria, la API seguiría
// recibiendo el tamaño anterior.
await page.goto(`${APP}/productos?status=all`, { waitUntil: 'networkidle' });
await page.waitForSelector('table tbody tr', { timeout: 15000 });

const sizes = await page.$eval('select[aria-label="Filas por página"]',
  (el) => [...el.options].map((o) => o.value));
check(sizes.join(',') === '10,15,25', 'el selector ofrece 10, 15 y 25 y nada más',
  sizes.join(', '));

const asked = page.waitForRequest(
  (r) => r.url().includes('/api/products') && r.url().includes('pageSize=10'),
  { timeout: 8000 },
);
await page.selectOption('select[aria-label="Filas por página"]', '10');
const sent = await asked.then(() => true).catch(() => false);
await page.waitForTimeout(500);
check(sent && params(page).get('pageSize') === '10',
  'elegir 10 filas lo escribe en la URL y se lo pide a la API',
  `?${params(page)} · petición ${sent ? 'con pageSize=10' : 'no observada'}`);

const rows = await page.locator('table tbody tr').count();
check(rows <= 10, 'la tabla nunca trae más filas de las pedidas', `${rows} filas`);
await page.screenshot({ path: `${OUT}/filters-04-tamano-pagina.png` });

// El defecto no ensucia la URL: volver a 15 borra el parámetro.
await page.selectOption('select[aria-label="Filas por página"]', '15');
await page.waitForTimeout(600);
check(params(page).get('pageSize') === null,
  'volver al tamaño por defecto limpia el parámetro', `?${params(page)}` );

// ───────────────────────────── 10. Cambiar el tamaño vuelve a la página 1
await page.goto(`${APP}/movimientos?page=2&pageSize=10`, { waitUntil: 'networkidle' });
await page.waitForSelector('select[aria-label="Filas por página"]', { timeout: 15000 });
await page.selectOption('select[aria-label="Filas por página"]', '25');
await page.waitForTimeout(700);
check(
  params(page).get('page') === null && params(page).get('pageSize') === '25',
  'cambiar el tamaño descarta la página: la 2 de 10 en 10 no existe de 25 en 25',
  `?${params(page)}`,
);

// ───────────────────────────── 11. Un tamaño fuera del catálogo no se acepta
await page.goto(`${APP}/productos?pageSize=99999`, { waitUntil: 'networkidle' });
await page.waitForSelector('select[aria-label="Filas por página"]', { timeout: 15000 });
const fallback = await page.inputValue('select[aria-label="Filas por página"]');
check(fallback === '15', 'un pageSize inventado en la URL cae al del backend',
  `el selector quedó en ${fallback}`);

// ───────────────────────────── 12. Cada pantalla con filtros los conserva
const screens = [
  ['/usuarios', 'usuarios', 'search=admin'],
  ['/roles', 'roles', 'search=bodega'],
  ['/categorias', 'categorías', 'search=a'],
  ['/proveedores', 'proveedores', 'search=a'],
  ['/clientes', 'clientes', 'search=a'],
  ['/reportes', 'reportes', 'from=2024-01-01&to=2024-12-31'],
];
for (const [path, label, query] of screens) {
  await page.goto(`${APP}${path}?${query}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(800);
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForTimeout(800);
  const kept = new URL(page.url()).search.replace(/^\?/, '');
  const expected = new URLSearchParams(query);
  const ok = [...expected].every(([k, v]) => params(page).get(k) === v);
  check(ok, `${label}: los filtros de la URL se conservan`, `?${kept}`);
}

await page.close();
await browser.close();
console.log(failures === 0
  ? '\n  ✓ regla 4 verificada: los filtros viven en la URL\n'
  : `\n  ✗ ${failures} comprobación(es) fallida(s)\n`);
process.exit(failures === 0 ? 0 : 1);

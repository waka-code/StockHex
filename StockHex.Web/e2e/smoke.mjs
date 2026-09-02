import { chromium } from 'playwright';

const OUT = process.env.SHOTS ?? 'e2e/shots';
const APP = process.env.APP_URL ?? 'http://localhost:5173';
const errors = [];
const failedRequests = [];

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 950 } });

page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`));
page.on('requestfailed', (r) => failedRequests.push(`${r.method()} ${r.url()} — ${r.failure()?.errorText}`));
page.on('response', (r) => {
  if (r.status() >= 400 && r.url().includes('/api/')) {
    failedRequests.push(`${r.status()} ${r.request().method()} ${r.url().replace('http://localhost:8080','')}`);
  }
});

const step = async (name, fn) => {
  try { await fn(); console.log(`  ✓ ${name}`); }
  catch (e) { console.log(`  ✗ ${name} — ${e.message.split('\n')[0]}`); throw e; }
};

const shot = (n) => page.screenshot({ path: `${OUT}/${n}.png`, fullPage: false });

// ─────────────────────────────────────────── login
await step('carga /login', async () => {
  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.waitForSelector('text=Iniciar sesión', { timeout: 10000 });
});
await shot('01-login');

await step('credenciales incorrectas muestran el error', async () => {
  await page.fill('input[name=email]', 'admin@stockhex.local');
  await page.fill('input[name=password]', 'incorrecta');
  await page.click('button[type=submit]');
  await page.waitForSelector('text=Email o contraseña incorrectos', { timeout: 10000 });
});
await shot('02-login-error');

await step('login correcto entra al dashboard', async () => {
  await page.fill('input[name=password]', 'Admin123456');
  await page.click('button[type=submit]');
  await page.waitForSelector('text=Últimos movimientos', { timeout: 15000 });
});
await page.waitForTimeout(900);
await shot('03-dashboard');

await step('el rol Admin aparece en la barra superior', async () => {
  await page.waitForSelector('text=Admin', { timeout: 5000 });
});

await step('el menú tiene las 8 secciones de Admin', async () => {
  const count = await page.locator('nav a').count();
  if (count !== 8) throw new Error(`esperaba 8 items, hay ${count}`);
});

// ─────────────────────────────────────────── productos
await step('navega a Productos', async () => {
  await page.click('nav a:has-text("Productos")');
  await page.waitForSelector('text=El stock no se edita desde esta pantalla', { timeout: 10000 });
  await page.waitForTimeout(700);
});
await shot('04-productos');

await step('el filtro de stock bajo funciona', async () => {
  await page.click('button:has-text("Solo stock bajo")');
  await page.waitForTimeout(1200);
});
await shot('05-productos-stock-bajo');
await step('quita el filtro', async () => {
  await page.click('button:has-text("Solo stock bajo")');
  await page.waitForTimeout(900);
});

// ─────────────────────────────────────────── movimientos
await step('navega a Movimientos', async () => {
  await page.click('nav a:has-text("Movimientos")');
  await page.waitForSelector('text=Entradas en la página', { timeout: 10000 });
  await page.waitForTimeout(900);
});
await shot('06-movimientos');

await step('abre el formulario de movimiento', async () => {
  await page.click('header button:has-text("Registrar movimiento")');
  await page.waitForSelector('[role=dialog]', { timeout: 6000 });
  await page.waitForTimeout(800);
});
await shot('07-movimiento-modal');

await step('avisa de stock insuficiente antes de enviar', async () => {
  const select = page.locator('[role=dialog] select').first();
  const options = await select.locator('option').all();
  // Primer producto real (el índice 0 es el placeholder).
  let picked = null;
  for (const o of options.slice(1)) {
    const text = await o.textContent();
    const m = text?.match(/stock (\d+)/);
    if (m && Number(m[1]) < 100000) { picked = await o.getAttribute('value'); break; }
  }
  if (!picked) throw new Error('no hay productos para elegir');
  await select.selectOption(picked);
  await page.click('[role=dialog] button:has-text("Salida")');
  await page.fill('[role=dialog] input[type=number]', '999999');
  await page.waitForSelector('text=Stock insuficiente', { timeout: 6000 });
});
await shot('08-stock-insuficiente');

await step('el botón de enviar queda deshabilitado', async () => {
  const disabled = await page.locator('[role=dialog] button:has-text("Registrar movimiento"), footer button')
    .last().isDisabled().catch(() => null);
  const submit = page.locator('button[form=movement-form]');
  if (!(await submit.isDisabled())) throw new Error('debería estar deshabilitado');
});

await step('registra una entrada de verdad', async () => {
  await page.click('[role=dialog] button:has-text("Entrada")');
  await page.fill('[role=dialog] input[type=number]', '7');
  await page.fill('[role=dialog] textarea', 'Smoke test automatizado');
  await page.click('button[form=movement-form]');
  await page.waitForSelector('text=Movimiento registrado', { timeout: 12000 });
});
await page.waitForTimeout(700);
await shot('09-movimiento-registrado');

// ─────────────────────────────────────────── resto de secciones
for (const [label, marker, file] of [
  ['Reportes', 'Movimientos del período', '10-reportes'],
  ['Categorías', 'Una categoría con productos', '11-categorias'],
  ['Proveedores', 'Los proveedores son la contraparte', '12-proveedores'],
  ['Clientes', 'Los clientes son la contraparte', '13-clientes'],
  ['Usuarios', 'Sección exclusiva de Admin', '14-usuarios'],
]) {
  await step(`navega a ${label}`, async () => {
    await page.click(`nav a:has-text("${label}")`);
    await page.waitForSelector(`text=${marker}`, { timeout: 10000 });
    await page.waitForTimeout(800);
  });
  await shot(file);
}

// ─────────────────────────────────────────── tema oscuro
await step('el tema oscuro se aplica', async () => {
  await page.click('nav a:has-text("Dashboard")');
  await page.waitForSelector('text=Últimos movimientos', { timeout: 10000 });
  await page.click('header button[aria-label*="oscuro"], header button[aria-label*="claro"]');
  await page.waitForTimeout(600);
  const theme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
  if (theme !== 'dark') throw new Error(`data-theme=${theme}`);
});
await shot('15-dashboard-oscuro');

// ─────────────────────────────────────────── móvil
await step('vista móvil 390px', async () => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(700);
});
await shot('16-movil-dashboard');

await step('el menú lateral se abre como panel', async () => {
  await page.click('button[aria-label="Abrir menú"]');
  await page.waitForTimeout(500);
});
await shot('17-movil-menu');

console.log('');
console.log(`  errores de consola: ${errors.length}`);
errors.slice(0, 6).forEach((e) => console.log(`    · ${e.slice(0, 160)}`));
console.log(`  peticiones fallidas: ${failedRequests.length}`);
failedRequests.slice(0, 8).forEach((r) => console.log(`    · ${r.slice(0, 160)}`));

await browser.close();
process.exit(errors.length > 0 ? 1 : 0);

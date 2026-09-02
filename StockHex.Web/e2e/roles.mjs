import { chromium } from 'playwright';

const OUT = process.env.SHOTS ?? 'e2e/shots';
const APP = process.env.APP_URL ?? 'http://localhost:8080';
const browser = await chromium.launch();

const check = async (email, password, expected) => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 950 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.fill('input[name=email]', email);
  await page.fill('input[name=password]', password);
  await page.click('button[type=submit]');
  await page.waitForSelector('text=Últimos movimientos', { timeout: 15000 });

  const nav = await page.locator('nav a').allTextContents();
  const role = (await page
    .locator('header span', { hasText: /^(Administrador|Jefe de bodega|Bodeguero)$/ })
    .first().textContent())?.trim();

  console.log(`\n  ${expected.role} (${email})`);
  console.log(`    rol en la barra: ${role}`);
  console.log(`    menú (${nav.length}): ${nav.join(', ')}`);

  const ok = nav.length === expected.nav.length && expected.nav.every((n) => nav.includes(n));
  console.log(`    ${ok ? '✓' : '✗'} secciones esperadas: ${expected.nav.length}`);

  // Productos: ¿aparece el botón de alta y la columna de acciones?
  await page.click('nav a:has-text("Productos")');
  await page.waitForSelector('table', { timeout: 10000 });
  await page.waitForTimeout(900);
  const newProduct = await page.locator('header button:has-text("Nuevo producto")').count();
  const editButtons = await page.locator('table button[aria-label="Editar"]').count();
  console.log(`    ${newProduct === expected.canCreate ? '✓' : '✗'} botón "Nuevo producto": ${newProduct}`);
  console.log(`    ${(editButtons > 0) === (expected.canCreate === 1) ? '✓' : '✗'} botones de editar en filas: ${editButtons}`);
  await page.screenshot({ path: `${OUT}/rol-${expected.role.toLowerCase().replace(/ /g, '-')}-productos.png` });

  // Movimientos: ¿aparece "Revertir"?
  await page.click('nav a:has-text("Movimientos")');
  await page.waitForSelector('table', { timeout: 10000 });
  await page.waitForTimeout(900);
  const revertButtons = await page.locator('table button:has-text("Revertir")').count();
  console.log(`    ${(revertButtons > 0) === expected.canReverse ? '✓' : '✗'} botones "Revertir": ${revertButtons}`);
  await page.screenshot({ path: `${OUT}/rol-${expected.role.toLowerCase().replace(/ /g, '-')}-movimientos.png` });

  // Ruta prohibida escrita a mano
  await page.goto(`${APP}/usuarios`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1100);
  const blocked = await page.locator('text=Esta sección no está disponible para tu rol').count();
  const reachedUsers = await page.locator('text=roles configurables').count();
  const expectBlocked = expected.role !== 'Administrador';
  const guardOk = expectBlocked ? blocked === 1 : reachedUsers === 1;
  console.log(`    ${guardOk ? '✓' : '✗'} /usuarios escrito a mano: ${expectBlocked ? 'bloqueado' : 'permitido'}`);
  if (expectBlocked) await page.screenshot({ path: `${OUT}/rol-${expected.role.toLowerCase().replace(/ /g, '-')}-sin-acceso.png` });

  if (errors.length) console.log(`    ✗ errores JS: ${errors.join(' | ')}`);
  await page.close();
};

// Los roles ya no son un enum: son las tres filas que creó la migración.
await check('admin@stockhex.local', 'Admin123456', {
  role: 'Administrador', canCreate: 1, canReverse: true,
  nav: ['Dashboard', 'Productos', 'Movimientos', 'Reportes', 'Categorías', 'Proveedores',
        'Clientes', 'Usuarios', 'Roles'],
});

await check('carla@stockhex.cl', 'Manager1234', {
  role: 'Jefe de bodega', canCreate: 1, canReverse: true,
  nav: ['Dashboard', 'Productos', 'Movimientos', 'Reportes', 'Categorías', 'Proveedores', 'Clientes'],
});

await check('juan@stockhex.cl', 'Operario123', {
  role: 'Bodeguero', canCreate: 0, canReverse: false,
  nav: ['Dashboard', 'Productos', 'Movimientos', 'Reportes'],
});

await browser.close();

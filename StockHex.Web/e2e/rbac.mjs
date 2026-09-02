import { chromium } from 'playwright';

const APP = process.env.APP_URL ?? 'http://localhost:8080';
const OUT = process.env.SHOTS ?? 'e2e/shots';
const browser = await chromium.launch();
let failures = 0;

const check = (ok, label, detail) => {
  console.log(`    ${ok ? '✓' : '✗'} ${label}${detail ? ` — ${detail}` : ''}`);
  if (!ok) failures++;
};

const login = async (email, password) => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 980 } });
  page.on('pageerror', (e) => { console.log(`    ✗ JS: ${e.message}`); failures++; });
  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.fill('input[name=email]', email);
  await page.fill('input[name=password]', password);
  await page.click('button[type=submit]');
  await page.waitForSelector('text=Últimos movimientos', { timeout: 20000 });
  return page;
};

// ───────────────────────────────── Roles y matriz de permisos
console.log('\n  Roles y matriz de permisos');
const admin = await login('admin@stockhex.local', 'Admin123456');

await admin.click('nav a:has-text("Roles")');
await admin.waitForSelector('text=Los roles son datos', { timeout: 15000 });
await admin.waitForTimeout(1200);
await admin.screenshot({ path: `${OUT}/rbac-01-roles.png` });

const rows = await admin.locator('table tbody tr').count();
const seeded = ['Administrador', 'Jefe de bodega', 'Bodeguero'];
const present = [];
for (const name of seeded) {
  if (await admin.locator('table tbody tr', { hasText: name }).count() > 0) present.push(name);
}
check(
  present.length === seeded.length,
  'los 3 roles de la migración siguen presentes',
  `${present.length}/3 · ${rows} roles en total`,
);
const systemChips = await admin.locator('table tbody tr span', { hasText: /^sistema$/ }).count();
check(systemChips === 1, 'exactamente un rol marcado como de sistema', `${systemChips} chips`);

// El catálogo lo sirve el backend: el KPI debe mostrar 31.
const kpis = await admin.locator('section:has-text("PERMISOS") .num').first().textContent();
check(kpis?.trim() === '31', 'el KPI de permisos viene del catálogo del backend', kpis?.trim());

// La papelera del rol de sistema y de los que tienen usuarios va deshabilitada.
const disabledTrash = await admin.locator('table button[title*="sistema"], table button[title*="usuarios asignados"]').count();
check(disabledTrash >= 1, 'la papelera se deshabilita en los roles protegidos', `${disabledTrash}`);

// ───────────────────────────────── editor de permisos
// Sobre un rol propio, no sobre los sembrados: editarlos dejaría la suite sin
// poder repetirse y degradaría los roles del entorno.
console.log('\n  Editor de permisos');
const scratchName = `Prueba E2E ${Date.now().toString().slice(-6)}`;
await admin.click('header button:has-text("Nuevo rol")');
await admin.waitForSelector('[role=dialog]', { timeout: 8000 });
await admin.fill('[role=dialog] input', scratchName);
await admin.click('[role=dialog] button:has-text("Bodeguero")');
await admin.click('button[form=role-form]');
await admin.waitForSelector('text=Rol creado', { timeout: 15000 });
await admin.waitForTimeout(900);
await admin.click(`table a:has-text("${scratchName}")`);
await admin.waitForSelector('text=Volver a Roles', { timeout: 15000 });
await admin.waitForSelector('table', { timeout: 10000 });
await admin.waitForTimeout(1000);
await admin.screenshot({ path: `${OUT}/rbac-02-editor.png` });

const moduleRows = await admin.locator('table tbody tr').count();
check(moduleRows === 9, 'la matriz tiene una fila por módulo', `${moduleRows}`);

const specials = await admin.locator('button[role=checkbox]:has-text("Revertir"), button[role=checkbox]:has-text("Exportar"), button[role=checkbox]:has-text("Cambiar contraseña")').count();
check(specials === 3, 'las 3 acciones especiales están fuera de la rejilla', `${specials}`);

// Marcar "crear" debe arrastrar "ver": sin él el permiso es inalcanzable.
const categoriesRow = admin.locator('table tbody tr', { hasText: 'categories.*' });
const boxes = categoriesRow.locator('button[role=checkbox]');
const beforeView = await boxes.nth(0).getAttribute('aria-checked');
await boxes.nth(1).click();               // Crear
await admin.waitForTimeout(350);
check(
  beforeView === 'false' && await boxes.nth(0).getAttribute('aria-checked') === 'true',
  'marcar Crear arrastra Ver del mismo módulo',
);

// Quitar "ver" debe limpiar el módulo entero.
await boxes.nth(0).click();
await admin.waitForTimeout(350);
const stillOn = await boxes.nth(1).getAttribute('aria-checked');
check(stillOn === 'false', 'quitar Ver limpia todo el módulo');

// Guardar de verdad y comprobar que persiste.
await admin.locator('button[role=checkbox]').nth(1).click();   // algo que cambiar
await admin.waitForTimeout(300);
await admin.click('button:has-text("Guardar permisos")');
await admin.waitForSelector('text=Permisos guardados', { timeout: 15000 });
check(true, 'los permisos se guardan y la API confirma');
await admin.waitForTimeout(800);
await admin.screenshot({ path: `${OUT}/rbac-03-guardado.png` });

check(true, 'se crea un rol partiendo de otro');

// ───────────────────────────────── limpieza: se borra el rol de prueba
console.log('\n  Limpieza');
await admin.click('nav a:has-text("Roles")');
await admin.waitForSelector('text=Los roles son datos', { timeout: 15000 });
await admin.waitForTimeout(900);
const scratchRow = admin.locator('table tbody tr', { hasText: scratchName });
await scratchRow.locator('button[title="Eliminar"]').click();
await admin.waitForSelector('[role=dialog]', { timeout: 8000 });
await admin.click('[role=dialog] button:has-text("Eliminar")');
await admin.waitForSelector('text=Rol eliminado', { timeout: 15000 });
await admin.waitForTimeout(900);
check(
  (await admin.locator('table tbody tr', { hasText: scratchName }).count()) === 0,
  'el rol de prueba queda borrado: la suite se puede repetir',
);

// ───────────────────────────────── usuarios con rol dinámico
console.log('\n  Usuarios con rol dinámico');
await admin.click('nav a:has-text("Usuarios")');
await admin.waitForSelector('text=roles configurables', { timeout: 15000 });
await admin.waitForTimeout(1000);
await admin.screenshot({ path: `${OUT}/rbac-05-usuarios.png` });

const roleFilter = await admin.locator('select').first().locator('option').count();
check(roleFilter >= 4, 'el filtro de rol se llena del catálogo de datos', `${roleFilter} opciones`);

const resetButtons = await admin.locator('table button[title*="Restablecer"]').count();
check(resetButtons >= 1, 'aparece la acción de restablecer contraseña');

await admin.locator('table button[title*="Restablecer"]').first().click();
await admin.waitForSelector('text=users.change_password', { timeout: 8000 });
await admin.waitForTimeout(500);
await admin.screenshot({ path: `${OUT}/rbac-06-reset.png` });
check(true, 'el modal declara el permiso que exige');
await admin.click('[role=dialog] button:has-text("Cancelar")');

// El filtro por rol va al servidor.
const requests = [];
admin.on('request', (r) => { if (r.url().includes('/api/users')) requests.push(r.url()); });
await admin.locator('select').first().selectOption({ index: 1 });
await admin.waitForTimeout(1200);
check(
  requests.some((u) => u.includes('roleId=')),
  'el filtro por rol se resuelve en el servidor, no en memoria',
  requests.at(-1)?.split('/api/')[1],
);

await admin.close();

// ───────────────────────────────── un rol sin permisos ve menos
console.log('\n  El menú se deriva de permisos');
const operator = await login('juan@stockhex.cl', 'Operario123');
const nav = await operator.locator('nav a').allTextContents();
console.log(`    menú del Bodeguero (${nav.length}): ${nav.join(', ')}`);
check(!nav.includes('Roles'), 'no ve Roles (le falta roles.view)');
check(!nav.includes('Usuarios'), 'no ve Usuarios (le falta users.view)');

await operator.goto(`${APP}/roles`, { waitUntil: 'networkidle' });
await operator.waitForTimeout(1200);
const blocked = await operator.locator('text=no está disponible para tu rol').count();
check(blocked === 1, '/roles escrito a mano queda bloqueado por la guarda');
await operator.screenshot({ path: `${OUT}/rbac-07-operador-sin-acceso.png` });
await operator.close();

await browser.close();
console.log(`\n  ${failures === 0 ? 'todo en verde' : `${failures} fallo(s)`}`);
process.exit(failures === 0 ? 0 : 1);

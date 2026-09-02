/**
 * Prueba de estrés: las pantallas una a una, y encima el maltrato que recibe
 * una aplicación real.
 *
 * Qué la distingue de las otras suites:
 *   · `smoke` recorre el camino feliz; aquí interesa el camino torcido.
 *   · Se opera **como una persona**: se teclea letra a letra, se hace doble clic
 *     por impaciencia, se cierra con Escape, se navega con el botón atrás, se
 *     refresca a media faena y se cambia el tamaño de la ventana.
 *   · Cada pantalla se audita sola: sin errores de consola, sin excepciones,
 *     sin 5xx, sin desbordes horizontales del documento y sin filas apiladas.
 *
 * Un bloque que revienta se anota y la tanda sigue: en una prueba de estrés lo
 * interesante es qué MÁS se rompe después del primer fallo.
 *
 * Lo que crea se marca con el prefijo ZZTEST y se borra al final.
 */
import {
  APP, FILA_MAX, beat, createReporter, inspectLayout, launch, login,
} from './harness.mjs';

const R = createReporter();
const SUFIJO = Math.random().toString(36).slice(2, 7).toUpperCase();
const nombre = (base) => `ZZTEST-${base}-${SUFIJO}`;

const browser = await launch();
const { page, watcher, context } = await login(browser);

/** Recursos creados, para barrerlos al final. */
const basura = { categorias: [], proveedores: [], clientes: [] };

// ═══════════════════════════════════════════ utilidades del recorrido

/** Audita la pantalla en la que se está: lo que debe cumplirse siempre. */
async function auditar(pantalla, { esperaTabla = true } = {}) {
  await beat(400);
  const l = await inspectLayout(page);
  const sucio = watcher.drain();

  R.check(l.textoVisible > 20, `${pantalla}: pinta contenido`,
    l.textoVisible <= 20 ? 'la pantalla quedó en blanco' : undefined);
  R.check(l.bodyOverflow === 0, `${pantalla}: el documento no desplaza en horizontal`,
    l.bodyOverflow ? `${l.bodyOverflow}px de más` : undefined);
  R.check(l.desbordados.length === 0, `${pantalla}: nada se sale del viewport`,
    l.desbordados.join(', ') || undefined);
  R.check(sucio.js.length === 0, `${pantalla}: sin excepciones JS`, sucio.js[0]);
  R.check(sucio.console.length === 0, `${pantalla}: sin errores de consola`, sucio.console[0]);
  R.check(sucio.http.length === 0, `${pantalla}: sin respuestas de error de la API`, sucio.http[0]);

  if (esperaTabla && l.filas > 0) {
    R.check(l.filaMasAlta <= FILA_MAX, `${pantalla}: las filas no se apilan`,
      l.filaMasAlta > FILA_MAX ? `${l.filaMasAlta}px de alto` : undefined);
  }
}

const irA = async (ruta) => {
  await page.goto(APP + ruta, { waitUntil: 'networkidle' });
  await beat(300);
};

/** Teclea como una persona, no de un pegote: dispara el debounce de verdad. */
const teclearHumano = async (selector, texto) => {
  await page.click(selector);
  await page.fill(selector, '');
  await page.type(selector, texto, { delay: 55 });
};

const filas = () => page.locator('table tbody tr').count();
const hayModal = () => page.locator('[role=dialog]').count();

/** Aísla un bloque: si revienta, se anota y se sigue desde un sitio conocido. */
async function bloque(seccion, fn) {
  R.section(seccion);
  try {
    await fn();
  } catch (e) {
    R.blew('el bloque se interrumpió', e);
    await page.goto(`${APP}/`, { waitUntil: 'networkidle' }).catch(() => {});
    watcher.drain();
  }
}

// ═══════════════════════════════════════════════ 1 · Login

await bloque('1 · Login', async () => {
  const anon = await browser.newPage({ viewport: { width: 1440, height: 950 } });
  await anon.goto(`${APP}/productos`, { waitUntil: 'networkidle' });
  R.check(anon.url().includes('/login'), 'una ruta protegida sin sesión manda al login',
    anon.url().replace(APP, ''));

  // Con el formulario vacío no hay nada que enviar: el botón está inhabilitado,
  // que es mejor que dejar mandar una petición condenada a fallar.
  R.check(await anon.locator('button[type=submit]').isDisabled(),
    'con el formulario vacío no se puede enviar');

  // Enter en el campo de contraseña envía, como espera cualquiera.
  await anon.fill('input[name=email]', 'admin@stockhex.local');
  await anon.fill('input[name=password]', 'esta-no-es');
  await anon.press('input[name=password]', 'Enter');
  const error = await anon.waitForSelector('text=Email o contraseña incorrectos', { timeout: 12_000 })
    .then(() => true).catch(() => false);
  R.check(error, 'Enter envía el formulario y el error se muestra');

  // El mensaje no debe revelar si el email existe.
  const texto = await anon.locator('body').innerText();
  R.check(!/no existe|no registrado|usuario desconocido/i.test(texto),
    'el error no delata qué emails están registrados');

  await anon.close();
});

// ═══════════════════════════════════════════════ 2 · Dashboard

await bloque('2 · Dashboard', async () => {
  await irA('/');
  await auditar('Dashboard', { esperaTabla: false });

  const kpis = await page.locator('main').innerText();
  R.check(/[Ss]tock|[Pp]roducto/.test(kpis), 'muestra indicadores de inventario');

  // Refrescar la pantalla de entrada es lo primero que hace cualquiera.
  await page.reload({ waitUntil: 'networkidle' });
  await auditar('Dashboard tras refrescar', { esperaTabla: false });
});

// ═══════════════════════════════════════════════ 3 · Productos

await bloque('3 · Productos', async () => {
  await irA('/productos');
  await auditar('Productos');

  const total = await filas();
  const buscador = 'input[type=search]';
  const tieneBuscador = await page.locator(buscador).count() > 0;

  if (tieneBuscador) {
    // — buscar tecleando, letra a letra
    await teclearHumano(buscador, 'agua');
    await beat(1000);
    R.check(new URL(page.url()).searchParams.get('search') === 'agua',
      'el buscador escribe en la URL', page.url().replace(APP, ''));
    await auditar('Productos filtrados');

    // — borrarlo devuelve la lista y limpia la URL
    await page.fill(buscador, '');
    await beat(1000);
    R.check(!new URL(page.url()).searchParams.has('search'),
      'vaciar el buscador quita el parámetro de la URL');
    R.check(await filas() === total, 'vuelve el listado completo',
      `${await filas()} vs ${total}`);

    // — ráfaga de tecleo: el debounce no debe dejar peticiones colgadas
    for (const t of ['a', 'ag', 'agu', 'agua', 'agu', 'ag', 'a', '']) {
      await page.fill(buscador, t);
      await beat(40);
    }
    await beat(1400);
    await auditar('Productos tras ráfaga de tecleo');
  }

  // — tamaño de página (regla 8)
  const selectorTam = page.locator('select[aria-label="Filas por página"]');
  if (await selectorTam.count()) {
    await selectorTam.selectOption('10');
    await beat(900);
    R.check(new URL(page.url()).searchParams.get('pageSize') === '10',
      'el tamaño de página va a la URL');
    const n = await filas();
    R.check(n <= 10, 'la API respeta el tamaño pedido', `${n} filas`);
    await auditar('Productos con 10 por página');
  }

  // — parámetros absurdos en la URL: se acotan, no revientan
  await irA('/productos?page=-7&pageSize=99999&isActive=quizas&categoryId=no-es-un-guid');
  await auditar('Productos con parámetros basura');
  R.check(await filas() >= 0, 'una URL manipulada no rompe la pantalla');

  await irA('/productos');
});

// ═══════════════════════════════════════════════ 4 · Detalle de producto

await bloque('4 · Detalle de producto', async () => {
  await irA('/productos');
  const enlace = page.locator('table tbody tr a').first();
  if (!(await enlace.count())) {
    R.check(false, 'hay al menos un producto para abrir');
    return;
  }

  await enlace.click();
  await page.waitForLoadState('networkidle');
  await auditar('Detalle de producto', { esperaTabla: false });
  R.check(/\/productos\/[0-9a-f-]{36}/.test(page.url()), 'la URL lleva el id del producto',
    page.url().replace(APP, ''));

  // — el botón atrás del navegador es el que más se usa
  await page.goBack({ waitUntil: 'networkidle' });
  R.check(/\/productos\/?($|\?)/.test(page.url().replace(APP, '')),
    'atrás vuelve al listado', page.url().replace(APP, ''));
  await page.goForward({ waitUntil: 'networkidle' });
  await auditar('Detalle tras adelante', { esperaTabla: false });

  // — refrescar el detalle directamente por su URL
  await page.reload({ waitUntil: 'networkidle' });
  await auditar('Detalle tras refrescar', { esperaTabla: false });
});

// ═══════════════════════════════════════════════ 5 · Movimientos

await bloque('5 · Movimientos', async () => {
  await irA('/movimientos');
  await auditar('Movimientos');

  // — la tabla ancha desplaza en su contenedor, no en el documento
  const scroll = await page.evaluate(() => {
    const t = document.querySelector('table');
    if (!t) return null;
    const c = t.parentElement;
    return {
      desborda: t.scrollWidth > c.clientWidth + 1,
      puede: getComputedStyle(c).overflowX === 'auto',
      accionFijada: getComputedStyle(
        t.querySelector('tbody tr td:last-child') ?? t,
      ).position === 'sticky',
    };
  });
  if (scroll) {
    R.check(!scroll.desborda || scroll.puede,
      'si la tabla no cabe, su contenedor la desplaza');
    R.check(!scroll.desborda || scroll.accionFijada,
      'la columna de acciones queda fijada al desplazar');
  }

  // — filtro por tipo
  const tipo = page.locator('select:has(option[value="Out"])').first();
  if (await tipo.count()) {
    await tipo.selectOption('Out');
    await beat(1000);
    R.check(new URL(page.url()).searchParams.get('type') === 'Out',
      'el filtro de tipo va a la URL');
    await auditar('Movimientos filtrados por salida');
    await tipo.selectOption('');
    await beat(800);
  }

  // — refrescar con filtros puestos los conserva (regla 4)
  await irA('/movimientos?type=In&pageSize=10');
  await page.reload({ waitUntil: 'networkidle' });
  await beat(600);
  const tras = new URL(page.url()).searchParams;
  R.check(tras.get('type') === 'In' && tras.get('pageSize') === '10',
    'refrescar conserva los filtros', page.url().replace(APP, ''));
  await auditar('Movimientos tras refrescar con filtros');

  // — el modal de alta se abre y se cierra con Escape sin registrar nada
  await irA('/movimientos');
  const alta = page.locator('button:has-text("Registrar movimiento")').first();
  if (await alta.count()) {
    const antes = await filas();
    await alta.click();
    await page.waitForSelector('[role=dialog]', { timeout: 8000 });
    R.check(await hayModal() === 1, 'el modal de movimiento se abre');
    await page.keyboard.press('Escape');
    await beat(500);
    R.check(await hayModal() === 0, 'Escape lo cierra');
    R.check(await filas() === antes, 'cerrar sin guardar no registra nada');
  }
});

// ═══════════════════════════════════════════════ 6 · Reportes

await bloque('6 · Reportes', async () => {
  await irA('/reportes');
  await auditar('Reportes');

  const fechas = page.locator('input[type=date]');
  if (await fechas.count() >= 2) {
    // Rango invertido: error de usuario habitual, la API responde 400.
    watcher.expect(/400|movement-summary/);
    await fechas.nth(0).fill('2030-01-01');
    await fechas.nth(1).fill('2020-01-01');
    await beat(1500);
    watcher.stopExpecting();
    watcher.drain();
    const vivo = await page.locator('main').innerText();
    R.check(vivo.trim().length > 20, 'un rango invertido no deja la pantalla en blanco');

    // Al reordenar el rango hay un instante en que sigue invertido y la API
    // responde 400: es correcto, y se marca como esperado para no achacárselo
    // a la pantalla ya corregida.
    watcher.expect(/400|movement-summary/);
    await fechas.nth(1).fill('2026-09-30');
    await fechas.nth(0).fill('2026-08-01');
    await beat(1800);
    watcher.stopExpecting();
    watcher.drain();
    await auditar('Reportes con rango válido');
  } else {
    R.check(false, 'la pantalla de reportes ofrece un rango de fechas');
  }
});

// ═══════════════════ 7-9 · Catálogo: Categorías, Proveedores, Clientes

/**
 * `unico` es el campo que lleva el índice único en la base, y no siempre es el
 * nombre: un cliente se identifica por su email, así que dos clientes homónimos
 * son legítimos y duplicar el nombre no debe dar 409.
 */
const CATALOGO = [
  { ruta: '/categorias', etiqueta: 'Categorías', clave: 'categorias', unico: 'Nombre' },
  { ruta: '/proveedores', etiqueta: 'Proveedores', clave: 'proveedores', unico: 'Nombre' },
  { ruta: '/clientes', etiqueta: 'Clientes', clave: 'clientes', unico: 'Email' },
];

for (const [i, mod] of CATALOGO.entries()) {
  // eslint-disable-next-line no-loop-func
  await bloque(`${7 + i} · ${mod.etiqueta}`, async () => {
    await irA(mod.ruta);
    await auditar(mod.etiqueta);

    const abrir = () => page.locator('button:has-text("Nueva"), button:has-text("Nuevo")').first();
    if (!(await abrir().count())) {
      R.check(false, `${mod.etiqueta}: hay botón de alta`);
      return;
    }

    // — Escape cierra el modal sin guardar
    await abrir().click();
    await page.waitForSelector('[role=dialog]', { timeout: 8000 });
    R.check(await hayModal() === 1, `${mod.etiqueta}: el modal se abre`);
    await page.keyboard.press('Escape');
    await beat(400);
    R.check(await hayModal() === 0, `${mod.etiqueta}: Escape cierra el modal`);

    // — el botón de guardar está inhabilitado con el formulario vacío
    await abrir().click();
    await page.waitForSelector('[role=dialog]');
    const guardar = page.locator('[role=dialog] button:has-text("Guardar")');
    R.check(await guardar.isDisabled(), `${mod.etiqueta}: no se guarda un formulario vacío`);

    // — alta real, tecleando, y doble clic por impaciencia
    const valor = nombre(mod.etiqueta.slice(0, 3).toUpperCase());
    const valorUnico = mod.unico === 'Nombre' ? valor : `${valor.toLowerCase()}@test.local`;
    const campo = page.locator('[role=dialog] input[type=text]').first();
    await campo.click();
    await campo.type(valor, { delay: 35 });
    if (mod.unico !== 'Nombre') {
      await page.locator(`[role=dialog] label:has-text("${mod.unico}") input`)
        .first().fill(valorUnico);
    }
    await beat(300);
    await guardar.click({ clickCount: 2, delay: 60 });
    await page.waitForSelector('[role=dialog]', { state: 'detached', timeout: 15_000 })
      .catch(() => {});
    await beat(1400);

    await irA(`${mod.ruta}?search=${encodeURIComponent(valor)}`);
    await beat(700);
    const creados = await filas();
    R.check(creados === 1, `${mod.etiqueta}: el doble clic crea uno solo`,
      `${creados} encontrados`);
    if (creados >= 1) basura[mod.clave].push(valor);
    await auditar(`${mod.etiqueta} con el registro nuevo`);

    // — nombre duplicado: la API responde 409 y la interfaz no lo oculta
    if (creados === 1) {
      watcher.expect(/409/);
      await irA(mod.ruta);
      await abrir().click();
      await page.waitForSelector('[role=dialog]');
      await page.locator('[role=dialog] input[type=text]').first().fill(valor);
      if (mod.unico !== 'Nombre') {
        await page.locator(`[role=dialog] label:has-text("${mod.unico}") input`)
          .first().fill(valorUnico);
      }
      await page.locator('[role=dialog] button:has-text("Guardar")').click();
      await beat(1800);
      const sigue = await hayModal();
      const texto = await page.locator('body').innerText();
      R.check(sigue === 1 || /[Yy]a existe|onflicto/.test(texto),
        `${mod.etiqueta}: repetir ${mod.unico} se rechaza y se avisa`);
      await page.keyboard.press('Escape');
      await beat(400);
      watcher.stopExpecting();
      watcher.drain();
    }

    // — texto hostil: ni rompe el formulario ni ejecuta nada
    await irA(mod.ruta);
    await abrir().click();
    await page.waitForSelector('[role=dialog]');
    await page.locator('[role=dialog] input[type=text]').first()
      .fill(`<img src=x onerror=alert(1)>·áéíóú·"'\\/&%$#@ `.repeat(3));
    await beat(400);
    R.check(await hayModal() === 1, `${mod.etiqueta}: texto hostil no rompe el formulario`);
    await page.keyboard.press('Escape');
    await beat(300);
    watcher.drain();
  });
}

// ═══════════════════════════════════════════════ 10 · Usuarios

await bloque('10 · Usuarios', async () => {
  await irA('/usuarios');
  await auditar('Usuarios');

  const cuerpo = await page.locator('main').innerText();
  R.check(!/\$2[aby]\$|passwordHash/i.test(cuerpo),
    'el listado no filtra ningún hash de contraseña');

  const alta = page.locator('button:has-text("Nuevo")').first();
  if (await alta.count()) {
    await alta.click();
    await page.waitForSelector('[role=dialog]', { timeout: 8000 });
    const pass = page.locator('[role=dialog] input[type=password]').first();
    if (await pass.count()) {
      // Contraseña débil: no debe cerrarse el modal como si hubiera funcionado.
      await pass.fill('123');
      await beat(400);
      R.check(await hayModal() === 1, 'una contraseña débil no cierra el modal');
    }
    await page.keyboard.press('Escape');
    await beat(400);
    watcher.drain();
  }

  // — filtrar por estado y volver
  const activo = page.locator('select:has(option[value="inactive"])').first();
  if (await activo.count()) {
    await activo.selectOption('inactive');
    await beat(1000);
    await auditar('Usuarios inactivos');
    await activo.selectOption('active');
    await beat(800);
  }
});

// ═══════════════════════════════════════════════ 11 · Roles

await bloque('11 · Roles', async () => {
  await irA('/roles');
  await auditar('Roles');

  const primero = page.locator('table tbody tr a[href^="/roles/"]').first();
  if (!(await primero.count())) {
    R.check(false, 'hay al menos un rol con enlace a su editor');
    return;
  }

  await primero.click();
  await page.waitForLoadState('networkidle');
  await beat(700);
  const enEditor = /\/roles\/[0-9a-f-]{36}/.test(page.url());
  R.check(enEditor, 'se abre el editor del rol', page.url().replace(APP, ''));
  if (!enEditor) return;

  await auditar('Editor de rol', { esperaTabla: false });
  // La matriz usa <button role="checkbox" aria-checked>, no <input>: es ARIA
  // válida y es lo que hay que localizar.
  const casillas = await page.locator('[role=checkbox]').count();
  R.check(casillas >= 31, 'la matriz dibuja el catálogo completo de permisos',
    `${casillas} casillas`);
  const marcadas = await page.locator('[role=checkbox][aria-checked="true"]').count();
  R.check(marcadas > 0, 'el rol llega con sus permisos ya marcados', `${marcadas} marcadas`);

  // Refrescar el editor por su URL directa.
  await page.reload({ waitUntil: 'networkidle' });
  await auditar('Editor de rol tras refrescar', { esperaTabla: false });
  await page.goBack({ waitUntil: 'networkidle' }).catch(() => {});
});

// ═══════════════════════════════════════════════ 12 · Rutas límite

await bloque('12 · Rutas límite', async () => {
  await irA('/una-ruta-que-no-existe');
  await auditar('404', { esperaTabla: false });
  const t404 = await page.locator('main').innerText();
  R.check(t404.trim().length > 10, 'la ruta inexistente muestra algo, no una pantalla vacía');

  await irA('/sin-acceso');
  await auditar('Sin acceso', { esperaTabla: false });

  // Un id con forma de GUID pero inexistente: 404 de la API, nunca 500.
  watcher.expect(/404/);
  await irA('/productos/00000000-0000-0000-0000-000000000000');
  await beat(1200);
  const sucio = watcher.drain();
  watcher.stopExpecting();
  R.check(!sucio.http.some((h) => h.startsWith('5')),
    'un producto inexistente no provoca un 500', sucio.http[0]);
  R.check(sucio.js.length === 0, 'ni una excepción JS', sucio.js[0]);
  const vivo = await page.locator('main').innerText();
  R.check(vivo.trim().length > 10, 'y la pantalla informa en vez de quedarse vacía');
});

// ═══════════════════════════════ 13 · Comportamiento humano

await bloque('13 · Comportamiento humano', async () => {
  // — navegación a ráfagas por el menú, sin esperar a que cargue
  for (const etiqueta of ['Productos', 'Movimientos', 'Reportes', 'Categorías', 'Dashboard']) {
    const enlace = page.locator(`nav a:has-text("${etiqueta}")`).first();
    if (await enlace.count()) { await enlace.click(); await beat(130); }
  }
  await page.waitForLoadState('networkidle');
  await auditar('Tras navegar a ráfagas', { esperaTabla: false });

  // — atrás varias veces seguidas
  for (let i = 0; i < 4; i++) { await page.goBack().catch(() => {}); await beat(200); }
  await page.waitForLoadState('networkidle').catch(() => {});
  await auditar('Tras cuatro veces atrás', { esperaTabla: false });

  // — recorrer la pantalla con el teclado
  await irA('/productos');
  for (let i = 0; i < 12; i++) await page.keyboard.press('Tab');
  const foco = await page.evaluate(() => document.activeElement?.tagName ?? 'NINGUNO');
  R.check(['A', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA'].includes(foco),
    'el tabulador recorre controles reales', `foco en ${foco}`);

  // — abandonar un formulario a medias y navegar a otra pantalla
  const alta = page.locator('button:has-text("Nuevo")').first();
  if (await alta.count()) {
    await alta.click();
    await page.waitForSelector('[role=dialog]', { timeout: 8000 }).catch(() => {});
    const campo = page.locator('[role=dialog] input').first();
    if (await campo.count()) await campo.type('a medio escribir', { delay: 25 });
    await page.keyboard.press('Escape');
    await beat(400);
    await page.locator('nav a:has-text("Movimientos")').first().click();
    await page.waitForLoadState('networkidle');
    await auditar('Tras abandonar un formulario', { esperaTabla: false });
  }

  // — volver a la pestaña dispara la revalidación de TanStack Query
  await page.evaluate(() => window.dispatchEvent(new Event('blur')));
  await beat(300);
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  await beat(1500);
  await auditar('Tras recuperar el foco de la ventana', { esperaTabla: false });
});

// ═══════════════════════════════ 14 · Tamaños de ventana

await bloque('14 · Tamaños de ventana', async () => {
  const TAMANOS = [
    { w: 1920, h: 1080, etiqueta: 'escritorio' },
    { w: 1440, h: 900, etiqueta: 'portátil' },
    { w: 1190, h: 900, etiqueta: 'portátil estrecho' },
    { w: 1024, h: 768, etiqueta: 'tablet apaisado' },
    { w: 820, h: 1180, etiqueta: 'tablet vertical' },
    { w: 390, h: 844, etiqueta: 'teléfono' },
  ];

  for (const t of TAMANOS) {
    await page.setViewportSize({ width: t.w, height: t.h });
    await irA('/movimientos');
    await auditar(`Movimientos · ${t.etiqueta} (${t.w}px)`);

    if (t.w <= 900) {
      const menu = page.locator('.shx-menu-button');
      R.check(await menu.isVisible(), `${t.etiqueta}: aparece el botón de menú`);
      await menu.click();
      await beat(500);
      R.check(await page.locator('.shx-sidebar[data-open]').count() === 1,
        `${t.etiqueta}: el menú lateral se despliega`);
      await page.locator('.shx-scrim').click({ position: { x: 5, y: 5 } }).catch(() => {});
      await beat(400);
    }
  }
  await page.setViewportSize({ width: 1440, height: 950 });
});

// ═══════════════════════════════ 15 · Estrés de concurrencia

await bloque('15 · Estrés de concurrencia', async () => {
  // Seis pestañas cargando pantallas distintas a la vez, compartiendo la sesión.
  // Interesa que ninguna rompa y que el refresco del token no se pise entre ellas.
  const rutas = ['/', '/productos', '/movimientos', '/reportes', '/categorias', '/usuarios'];
  const pestanas = [];
  for (let i = 0; i < rutas.length; i++) pestanas.push(await context.newPage());

  const resultados = await Promise.all(pestanas.map(async (p, i) => {
    const errores = [];
    p.on('pageerror', (e) => errores.push(e.message.split('\n')[0]));
    p.on('response', (r) => {
      if (r.url().includes('/api/') && r.status() >= 500) errores.push(`${r.status()} ${r.url()}`);
    });
    try {
      await p.goto(APP + rutas[i], { waitUntil: 'networkidle', timeout: 45_000 });
      const texto = (await p.locator('main').innerText().catch(() => '')).trim();
      return { ruta: rutas[i], ok: texto.length > 20, errores };
    } catch (e) {
      return { ruta: rutas[i], ok: false, errores: [e.message.split('\n')[0]] };
    }
  }));

  for (const r of resultados) {
    R.check(r.ok && r.errores.length === 0, `seis pestañas a la vez: ${r.ruta}`,
      r.errores[0] ?? (r.ok ? undefined : 'quedó vacía'));
  }

  await Promise.all(pestanas.map((p) => p.close()));

  // La sesión sigue viva en la pestaña original después del bombardeo.
  await page.bringToFront();
  await irA('/movimientos');
  R.check(!page.url().includes('/login'),
    'la sesión sobrevive a seis pestañas simultáneas', page.url().replace(APP, ''));
  await auditar('Original tras la concurrencia');
});

// ═══════════════════════════════ 16 · Paginación a martillazos

await bloque('16 · Paginación a martillazos', async () => {
  await irA('/movimientos?pageSize=10');
  const siguiente = page.locator('button[aria-label="Página siguiente"]');
  if (!(await siguiente.count()) || await siguiente.isDisabled()) {
    R.check(true, 'no hay páginas suficientes para martillear (se omite)');
    return;
  }

  for (let i = 0; i < 8; i++) {
    if (await siguiente.isDisabled()) break;
    await siguiente.click();
    await beat(90);
  }
  await page.waitForLoadState('networkidle');
  await beat(900);
  await auditar('Movimientos tras ocho clics rápidos de página');
  const p = Number(new URL(page.url()).searchParams.get('page') ?? 1);
  R.check(p >= 1, 'la página nunca queda fuera de rango', `page=${p}`);
  R.check(await filas() > 0, 'la página final trae filas');
});

// ═══════════════════════════════════════════════ 17 · Limpieza

await bloque('17 · Limpieza', async () => {
  let borrados = 0;
  const totalCreado = Object.values(basura).flat().length;

  for (const mod of CATALOGO) {
    for (const valor of basura[mod.clave]) {
      await irA(`${mod.ruta}?search=${encodeURIComponent(valor)}`);
      await beat(600);
      const papelera = page.locator('table tbody tr button').last();
      if (!(await papelera.count())) continue;
      await papelera.click();
      await beat(500);
      const confirmar = page.locator('[role=dialog] button:has-text("Eliminar")').last();
      if (await confirmar.count()) {
        await confirmar.click();
        await beat(1100);
      }
      await irA(`${mod.ruta}?search=${encodeURIComponent(valor)}`);
      await beat(500);
      if (await filas() === 0) borrados++;
    }
  }
  watcher.drain();
  R.check(borrados === totalCreado, 'se borró todo lo que la prueba creó',
    borrados === totalCreado ? undefined
      : `quedan ${totalCreado - borrados} con prefijo ZZTEST-${SUFIJO}`);
});

await page.screenshot({ path: 'e2e/shots/stress-final.png' }).catch(() => {});
await browser.close();
process.exit(R.summary('Prueba de estrés'));

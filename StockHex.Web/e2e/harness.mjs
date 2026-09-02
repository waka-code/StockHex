/**
 * Piezas compartidas por las suites de navegador.
 *
 * Regla 2: `smoke`, `roles`, `refresh`, `rbac` y `filters` traen cada una su
 * propia copia de `check` y de la rutina de login. Son cinco implementaciones de
 * lo mismo, y la de `filters` ya arrastraba un bug que las otras no (un
 * `.catch()` que convertía un fallo en un ✓ silencioso). En vez de añadir una
 * sexta para la suite de estrés, la parte común vive aquí. Migrar las cinco
 * existentes queda pendiente: no se tocan en el mismo cambio que las estrena.
 */
import { chromium } from 'playwright';

export const APP = process.env.APP_URL ?? 'http://localhost:8080';
export const SHOTS = process.env.SHOTS ?? 'e2e/shots';

export const ADMIN = {
  email: process.env.ADMIN_EMAIL ?? 'admin@stockhex.local',
  password: process.env.ADMIN_PASSWORD ?? 'Admin123456',
};

/** Espera que un humano no percibe pero que deja respirar a React y a la red. */
export const beat = (ms = 250) => new Promise((r) => setTimeout(r, ms));

// ────────────────────────────────────────────────── informe

export function createReporter() {
  const failures = [];
  let passed = 0;
  let current = '';

  return {
    section(name) {
      current = name;
      console.log(`\n  ${name}`);
    },
    check(ok, label, detail) {
      if (ok) passed++;
      else failures.push(`${current} › ${label}${detail ? ` — ${detail}` : ''}`);
      console.log(`    ${ok ? '✓' : '✗'} ${label}${detail ? ` — ${detail}` : ''}`);
      return ok;
    },
    /** Un fallo que no viene de una comprobación sino de una excepción. */
    blew(label, error) {
      failures.push(`${current} › ${label} — ${error.message.split('\n')[0]}`);
      console.log(`    ✗ ${label} — ${error.message.split('\n')[0]}`);
    },
    get failures() { return failures; },
    get passed() { return passed; },
    summary(title) {
      const total = passed + failures.length;
      console.log(`\n  ${'─'.repeat(58)}`);
      if (failures.length === 0) {
        console.log(`  ✓ ${title}: ${total} comprobaciones en verde\n`);
        return 0;
      }
      console.log(`  ✗ ${title}: ${failures.length} de ${total} fallaron\n`);
      failures.forEach((f) => console.log(`      · ${f}`));
      console.log('');
      return 1;
    },
  };
}

// ──────────────────────────────────────── vigilancia de la página

/**
 * Recoge lo que el navegador denuncia por su cuenta: errores de consola,
 * excepciones no capturadas y respuestas de error de la API. Se acumula en vez
 * de fallar en el acto para poder atribuirlo a la pantalla que lo provocó.
 */
export function watchPage(page) {
  const log = { console: [], js: [], http: [], requestFailed: [] };
  let expecting = null;

  page.on('console', (m) => {
    if (m.type() !== 'error') return;
    const text = m.text();
    if (expecting?.test(text)) return;
    log.console.push(text.slice(0, 200));
  });
  page.on('pageerror', (e) => log.js.push(e.message.split('\n')[0].slice(0, 200)));
  page.on('requestfailed', (r) => {
    // El 404 de /config.js sólo ocurre con el servidor de Vite y está documentado.
    if (r.url().endsWith('/config.js')) return;
    log.requestFailed.push(`${r.method()} ${r.url()} — ${r.failure()?.errorText}`);
  });
  page.on('response', (r) => {
    if (!r.url().includes('/api/') || r.status() < 400) return;
    const line = `${r.status()} ${r.request().method()} ${r.url().replace(APP, '')}`;
    if (expecting?.test(line)) return;
    log.http.push(line);
  });

  return {
    log,
    /** Marca como esperados los errores que un paso provoca a propósito. */
    expect(pattern) { expecting = pattern; },
    stopExpecting() { expecting = null; },
    /** Vacía y devuelve lo acumulado, para atribuirlo a un tramo concreto. */
    drain() {
      const copy = {
        console: [...log.console], js: [...log.js],
        http: [...log.http], requestFailed: [...log.requestFailed],
      };
      log.console.length = 0; log.js.length = 0;
      log.http.length = 0; log.requestFailed.length = 0;
      return copy;
    },
  };
}

// ────────────────────────────────────────────────── sesión

export async function launch() {
  return chromium.launch();
}

export async function login(browser, {
  email = ADMIN.email, password = ADMIN.password,
  viewport = { width: 1440, height: 950 },
} = {}) {
  // Contexto explícito, no `browser.newPage()`: sólo así se pueden abrir más
  // pestañas que compartan la sesión (el localStorage vive en el contexto).
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  const watcher = watchPage(page);

  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.fill('input[name=email]', email);
  await page.fill('input[name=password]', password);
  await page.click('button[type=submit]');
  await page.waitForSelector('text=Últimos movimientos', { timeout: 25_000 });

  return { page, watcher, context };
}

// ──────────────────────────────────── invariantes de maquetación

/**
 * Lo que tiene que cumplirse en cualquier pantalla, mire quien la mire y al
 * ancho que sea. Se ejecuta dentro del navegador.
 */
export function inspectLayout(page) {
  return page.evaluate(() => {
    const doc = document.documentElement;

    // La página nunca desplaza en horizontal: lo hace el contenedor ancho
    // (una tabla), nunca el documento.
    const bodyOverflow = Math.max(0, doc.scrollWidth - doc.clientWidth);

    // Elementos que se salen por la derecha del viewport.
    const desbordados = [...document.querySelectorAll('main *')]
      .filter((el) => {
        const r = el.getBoundingClientRect();
        return r.width > 0 && r.right > doc.clientWidth + 2
          // Un hijo de un contenedor que sí desplaza es legítimo.
          && !el.closest('[data-scrolls], .shx-scrolls')
          && !(() => {
            let p = el.parentElement;
            while (p) {
              const o = getComputedStyle(p).overflowX;
              if (o === 'auto' || o === 'scroll' || o === 'hidden') return true;
              p = p.parentElement;
            }
            return false;
          })();
      })
      .slice(0, 3)
      .map((el) => `${el.tagName.toLowerCase()}.${(el.className || '').toString().slice(0, 20)}`);

    // Filas apiladas: el síntoma de una tabla comprimida en vez de desplazada.
    const filas = [...document.querySelectorAll('table tbody tr')]
      .map((f) => Math.round(f.getBoundingClientRect().height));
    const filaMasAlta = filas.length ? Math.max(...filas) : 0;

    return {
      bodyOverflow,
      desbordados,
      filaMasAlta,
      filas: filas.length,
      // Una pantalla en blanco es un fallo que no se ve en la consola.
      textoVisible: (document.querySelector('main')?.innerText ?? '').trim().length,
    };
  });
}

/** Alto máximo tolerable de una fila antes de considerarla apilada. */
export const FILA_MAX = 70;

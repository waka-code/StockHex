// Corre la suite completa en orden.
//
// Existe por una razón concreta: el limitador de `/api/auth` acepta 10 intentos
// por minuto y por IP, y una tanda completa hace más de 10 logins. Encadenar los
// archivos con `&&` dejaba a las últimas suites sin poder entrar, con un timeout
// que parecía un fallo del producto y era el limitador haciendo su trabajo.
//
// Entre suite y suite se espera a que la ventana del limitador se renueve.
import { spawn } from 'node:child_process';
import { setTimeout as sleep } from 'node:timers/promises';

const SUITES = ['smoke', 'roles', 'refresh', 'rbac', 'filters', 'stress'];
const WINDOW_MS = 62_000;

// Un solo destino para las cinco. Antes cada archivo traía su propio defecto y
// tres apuntaban al servidor de Vite mientras las otras dos iban al contenedor:
// una tanda «en verde» no verificaba un despliegue, sino dos a medias.
const APP = process.env.APP_URL ?? 'http://localhost:8080';
console.log(`\n  destino: ${APP}\n`);

const run = (name) => new Promise((resolve) => {
  const child = spawn(process.execPath, [`e2e/${name}.mjs`], {
    stdio: 'inherit',
    env: { ...process.env, APP_URL: APP },
  });
  child.on('exit', (code) => resolve(code ?? 1));
});

let failed = [];
for (const [index, name] of SUITES.entries()) {
  if (index > 0) {
    console.log(`\n  … esperando ${WINDOW_MS / 1000}s para que se renueve el límite de /api/auth\n`);
    await sleep(WINDOW_MS);
  }
  const code = await run(name);
  if (code !== 0) failed.push(name);
}

console.log(failed.length === 0
  ? `\n  ✓ ${SUITES.length} suites en verde\n`
  : `\n  ✗ suites con fallos: ${failed.join(', ')}\n`);
process.exit(failed.length === 0 ? 0 : 1);

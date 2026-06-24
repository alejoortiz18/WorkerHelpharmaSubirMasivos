/**
 * E2E visible: login → NoProcesados → Reprocesar → ciclo completo.
 * Ejecutar: node reproceso-visible.mjs
 */
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const BASE_URL = process.env.PORTAL_URL ?? 'http://localhost:5254';
const USUARIO = process.env.PORTAL_USUARIO ?? 'dgutierrez';
const FECHA = process.env.PORTAL_FECHA ?? '2026-06-24';
const ARCHIVO = process.env.PORTAL_ARCHIVO ?? 'CRC_900277244_FE256521.pdf';
const LOG_DIR = process.env.PORTAL_LOG_DIR ?? 'C:\\inetpub\\GestionDocumentosEscaneados\\logs';
const PAUSA_MS = Number(process.env.PAUSA_MS ?? '2000');

function log(paso, detalle = '') {
  const ts = new Date().toISOString().slice(11, 19);
  console.log(`[${ts}] ${paso}${detalle ? ' | ' + detalle : ''}`);
}

async function pausa(page, motivo) {
  log('PAUSA', motivo);
  await page.waitForTimeout(PAUSA_MS);
}

function ultimoLogStdout() {
  try {
    const files = fs.readdirSync(LOG_DIR)
      .filter(f => f.startsWith('stdout_') && f.endsWith('.log'))
      .map(f => ({ f, m: fs.statSync(path.join(LOG_DIR, f)).mtimeMs }))
      .sort((a, b) => b.m - a.m);
    return files[0]?.f ? path.join(LOG_DIR, files[0].f) : null;
  } catch {
    return null;
  }
}

function leerLogDesde(ruta, desdeBytes) {
  if (!ruta || !fs.existsSync(ruta)) return '';
  const buf = fs.readFileSync(ruta);
  return buf.slice(desdeBytes).toString('utf8');
}

async function main() {
  const logFile = ultimoLogStdout();
  const logOffset = logFile && fs.existsSync(logFile) ? fs.statSync(logFile).size : 0;

  log('INICIO', `URL=${BASE_URL} Usuario=${USUARIO} Fecha=${FECHA} Archivo=${ARCHIVO}`);
  if (logFile) log('LOG IIS', logFile);

  const browser = await chromium.launch({
    headless: false,
    slowMo: 400,
    args: ['--start-maximized']
  });

  const context = await browser.newContext({ viewport: null });
  const page = await context.newPage();

  page.on('console', msg => {
    if (msg.type() === 'error') log('BROWSER ERR', msg.text());
  });

  page.on('response', async resp => {
    const url = resp.url();
    if (url.includes('ReprocesarDocumento') || url.includes('api-soportes') || url.includes('soporte/fisico')) {
      let body = '';
      try { body = (await resp.text()).slice(0, 200); } catch { /* ignore */ }
      log('HTTP', `${resp.status()} ${resp.request().method()} ${url.split('?')[0]} ${body}`);
    }
  });

  try {
    // 1. Login
    log('PASO 1', 'Abrir login');
    await page.goto(`${BASE_URL}/Account/Login`, { waitUntil: 'networkidle' });
    await pausa(page, 'Pantalla de login visible');

    await page.fill('input[name="Usuario"]', USUARIO);
    await pausa(page, 'Usuario ingresado');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/Home\/Index/, { timeout: 15000 });
    log('PASO 2', 'Login OK → calendario');
    await pausa(page, 'Calendario visible');

    // 2. Ir directo a NoProcesados (más rápido que calendario)
    log('PASO 3', 'Navegar a documentos no procesados');
    await page.goto(`${BASE_URL}/Documentos/NoProcesados?fecha=${FECHA}`, { waitUntil: 'networkidle' });
    await pausa(page, 'Lista de pendientes visible');

    const fila = page.locator(`tr.docs-row[data-archivo="${ARCHIVO}"]`);
    const countFilas = await page.locator('tr.docs-row').count();
    log('PASO 4', `${countFilas} fila(s) pendiente(s)`);

    if (await fila.count() === 0) {
      throw new Error(`No se encontró el archivo ${ARCHIVO} en la lista. Verifique BD Procesado=0 y PDF en noprocesados.`);
    }

    await fila.click();
    await pausa(page, 'PDF seleccionado en vista previa');

    const btnReprocesar = page.locator('#btnReprocesar');
    await btnReprocesar.waitFor({ state: 'visible', timeout: 10000 });
    const disabled = await btnReprocesar.isDisabled();
    if (disabled) {
      throw new Error('Botón Reprocesar está deshabilitado (sin filas pendientes).');
    }

    log('PASO 5', 'Clic en Reprocesar — inicia IronBarCode → OpenAI → APIs');
    await pausa(page, 'Antes de pulsar Reprocesar');

    let resultadoObjetivo = null;
    page.on('response', async resp => {
      if (!resp.url().includes('ReprocesarDocumento') || resp.request().method() !== 'POST') return;
      const body = resp.request().postData() ?? '';
      if (!body.includes(encodeURIComponent(ARCHIVO)) && !body.includes(ARCHIVO)) return;
      try {
        resultadoObjetivo = await resp.json();
        log('PASO 7', `Respuesta ${ARCHIVO}: ${JSON.stringify(resultadoObjetivo)}`);
      } catch { /* ignore */ }
    });

    await btnReprocesar.click();

    log('PASO 6', 'Barra de progreso visible — procesando (OpenAI puede tardar ~1-2 min)...');

    const inicio = Date.now();
    while (!resultadoObjetivo && Date.now() - inicio < 300000) {
      await page.waitForTimeout(1000);
    }

    if (!resultadoObjetivo) {
      throw new Error(`Sin respuesta para ${ARCHIVO} en 5 minutos`);
    }

    const json = resultadoObjetivo;

    // Esperar reload automático (500ms en la vista)
    await page.waitForLoadState('networkidle', { timeout: 300000 }).catch(() => {});
    await page.waitForTimeout(3000);
    await pausa(page, 'Página recargada — revisar resultado');

    const exitoAlert = page.locator('.alert-success');
    const errorAlert = page.locator('.alert-danger, .alert-warning');
    if (await exitoAlert.count() > 0) {
      log('RESULTADO', await exitoAlert.first().textContent());
    } else if (await errorAlert.count() > 0) {
      log('RESULTADO', await errorAlert.first().textContent());
    }

    const filasRestantes = await page.locator(`tr.docs-row[data-archivo="${ARCHIVO}"]`).count();
    log('PASO 8', `Filas restantes con ${ARCHIVO}: ${filasRestantes} (0 = procesado OK)`);

    if (json.exito === true || json.estado === 'Exito') {
      log('CICLO COMPLETO', 'ÉXITO — APIs y movimiento de archivo OK');
    } else {
      log('CICLO COMPLETO', `Estado=${json.estado} — revisar logs IIS`);
    }

    // Mostrar logs del servidor capturados durante la prueba
    const nuevosLogs = leerLogDesde(logFile, logOffset);
    if (nuevosLogs) {
      console.log('\n--- Logs IIS (fragmento relevante) ---');
      const lineas = nuevosLogs.split('\n').filter(l =>
        /Reproceso|OpenAi|Barcode|Soporte|ApiSoporte|LeyendoPdf|HTTP/i.test(l));
      console.log(lineas.slice(-40).join('\n') || '(sin líneas de reproceso en log)');
    }

    await pausa(page, 'Fin de prueba — ventana abierta 10s más');
    await page.waitForTimeout(10000);
  } finally {
    await browser.close();
    log('FIN', 'Navegador cerrado');
  }
}

main().catch(err => {
  console.error('ERROR E2E:', err.message);
  process.exit(1);
});

"""
Prueba del flujo completo del boton Reprocesar contra IIS (localhost:5254)

Valida:
1. Login y navegacion a NoProcesados
2. Click en Reprocesar - el proceso corre realmente
3. Los logs de IIS muestran: LeyendoPdf, OpenAi (si IronBarCode falla),
   ApiSoporte, SoporteFisico
4. El resultado final en pantalla (exito o error con detalle)

Usuario: dgutierrez | Fecha: 2026-06-18
"""
import os
import time
import glob
from playwright.sync_api import sync_playwright

URL      = "http://localhost:5254"
USUARIO  = "dgutierrez"
FECHA    = "2026-06-18"
LOG_DIR  = r"C:\inetpub\GestionDocumentosEscaneados\logs"
SEP      = "=" * 65

# ──────────────────────────────────────────────────────────────
def log_mas_reciente():
    archivos = glob.glob(os.path.join(LOG_DIR, "stdout_*.log"))
    if not archivos:
        return None
    return max(archivos, key=os.path.getmtime)

def leer_log_desde(ruta, pos):
    try:
        with open(ruta, "r", encoding="utf-8", errors="replace") as f:
            f.seek(pos)
            return f.read(), f.tell()
    except Exception:
        return "", pos

def buscar_en_texto(texto, terminos):
    return {t: (t in texto) for t in terminos}

# ──────────────────────────────────────────────────────────────
def run():
    log_path = log_mas_reciente()
    pos_inicio = 0
    if log_path:
        with open(log_path, "r", encoding="utf-8", errors="replace") as f:
            f.seek(0, 2)
            pos_inicio = f.tell()
        print(f"  Log activo: {os.path.basename(log_path)}")
    else:
        print("  ⚠ No se encontro log de IIS activo")

    with sync_playwright() as p:
        browser = p.chromium.launch(
            channel="msedge",
            headless=False,
            slow_mo=500,
            args=["--start-maximized"]
        )
        ctx  = browser.new_context(no_viewport=True)
        page = ctx.new_page()

        # Capturar errores JS del navegador
        errores_js = []
        page.on("console", lambda msg: errores_js.append(msg.text) if msg.type == "error" else None)

        print(f"\n{SEP}")
        print("  PRUEBA FLUJO COMPLETO - REPROCESAR")
        print(f"  URL: {URL} | Usuario: {USUARIO} | Fecha: {FECHA}")
        print(f"{SEP}\n")

        # ── LOGIN ──────────────────────────────────────────────
        print("► Paso 1: Login")
        page.goto(URL)
        page.wait_for_load_state("networkidle")
        page.fill('input[name="Usuario"]', USUARIO)
        page.click('button[type="submit"]')
        page.wait_for_load_state("networkidle")
        url_post_login = page.url
        login_ok = "/Home" in url_post_login or "/Documentos" in url_post_login or url_post_login == URL + "/"
        print(f"  URL despues del login: {url_post_login}")
        print(f"  Login: {'✅ OK' if login_ok else '❌ FALLO'}")
        if not login_ok:
            browser.close()
            return

        # ── NAVEGAR A NO PROCESADOS ────────────────────────────
        print("\n► Paso 2: Navegar a NoProcesados")
        page.goto(f"{URL}/Documentos/NoProcesados?fecha={FECHA}")
        page.wait_for_load_state("networkidle")
        time.sleep(1)

        filas = page.query_selector_all("table tbody tr")
        total = len(filas)
        reprocesadas = len(page.query_selector_all("tr.is-reprocessed"))
        sin_intento  = total - reprocesadas
        print(f"  Documentos en lista         : {total}")
        print(f"  Con badge Reprocesado       : {reprocesadas}")
        print(f"  Sin intento previo          : {sin_intento}")

        if total == 0:
            print("  ⚠ No hay documentos en la lista para esta fecha. Prueba cancelada.")
            browser.close()
            return

        btn = page.query_selector("#btnReprocesar")
        btn_ok = btn is not None and not btn.is_disabled()
        print(f"  Boton Reprocesar habilitado : {'✅ SI' if btn_ok else '❌ NO'}")

        if not btn_ok:
            print("  ⚠ Boton deshabilitado. No hay documentos que reprocesar.")
            browser.close()
            return

        # Capturar respuestas de red relacionadas con el reproceso
        respuestas_reproceso = []
        def capturar(response):
            if "Reprocesar" in response.url or "reprocesar" in response.url.lower():
                respuestas_reproceso.append({
                    "url": response.url,
                    "status": response.status
                })
        page.on("response", capturar)

        # ── CLICK REPROCESAR ───────────────────────────────────
        print("\n► Paso 3: Click en Reprocesar (flujo real: IronBarCode → OpenAI → APIs)")
        print("  Esperando que el reproceso complete (puede tardar 30-120s)...")
        tiempo_inicio = time.time()

        page.click("#btnReprocesar")

        # Esperar hasta que la pagina recargue con los resultados
        try:
            page.wait_for_url("**/NoProcesados**", timeout=180000, wait_until="networkidle")
        except Exception:
            pass

        tiempo_total = round(time.time() - tiempo_inicio, 1)
        print(f"  Tiempo de ejecucion         : {tiempo_total}s")

        # ── RESULTADO EN PANTALLA ──────────────────────────────
        print("\n► Paso 4: Resultado en pantalla")
        filas_despues      = len(page.query_selector_all("table tbody tr"))
        reprocesadas_desp  = len(page.query_selector_all("tr.is-reprocessed"))

        print(f"  Documentos despues          : {filas_despues}")
        print(f"  Con badge Reprocesado       : {reprocesadas_desp}")

        # Buscar mensajes de resultado en la pagina
        toast    = page.query_selector(".toast-body, .alert, [class*='alert'], #toastMessage")
        resumen  = page.query_selector("[id*='resumen'], [id*='resultado'], .reproceso-resumen")
        if toast:
            print(f"  Mensaje en pantalla         : {toast.inner_text()[:200]}")
        if resumen:
            print(f"  Resumen reproceso           : {resumen.inner_text()[:200]}")

        # Errores JS
        if errores_js:
            print(f"  ⚠ Errores JS del navegador  : {len(errores_js)}")
            for e in errores_js[:3]:
                print(f"    - {e[:120]}")

        # ── ANALISIS DE LOGS IIS ───────────────────────────────
        print("\n► Paso 5: Analisis de logs IIS")
        log_nuevo = log_mas_reciente()
        texto_log = ""
        if log_nuevo:
            texto_log, _ = leer_log_desde(log_nuevo, pos_inicio)
            if not texto_log and log_nuevo != log_path:
                # El log roto durante la prueba, leer completo
                with open(log_nuevo, "r", encoding="utf-8", errors="replace") as f:
                    texto_log = f.read()

        TERMINOS = [
            "ReprocesoInicio",
            "LeyendoPdf",
            "ReprocesoBarcodeDetectado",
            "ReprocesoBarcodeNoDetectado",
            "OpenAiResultado",
            "OpenAiFallo",
            "ReprocesoEnviarSoporte",
            "ApiSoporteError",
            "SoporteFisicoOK",
            "SoporteFisicoError",
            "ReprocesoExitoso",
            "ReprocesoSoporteFallo",
        ]

        encontrados = buscar_en_texto(texto_log, TERMINOS)

        if texto_log:
            print(f"  Lineas de log capturadas    : {texto_log.count(chr(10))}")
            for termino, hallado in encontrados.items():
                estado = "✅" if hallado else "·"
                print(f"    {estado} {termino}")

            # Extraer lineas relevantes del log
            lineas_relevantes = [l for l in texto_log.splitlines()
                                 if any(t in l for t in TERMINOS)]
            if lineas_relevantes:
                print(f"\n  Detalle del log ({len(lineas_relevantes)} lineas relevantes):")
                for l in lineas_relevantes[-20:]:  # ultimas 20
                    print(f"    {l.strip()[:130]}")
        else:
            print("  ⚠ No se pudo leer el log de IIS (puede que stdoutLogEnabled=false)")

        # ── RESUMEN FINAL ──────────────────────────────────────
        print(f"\n{SEP}")
        print("  RESUMEN FINAL")
        print(f"{SEP}")

        exito_reproceso   = encontrados.get("ReprocesoExitoso", False)
        uso_barcode       = encontrados.get("ReprocesoBarcodeDetectado", False)
        uso_openai        = encontrados.get("OpenAiResultado", False)
        envio_soporte     = encontrados.get("ReprocesoEnviarSoporte", False)
        soporte_fisico_ok = encontrados.get("SoporteFisicoOK", False)
        soporte_fisico_err= encontrados.get("SoporteFisicoError", False)

        print(f"  Barcode detectado (IronBarCode) : {'✅' if uso_barcode else '· No detectado (fallback a OpenAI)'}")
        print(f"  OpenAI utilizado               : {'✅' if uso_openai else '· No se invoco'}")
        print(f"  Envio a DatosSoportes (API 1)  : {'✅' if envio_soporte else '❌ No se ejecuto'}")
        print(f"  Envio a soporte/fisico (API 2) : {'✅ OK' if soporte_fisico_ok else ('❌ ERROR' if soporte_fisico_err else '· No se ejecuto')}")
        print(f"  Reproceso exitoso              : {'✅ PASS' if exito_reproceso else '❌ FAIL o pendiente de verificar logs'}")
        print(f"  Tiempo total                   : {tiempo_total}s")
        print(f"{SEP}\n")

        time.sleep(5)
        browser.close()

if __name__ == "__main__":
    run()

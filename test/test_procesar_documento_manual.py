import os
import time
from playwright.sync_api import sync_playwright

URL = os.getenv("TEST_URL", "http://localhost:5255")
USUARIO = os.getenv("TEST_USUARIO", "dgutierrez")
FECHA = os.getenv("TEST_FECHA", "2026-06-18")
ARCHIVO = os.getenv("TEST_ARCHIVO", "CRC_900277244_FE32595.pdf")
CODIGO = os.getenv("TEST_CODIGO", "FMI58369")
SEP = "=" * 70


def run():
    with sync_playwright() as p:
        browser = p.chromium.launch(
            channel="msedge",
            headless=False,
            slow_mo=400,
            args=["--start-maximized"],
        )
        context = browser.new_context(no_viewport=True)
        page = context.new_page()

        errores_js = []
        page.on("console", lambda msg: errores_js.append(msg.text) if msg.type == "error" else None)

        print(SEP)
        print("PRUEBA MANUAL - PROCESAR DOCUMENTOS")
        print(f"URL={URL} | Usuario={USUARIO} | Fecha={FECHA}")
        print(f"Archivo objetivo={ARCHIVO} | Codigo={CODIGO}")
        print(SEP)

        page.goto(URL)
        page.wait_for_load_state("networkidle")

        page.fill('input[name="Usuario"]', USUARIO)
        page.click('button[type="submit"]')
        page.wait_for_load_state("networkidle")

        print(f"Login URL: {page.url}")
        if "/Home" not in page.url and "/Documentos" not in page.url and not page.url.endswith("/"):
            print("FALLO: login no exitoso")
            browser.close()
            return

        page.goto(f"{URL}/Documentos/NoProcesados?fecha={FECHA}")
        page.wait_for_load_state("networkidle")
        time.sleep(1)

        row = page.locator(f'tr[data-archivo="{ARCHIVO}"]')
        if row.count() == 0:
            print("FALLO: archivo objetivo no aparece en la lista")
            print("Archivos visibles:")
            for item in page.locator("tr[data-archivo]").evaluate_all("rows => rows.map(r => r.getAttribute('data-archivo'))"):
                print(f"- {item}")
            browser.close()
            return

        row.click()
        input_codigo = row.locator(".barcode-input")
        input_codigo.fill(CODIGO)

        btn = page.locator("#btnProcesar")
        print(f"Boton Procesar habilitado: {btn.is_enabled()}")
        btn.click()

        page.wait_for_load_state("networkidle")
        time.sleep(2)

        mensajes = page.locator(".alert").all_inner_texts()
        print("Mensajes en pantalla:")
        for mensaje in mensajes:
            print(f"- {mensaje}")

        sigue_visible = page.locator(f'tr[data-archivo="{ARCHIVO}"]').count() > 0
        print(f"Archivo sigue visible tras procesar: {sigue_visible}")

        if errores_js:
            print("Errores JS:")
            for error in errores_js[:5]:
                print(f"- {error}")

        time.sleep(5)
        browser.close()


if __name__ == "__main__":
    run()

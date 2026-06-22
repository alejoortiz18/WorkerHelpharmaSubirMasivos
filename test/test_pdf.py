from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser = p.chromium.launch(channel="msedge", headless=True)
    ctx = browser.new_context()
    page = ctx.new_page()

    page.goto("http://localhost:5254")
    page.fill("input[name=Usuario]", "dgutierrez")
    page.click("button[type=submit]")
    page.wait_for_load_state("networkidle")
    print("Despues login URL:", page.url)

    page.goto("http://localhost:5254/Documentos/NoProcesados?fecha=2026-06-18")
    page.wait_for_load_state("networkidle")

    pdf_url = page.evaluate("() => { const r = document.querySelector('tr.docs-row'); return r ? r.dataset.pdfUrl : 'no encontrado'; }")
    print("PDF URL en data-pdf-url:", pdf_url)

    if pdf_url and pdf_url != "no encontrado":
        full_url = "http://localhost:5254" + pdf_url if pdf_url.startswith("/") else pdf_url
        print("URL completa:", full_url)
        
        # Captura los errores de red al cargar el iframe
        errors = []
        page.on("requestfailed", lambda r: errors.append(f"FAIL: {r.url} -> {r.failure}"))
        
        # Verifica los headers de respuesta del PDF
        responses = {}
        page.on("response", lambda r: responses.update({r.url: dict(r.headers)}) if "Pdf" in r.url else None)
        
        # Cargar la pagina con el iframe
        page.goto("http://localhost:5254/Documentos/NoProcesados?fecha=2026-06-18")
        page.wait_for_load_state("networkidle")
        page.wait_for_timeout(2000)
        
        print("Errores de red:", errors if errors else "Ninguno")
        for url, hdrs in responses.items():
            print(f"Response {url}")
            for k, v in hdrs.items():
                print(f"  {k}: {v}")
        
        if not responses:
            print("El iframe no hizo ninguna peticion al endpoint PDF (src no fue asignado)")
    
    browser.close()

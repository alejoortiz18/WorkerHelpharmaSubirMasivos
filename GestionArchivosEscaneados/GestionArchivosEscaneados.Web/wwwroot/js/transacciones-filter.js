(function () {
    const table = document.getElementById('tx-documentos-table');
    if (!table) return;

    const filters = Array.from(table.querySelectorAll('.tx-col-filter'));
    const allRows = Array.from(table.querySelectorAll('tbody tr'));
    const pageSizeSelect = document.getElementById('tx-page-size');
    const pagePrev = document.getElementById('tx-page-prev');
    const pageNext = document.getElementById('tx-page-next');
    const pageLabel = document.getElementById('tx-page-label');
    const paginationInfo = document.getElementById('tx-pagination-info');
    const resumenFiltrado = document.getElementById('tx-resumen-filtrado');

    let paginaActual = 1;
    let filasFiltradas = allRows;

    function normalizar(texto) {
        return (texto || '').trim().toLowerCase();
    }

    function obtenerFilasFiltradas() {
        const valores = filters.map(input => normalizar(input.value));
        const hayFiltro = valores.some(v => v.length > 0);

        const filtradas = allRows.filter(row => {
            const celdas = row.querySelectorAll('td');
            return valores.every((filtro, indice) => {
                if (!filtro) return true;
                const celda = celdas[indice];
                return celda && normalizar(celda.textContent).includes(filtro);
            });
        });

        if (resumenFiltrado) {
            if (hayFiltro && filtradas.length !== allRows.length) {
                resumenFiltrado.textContent = `${filtradas.length} archivos filtrados`;
            } else {
                resumenFiltrado.textContent = '';
            }
        }

        return filtradas;
    }

    function obtenerTamanoPagina() {
        return parseInt(pageSizeSelect?.value || '10', 10) || 10;
    }

    function aplicarPaginacion() {
        const tamanoPagina = obtenerTamanoPagina();
        const totalFilas = filasFiltradas.length;
        const totalPaginas = Math.max(1, Math.ceil(totalFilas / tamanoPagina));

        if (paginaActual > totalPaginas) {
            paginaActual = totalPaginas;
        }

        allRows.forEach(row => {
            row.style.display = 'none';
        });

        if (totalFilas === 0) {
            if (pageLabel) pageLabel.textContent = '';
            if (paginationInfo) paginationInfo.textContent = '0 registros';
            if (pagePrev) pagePrev.disabled = true;
            if (pageNext) pageNext.disabled = true;
            return;
        }

        const inicio = (paginaActual - 1) * tamanoPagina;
        const fin = Math.min(inicio + tamanoPagina, totalFilas);
        const pagina = filasFiltradas.slice(inicio, fin);

        pagina.forEach(row => {
            row.style.display = '';
        });

        if (pageLabel) {
            pageLabel.textContent = `Página ${paginaActual} de ${totalPaginas}`;
        }

        if (paginationInfo) {
            paginationInfo.textContent = `Mostrando ${inicio + 1}-${fin} de ${totalFilas}`;
        }

        if (pagePrev) pagePrev.disabled = paginaActual <= 1;
        if (pageNext) pageNext.disabled = paginaActual >= totalPaginas;
    }

    function actualizarVista() {
        filasFiltradas = obtenerFilasFiltradas();
        aplicarPaginacion();
    }

    filters.forEach(input => {
        input.addEventListener('input', () => {
            paginaActual = 1;
            actualizarVista();
        });
    });

    pageSizeSelect?.addEventListener('change', () => {
        paginaActual = 1;
        actualizarVista();
    });

    pagePrev?.addEventListener('click', () => {
        if (paginaActual > 1) {
            paginaActual--;
            aplicarPaginacion();
        }
    });

    pageNext?.addEventListener('click', () => {
        paginaActual++;
        aplicarPaginacion();
    });

    actualizarVista();
})();

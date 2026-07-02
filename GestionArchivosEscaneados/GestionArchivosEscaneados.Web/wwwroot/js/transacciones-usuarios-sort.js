(function () {
    const table = document.getElementById('tx-usuarios-table');
    if (!table) return;

    const tbody = table.querySelector('tbody');
    const sortButtons = Array.from(table.querySelectorAll('.tx-sort-btn'));
    if (!tbody || sortButtons.length === 0) return;

    function obtenerValor(fila, key) {
        switch (key) {
            case 'usuario':
                return (fila.dataset.usuario || '').trim().toLowerCase();
            case 'dias':
                return parseInt(fila.dataset.dias || '0', 10) || 0;
            case 'ultimo-dia':
                return fila.dataset.ultimoDia || '';
            default:
                return '';
        }
    }

    function comparar(a, b, key, dir) {
        const va = obtenerValor(a, key);
        const vb = obtenerValor(b, key);
        let resultado = 0;

        if (key === 'dias') {
            resultado = va - vb;
        } else {
            resultado = va < vb ? -1 : va > vb ? 1 : 0;
        }

        return dir === 'desc' ? -resultado : resultado;
    }

    function ordenar(key, dir) {
        const filas = Array.from(tbody.querySelectorAll('tr'));
        filas.sort((a, b) => comparar(a, b, key, dir));
        filas.forEach(fila => tbody.appendChild(fila));
    }

    function limpiarEstadoActivo(excepto) {
        sortButtons.forEach(btn => {
            if (btn === excepto) return;
            btn.removeAttribute('data-sort-dir');
            btn.setAttribute('aria-sort', 'none');
        });
    }

    sortButtons.forEach(btn => {
        btn.addEventListener('click', event => {
            event.stopPropagation();

            const key = btn.dataset.sortKey;
            if (!key) return;

            const dirActual = btn.getAttribute('data-sort-dir');
            const dirNueva = dirActual === 'asc' ? 'desc' : 'asc';

            limpiarEstadoActivo(btn);
            btn.setAttribute('data-sort-dir', dirNueva);
            btn.setAttribute('aria-sort', dirNueva === 'asc' ? 'ascending' : 'descending');

            ordenar(key, dirNueva);
        });
    });
})();

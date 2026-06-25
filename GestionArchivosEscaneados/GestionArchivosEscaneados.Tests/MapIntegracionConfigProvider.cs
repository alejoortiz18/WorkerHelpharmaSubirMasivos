using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Settings;

namespace GestionArchivosEscaneados.Tests;

internal sealed class MapIntegracionConfigProvider : IIntegracionConfigProvider
{
    private readonly Dictionary<string, ConfiguracionProducto> _productos;

    public MapIntegracionConfigProvider(RutasSettings rutas, RedSettings? red = null)
    {
        red ??= new RedSettings();
        _productos = new Dictionary<string, ConfiguracionProducto>(StringComparer.Ordinal)
        {
            [ProductoIntegracion.Unc] = new ConfiguracionProducto
            {
                Producto = ProductoIntegracion.Unc,
                Endpoint = rutas.RaizUnc,
                ClaveCredencial = red.Usuario,
                ValorAdicional = red.Clave
            }
        };
    }

    public Task<ConfiguracionProducto?> ObtenerProductoAsync(
        string producto,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_productos.TryGetValue(producto, out var valor) ? valor : null);

    public Task GuardarProductoAsync(
        ConfiguracionProducto configuracion,
        CancellationToken cancellationToken = default)
    {
        _productos[configuracion.Producto] = configuracion;
        return Task.CompletedTask;
    }

    public string ObtenerFallback(string clave) => string.Empty;
}

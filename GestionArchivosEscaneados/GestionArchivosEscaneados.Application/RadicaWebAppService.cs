using GestionArchivosEscaneados.Infrastructure.Api;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Models.Entities;

namespace GestionArchivosEscaneados.Application;

public class RadicaWebAppService
{
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;
    private readonly RadicaWebApiService _api;

    public RadicaWebAppService(
        ITrazabilidadConsultaSqlService trazabilidad,
        RadicaWebApiService api)
    {
        _trazabilidad = trazabilidad;
        _api = api;
    }

    public async Task<RadicaWebPanelDatos> ObtenerPanelAsync(
        RadicaWebFiltrosConsulta filtros,
        CancellationToken cancellationToken = default)
    {
        var pagina = Math.Max(1, filtros.Pagina);
        var tamano = Math.Clamp(filtros.TamanoPagina, 5, 100);
        var apiConfigurada = await _api.EstaConfiguradoAsync(cancellationToken);

        var (items, total) = await _trazabilidad.ListarRadicaWebNotificacionesAsync(
            filtros.Desde,
            filtros.Hasta,
            filtros.Usuario,
            filtros.Bodega,
            filtros.Success,
            pagina,
            tamano,
            cancellationToken);

        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamano));

        return new RadicaWebPanelDatos
        {
            Filtros = filtros,
            UsuariosDisponibles = await _trazabilidad.ListarUsuariosRadicaWebAsync(cancellationToken),
            Notificaciones = items,
            TotalNotificaciones = total,
            Pagina = pagina,
            TamanoPagina = tamano,
            TotalPaginas = totalPaginas,
            KpiResumen = await _trazabilidad.ObtenerRadicaWebKpiResumenAsync(
                filtros.Desde, filtros.Hasta, filtros.Usuario, filtros.Bodega, filtros.Success, cancellationToken),
            PorUsuario = await _trazabilidad.ListarRadicaWebKpiPorUsuarioAsync(
                filtros.Desde, filtros.Hasta, filtros.Usuario, filtros.Bodega, filtros.Success, cancellationToken),
            PorBodega = await _trazabilidad.ListarRadicaWebKpiPorBodegaAsync(
                filtros.Desde, filtros.Hasta, filtros.Usuario, filtros.Bodega, filtros.Success, cancellationToken),
            PorFechaFactura = await _trazabilidad.ListarRadicaWebKpiPorFechaFacturaAsync(
                filtros.Desde, filtros.Hasta, filtros.Usuario, filtros.Bodega, filtros.Success, cancellationToken),
            ApiConfigurada = apiConfigurada
        };
    }

    public async Task<RenotificarRadicaWebResultado> RenotificarAsync(
        long radicaWebApiId,
        CancellationToken cancellationToken = default)
    {
        if (!await _api.EstaConfiguradoAsync(cancellationToken))
        {
            return new RenotificarRadicaWebResultado
            {
                Exito = false,
                Mensaje = "Configure API RadicaWeb en dbo.Configuraciones (x-api-client y x-api-secret)."
            };
        }

        var notificacion = await _trazabilidad.ObtenerRadicaWebNotificacionAsync(radicaWebApiId, cancellationToken);
        if (notificacion is null)
        {
            return new RenotificarRadicaWebResultado
            {
                Exito = false,
                Mensaje = "La notificación no existe."
            };
        }

        if (!notificacion.PuedeRenotificar)
        {
            return new RenotificarRadicaWebResultado
            {
                Exito = false,
                Mensaje = "Esta notificación ya fue exitosa y no puede reenviarse."
            };
        }

        var resultado = await _api.EnviarBusquedaAsync(
            notificacion.FechaFactura,
            notificacion.Bodega,
            cancellationToken);

        await _trazabilidad.ActualizarRadicaWebNotificacionAsync(radicaWebApiId, resultado, cancellationToken);

        return new RenotificarRadicaWebResultado
        {
            Exito = resultado.Success == true,
            Mensaje = resultado.Message ?? (resultado.Success == true ? "Notificación reenviada." : "Error al renotificar."),
            StatusCode = resultado.HttpStatusCode
        };
    }
}

public class RadicaWebFiltrosConsulta
{
    public DateOnly? Desde { get; init; }

    public DateOnly? Hasta { get; init; }

    public string? Usuario { get; init; }

    public string? Bodega { get; init; }

    public bool? Success { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;

    public string Tab { get; init; } = "notificaciones";
}

public class RadicaWebPanelDatos
{
    public RadicaWebFiltrosConsulta Filtros { get; init; } = new();

    public IReadOnlyList<string> UsuariosDisponibles { get; init; } = [];

    public IReadOnlyList<RadicaWebNotificacionConsulta> Notificaciones { get; init; } = [];

    public int TotalNotificaciones { get; init; }

    public int Pagina { get; init; }

    public int TamanoPagina { get; init; }

    public int TotalPaginas { get; init; }

    public RadicaWebKpiResumen KpiResumen { get; init; } = new();

    public IReadOnlyList<RadicaWebUsuarioKpi> PorUsuario { get; init; } = [];

    public IReadOnlyList<RadicaWebBodegaKpi> PorBodega { get; init; } = [];

    public IReadOnlyList<RadicaWebFechaFacturaKpi> PorFechaFactura { get; init; } = [];

    public bool ApiConfigurada { get; init; }
}

public class RenotificarRadicaWebResultado
{
    public bool Exito { get; init; }

    public string Mensaje { get; init; } = string.Empty;

    public int? StatusCode { get; init; }
}

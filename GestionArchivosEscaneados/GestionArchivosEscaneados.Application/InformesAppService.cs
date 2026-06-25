using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Models.Entities;

namespace GestionArchivosEscaneados.Application;

public class InformesAppService
{
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;

    public InformesAppService(ITrazabilidadConsultaSqlService trazabilidad)
    {
        _trazabilidad = trazabilidad;
    }

    public async Task<InformesDatos> ObtenerInformesAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? usuarioDetalle,
        CancellationToken cancellationToken = default)
    {
        var usuarios = await _trazabilidad.ListarUsuariosConEscaneosAsync(cancellationToken);

        return new InformesDatos
        {
            Desde = desde,
            Hasta = hasta,
            UsuarioDetalle = string.IsNullOrWhiteSpace(usuarioDetalle) ? null : usuarioDetalle.Trim(),
            UsuariosDisponibles = usuarios.Select(u => u.NombreUsuario).ToList(),
            TotalHistorico = await _trazabilidad.ContarDocumentosEscaneadosAsync(desde, hasta, cancellationToken: cancellationToken),
            PorFecha = await _trazabilidad.ListarEscaneosPorFechaAsync(desde, hasta, cancellationToken: cancellationToken),
            PorUsuario = await _trazabilidad.ListarEscaneosPorUsuarioAsync(desde, hasta, cancellationToken),
            PorMes = await _trazabilidad.ListarEscaneosPorMesAsync(desde, hasta, cancellationToken: cancellationToken),
            PorDia = await _trazabilidad.ListarEscaneosPorFechaAsync(desde, hasta, cancellationToken: cancellationToken),
            UsuarioTotal = string.IsNullOrWhiteSpace(usuarioDetalle)
                ? null
                : await _trazabilidad.ContarDocumentosEscaneadosAsync(desde, hasta, usuarioDetalle.Trim(), cancellationToken),
            UsuarioPorMes = string.IsNullOrWhiteSpace(usuarioDetalle)
                ? []
                : await _trazabilidad.ListarEscaneosPorMesAsync(desde, hasta, usuarioDetalle.Trim(), cancellationToken),
            UsuarioPorDia = string.IsNullOrWhiteSpace(usuarioDetalle)
                ? []
                : await _trazabilidad.ListarEscaneosPorFechaAsync(desde, hasta, usuarioDetalle.Trim(), cancellationToken)
        };
    }
}

public class InformesDatos
{
    public DateOnly? Desde { get; init; }

    public DateOnly? Hasta { get; init; }

    public string? UsuarioDetalle { get; init; }

    public IReadOnlyList<string> UsuariosDisponibles { get; init; } = [];

    public int TotalHistorico { get; init; }

    public IReadOnlyList<FechaEscaneoResumen> PorFecha { get; init; } = [];

    public IReadOnlyList<UsuarioEscaneoTotal> PorUsuario { get; init; } = [];

    public IReadOnlyList<MesEscaneoResumen> PorMes { get; init; } = [];

    public IReadOnlyList<FechaEscaneoResumen> PorDia { get; init; } = [];

    public int? UsuarioTotal { get; init; }

    public IReadOnlyList<MesEscaneoResumen> UsuarioPorMes { get; init; } = [];

    public IReadOnlyList<FechaEscaneoResumen> UsuarioPorDia { get; init; } = [];
}

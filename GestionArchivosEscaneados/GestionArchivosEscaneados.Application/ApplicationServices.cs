using GestionArchivosEscaneados.Infrastructure.Auth;
using GestionArchivosEscaneados.Infrastructure.Logging;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace GestionArchivosEscaneados.Application;

public class AuthAppService
{
    private readonly UsuarioAuthService _usuarioAuth;

    public AuthAppService(UsuarioAuthService usuarioAuth)
    {
        _usuarioAuth = usuarioAuth;
    }

    public Task<string?> ValidarLoginAsync(string usuarioIngresado, CancellationToken cancellationToken = default) =>
        _usuarioAuth.ValidarYObtenerUsuarioNormalizadoAsync(usuarioIngresado, cancellationToken);
}

public class CalendarioAppService
{
    private readonly UncStorageService _unc;

    public CalendarioAppService(UncStorageService unc)
    {
        _unc = unc;
    }

    public IReadOnlyList<string> ObtenerFechasDisponibles(string usuario) =>
        _unc.ListarFechasDisponibles(usuario);

    public bool FechaExiste(string usuario, string fecha) =>
        _unc.ExisteCarpetaDia(usuario, fecha);
}

public class DashboardAppService
{
    private readonly UncStorageService _unc;
    private readonly LogDiarioService _logDiario;

    public DashboardAppService(UncStorageService unc, LogDiarioService logDiario)
    {
        _unc = unc;
        _logDiario = logDiario;
    }

    public async Task<ResumenLogDiario?> ObtenerResumenAsync(
        string usuario,
        string fecha,
        CancellationToken cancellationToken = default)
    {
        if (!_unc.ExisteCarpetaDia(usuario, fecha))
            return null;

        var rutas = _unc.ObtenerRutasDia(usuario, fecha);
        return await _logDiario.LeerResumenAsync(rutas, cancellationToken);
    }
}

public class ReprocesoAppService
{
    private readonly UncStorageService _unc;
    private readonly LogDiarioService _logDiario;
    private readonly Infrastructure.Api.SoporteProcesamientoService _soporte;

    public ReprocesoAppService(
        UncStorageService unc,
        LogDiarioService logDiario,
        Infrastructure.Api.SoporteProcesamientoService soporte)
    {
        _unc = unc;
        _logDiario = logDiario;
        _soporte = soporte;
    }

    public IReadOnlyList<ArchivoNoProcesado> ListarNoProcesados(string usuario, string fecha) =>
        _unc.ListarNoProcesados(usuario, fecha);

    public string? ResolverRutaPdf(string usuario, string fecha, string nombreArchivo) =>
        _unc.ResolverRutaPdfSegura(usuario, fecha, nombreArchivo);

    public async Task<SoporteProcesamientoEstado> ReprocesarAsync(
        string usuario,
        string fecha,
        string nombreArchivo,
        string codigoBarras,
        CancellationToken cancellationToken = default)
    {
        var ruta = _unc.ResolverRutaPdfSegura(usuario, fecha, nombreArchivo);
        if (ruta == null)
            return SoporteProcesamientoEstado.FalloApiDatos;

        var resultado = await _soporte.ProcesarAsync(
            codigoBarras.Trim(),
            ruta,
            usuario,
            cancellationToken);

        if (!resultado.EsExitoso)
            return resultado.Estado;

        var rutas = _unc.ObtenerRutasDia(usuario, fecha);
        _unc.MoverANoprocesadosAProcesados(ruta, rutas);
        await _logDiario.RegistrarReprocesoExitosoAsync(rutas, cancellationToken);

        return SoporteProcesamientoEstado.Exito;
    }

    public async Task<IReadOnlyList<ReprocesoLoteItemResult>> ReprocesarLoteAsync(
        string usuario,
        string fecha,
        IEnumerable<(string NombreArchivo, string CodigoBarras)> documentos,
        CancellationToken cancellationToken = default)
    {
        var resultados = new List<ReprocesoLoteItemResult>();

        foreach (var (nombreArchivo, codigoBarras) in documentos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(codigoBarras))
                continue;

            var estado = await ReprocesarAsync(
                usuario,
                fecha,
                nombreArchivo,
                codigoBarras,
                cancellationToken);

            resultados.Add(new ReprocesoLoteItemResult
            {
                NombreArchivo = nombreArchivo,
                CodigoBarras = codigoBarras.Trim(),
                Exito = estado == SoporteProcesamientoEstado.Exito,
                Estado = estado
            });
        }

        return resultados;
    }

    public async Task<bool> EliminarAsync(
        string usuario,
        string fecha,
        string nombreArchivo,
        CancellationToken cancellationToken = default)
    {
        if (!_unc.EliminarPdfNoProcesado(usuario, fecha, nombreArchivo))
            return false;

        var rutas = _unc.ObtenerRutasDia(usuario, fecha);
        await _logDiario.RegistrarEliminacionAsync(rutas, cancellationToken);
        return true;
    }
}

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddGestionArchivosApplication(this IServiceCollection services)
    {
        services.AddSingleton<AuthAppService>();
        services.AddSingleton<CalendarioAppService>();
        services.AddSingleton<DashboardAppService>();
        services.AddSingleton<ReprocesoAppService>();
        return services;
    }
}

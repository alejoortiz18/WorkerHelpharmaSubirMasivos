using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace GestionArchivosEscaneados.Infrastructure.Unc;

public class UncConexionService
{
    private const int Logon32LogonNewCredentials = 9;
    private const int Logon32ProviderWinnt50 = 3;

    private readonly IIntegracionConfigProvider _config;
    private readonly ILogger<UncConexionService> _logger;
    private bool _conexionWNetEstablecida;
    private string? _ultimaRaizUnc;
    private string? _ultimoUsuario;
    private string? _ultimaClave;

    public UncConexionService(
        IIntegracionConfigProvider config,
        ILogger<UncConexionService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string? UltimoErrorMensaje { get; private set; }

    public bool UsaCredenciales =>
        ResolverConfiguracion().UsarCredenciales;

    public bool AsegurarAccesoUnc() =>
        EjecutarConAcceso(() =>
        {
            var raizUnc = ResolverConfiguracion().RaizUnc;
            if (!Directory.Exists(raizUnc))
            {
                UltimoErrorMensaje = $"No se puede acceder a {raizUnc}";
                return false;
            }

            return true;
        });

    public T EjecutarConAcceso<T>(Func<T> operacion)
    {
        UltimoErrorMensaje = null;
        var configuracion = ResolverConfiguracion();

        if (string.IsNullOrWhiteSpace(configuracion.RaizUnc))
        {
            UltimoErrorMensaje = "Rutas:RaizUnc no está configurada.";
            return operacion();
        }

        try
        {
            if (OperatingSystem.IsWindows() && configuracion.UsarCredenciales)
            {
                _logger.LogDebug("UncEjecutarConAcceso | Modo=Impersonacion | RaizUnc={RaizUnc}", configuracion.RaizUnc);
                return EjecutarConImpersonacion(operacion, configuracion);
            }

            _logger.LogWarning(
                "UncEjecutarConAcceso | Modo=IdentidadProceso | UsaCredenciales={UsaCredenciales} | RaizUnc={RaizUnc}",
                configuracion.UsarCredenciales,
                configuracion.RaizUnc);
            EstablecerConexionWNetSiAplica(configuracion);
            return operacion();
        }
        catch (Exception ex)
        {
            UltimoErrorMensaje = ex.Message;
            _logger.LogError(ex, "UncOperacionFallo | RaizUnc={RaizUnc}", configuracion.RaizUnc);
            throw;
        }
    }

    public async Task<T> EjecutarConAccesoAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken = default)
    {
        UltimoErrorMensaje = null;
        var configuracion = await ResolverConfiguracionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(configuracion.RaizUnc))
        {
            UltimoErrorMensaje = "Rutas:RaizUnc no está configurada.";
            return await operacion(cancellationToken);
        }

        try
        {
            if (OperatingSystem.IsWindows() && configuracion.UsarCredenciales)
            {
                _logger.LogDebug("UncEjecutarConAccesoAsync | Modo=Impersonacion | RaizUnc={RaizUnc}", configuracion.RaizUnc);
                return await EjecutarConImpersonacionAsync(
                    () => operacion(cancellationToken),
                    configuracion,
                    cancellationToken);
            }

            _logger.LogWarning(
                "UncEjecutarConAccesoAsync | Modo=IdentidadProceso | UsaCredenciales={UsaCredenciales} | RaizUnc={RaizUnc}",
                configuracion.UsarCredenciales,
                configuracion.RaizUnc);
            EstablecerConexionWNetSiAplica(configuracion);
            return await operacion(cancellationToken);
        }
        catch (Exception ex)
        {
            UltimoErrorMensaje = ex.Message;
            _logger.LogError(ex, "UncOperacionFallo | RaizUnc={RaizUnc}", configuracion.RaizUnc);
            throw;
        }
    }

    public void EjecutarConAcceso(Action operacion) =>
        EjecutarConAcceso(() =>
        {
            operacion();
            return true;
        });

    public void InvalidarConexionRed()
    {
        _conexionWNetEstablecida = false;
        _ultimaRaizUnc = null;
        _ultimoUsuario = null;
        _ultimaClave = null;
    }

    private UncRuntimeConfig ResolverConfiguracion() =>
        ResolverConfiguracionAsync(CancellationToken.None).GetAwaiter().GetResult();

    private async Task<UncRuntimeConfig> ResolverConfiguracionAsync(CancellationToken cancellationToken)
    {
        var raizUnc = (await _config.ObtenerRaizUncAsync(cancellationToken)).Trim();
        var usuario = (await _config.ObtenerRedUsuarioAsync(cancellationToken)).Trim();
        var clave = await _config.ObtenerRedClaveAsync(cancellationToken);
        var usarCredenciales = await _config.UsaCredencialesUncAsync(cancellationToken);

        InvalidarConexionRedSiCambio(raizUnc, usuario, clave);

        return new UncRuntimeConfig(raizUnc, usuario, clave, usarCredenciales);
    }

    private void InvalidarConexionRedSiCambio(string raizUnc, string usuario, string clave)
    {
        if (_ultimaRaizUnc == raizUnc && _ultimoUsuario == usuario && _ultimaClave == clave)
            return;

        InvalidarConexionRed();
        _ultimaRaizUnc = raizUnc;
        _ultimoUsuario = usuario;
        _ultimaClave = clave;
    }

    private T EjecutarConImpersonacion<T>(Func<T> operacion, UncRuntimeConfig configuracion)
    {
        using var token = AbrirTokenRed(configuracion);
        return WindowsIdentity.RunImpersonated(token, operacion);
    }

    private async Task<T> EjecutarConImpersonacionAsync<T>(
        Func<Task<T>> operacion,
        UncRuntimeConfig configuracion,
        CancellationToken cancellationToken)
    {
        using var token = AbrirTokenRed(configuracion);
        return await WindowsIdentity.RunImpersonatedAsync(token, operacion);
    }

    private SafeAccessTokenHandle AbrirTokenRed(UncRuntimeConfig configuracion)
    {
        var (dominio, usuario) = ResolverDominioYUsuario(configuracion);

        if (!LogonUser(
                usuario,
                dominio,
                configuracion.Clave,
                Logon32LogonNewCredentials,
                Logon32ProviderWinnt50,
                out var tokenHandle))
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                error,
                $"LogonUser falló para '{dominio}\\{usuario}' (código {error}).");
        }

        _logger.LogInformation(
            "UncImpersonacionLista | Dominio={Dominio} | Usuario={Usuario}",
            dominio,
            usuario);

        return new SafeAccessTokenHandle(tokenHandle);
    }

    private (string Dominio, string Usuario) ResolverDominioYUsuario(UncRuntimeConfig configuracion)
    {
        var usuario = configuracion.Usuario.Trim();

        if (usuario.Contains('\\'))
        {
            var partes = usuario.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
            return (partes[0], partes[1]);
        }

        var servidor = configuracion.RaizUnc.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries)[0];
        return (servidor, usuario);
    }

    private void EstablecerConexionWNetSiAplica(UncRuntimeConfig configuracion)
    {
        if (!OperatingSystem.IsWindows() || !configuracion.UsarCredenciales || _conexionWNetEstablecida)
            return;

        var (dominio, usuario) = ResolverDominioYUsuario(configuracion);
        var usuarioCompleto = string.IsNullOrWhiteSpace(dominio) ? usuario : $"{dominio}\\{usuario}";

        var resultado = WNetAddConnection2(
            new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = configuracion.RaizUnc.TrimEnd('\\')
            },
            configuracion.Clave,
            usuarioCompleto,
            0);

        if (resultado != 0 && resultado != 1219)
            throw new Win32Exception(resultado);

        _conexionWNetEstablecida = true;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(
        string lpszUsername,
        string? lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out IntPtr phToken);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(
        NetResource netResource,
        string password,
        string username,
        int flags);

    [StructLayout(LayoutKind.Sequential)]
    private class NetResource
    {
        public ResourceScope Scope { get; set; }
        public ResourceType ResourceType { get; set; }
        public ResourceDisplaytype DisplayType { get; set; }
        public int Usage { get; set; }
        public string LocalName { get; set; } = string.Empty;
        public string RemoteName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
    }

    private enum ResourceScope : int
    {
        Connected = 1,
        GlobalNetwork = 2,
        Remembered = 3,
        Recent = 4,
        Context = 5
    }

    private enum ResourceType : int
    {
        Any = 0,
        Disk = 1,
        Print = 2
    }

    private enum ResourceDisplaytype : int
    {
        Generic = 0x0,
        Domain = 0x01,
        Server = 0x02,
        Share = 0x03
    }

    private sealed record UncRuntimeConfig(
        string RaizUnc,
        string Usuario,
        string Clave,
        bool UsarCredenciales);
}

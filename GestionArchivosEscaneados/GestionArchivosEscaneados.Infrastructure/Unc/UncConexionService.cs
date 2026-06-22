using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;

namespace GestionArchivosEscaneados.Infrastructure.Unc;

public class UncConexionService
{
    private const int Logon32LogonNewCredentials = 9;
    private const int Logon32ProviderWinnt50 = 3;

    private readonly RutasSettings _rutas;
    private readonly RedSettings _red;
    private readonly ILogger<UncConexionService> _logger;
    private bool _conexionWNetEstablecida;

    public UncConexionService(
        IOptions<RutasSettings> rutas,
        IOptions<RedSettings> red,
        ILogger<UncConexionService> logger)
    {
        _rutas = rutas.Value;
        _red = red.Value;
        _logger = logger;
    }

    public string? UltimoErrorMensaje { get; private set; }

    public bool UsaCredenciales =>
        _red.UsarCredencialesConfiguradas &&
        !string.IsNullOrWhiteSpace(_red.Usuario) &&
        !string.IsNullOrWhiteSpace(_red.Clave);

    public bool AsegurarAccesoUnc() =>
        EjecutarConAcceso(() =>
        {
            if (!Directory.Exists(_rutas.RaizUnc))
            {
                UltimoErrorMensaje = $"No se puede acceder a {_rutas.RaizUnc}";
                return false;
            }

            return true;
        });

    public T EjecutarConAcceso<T>(Func<T> operacion)
    {
        UltimoErrorMensaje = null;

        if (string.IsNullOrWhiteSpace(_rutas.RaizUnc))
        {
            UltimoErrorMensaje = "Rutas:RaizUnc no está configurada.";
            return operacion();
        }

        try
        {
            if (OperatingSystem.IsWindows() && UsaCredenciales)
            {
                _logger.LogDebug("UncEjecutarConAcceso | Modo=Impersonacion | RaizUnc={RaizUnc}", _rutas.RaizUnc);
                return EjecutarConImpersonacion(operacion);
            }

            _logger.LogWarning(
                "UncEjecutarConAcceso | Modo=IdentidadProceso | UsaCredenciales={UsaCredenciales} | RaizUnc={RaizUnc}",
                UsaCredenciales,
                _rutas.RaizUnc);
            EstablecerConexionWNetSiAplica();
            return operacion();
        }
        catch (Exception ex)
        {
            UltimoErrorMensaje = ex.Message;
            _logger.LogError(ex, "UncOperacionFallo | RaizUnc={RaizUnc}", _rutas.RaizUnc);
            throw;
        }
    }

    public async Task<T> EjecutarConAccesoAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken = default)
    {
        UltimoErrorMensaje = null;

        if (string.IsNullOrWhiteSpace(_rutas.RaizUnc))
        {
            UltimoErrorMensaje = "Rutas:RaizUnc no está configurada.";
            return await operacion(cancellationToken);
        }

        try
        {
            if (OperatingSystem.IsWindows() && UsaCredenciales)
            {
                _logger.LogDebug("UncEjecutarConAccesoAsync | Modo=Impersonacion | RaizUnc={RaizUnc}", _rutas.RaizUnc);
                return await EjecutarConImpersonacionAsync(
                    () => operacion(cancellationToken),
                    cancellationToken);
            }

            _logger.LogWarning(
                "UncEjecutarConAccesoAsync | Modo=IdentidadProceso | UsaCredenciales={UsaCredenciales} | RaizUnc={RaizUnc}",
                UsaCredenciales,
                _rutas.RaizUnc);
            EstablecerConexionWNetSiAplica();
            return await operacion(cancellationToken);
        }
        catch (Exception ex)
        {
            UltimoErrorMensaje = ex.Message;
            _logger.LogError(ex, "UncOperacionFallo | RaizUnc={RaizUnc}", _rutas.RaizUnc);
            throw;
        }
    }

    public void EjecutarConAcceso(Action operacion) =>
        EjecutarConAcceso(() =>
        {
            operacion();
            return true;
        });

    private T EjecutarConImpersonacion<T>(Func<T> operacion)
    {
        using var token = AbrirTokenRed();
        return WindowsIdentity.RunImpersonated(token, operacion);
    }

    private async Task<T> EjecutarConImpersonacionAsync<T>(
        Func<Task<T>> operacion,
        CancellationToken cancellationToken)
    {
        using var token = AbrirTokenRed();
        return await WindowsIdentity.RunImpersonatedAsync(token, operacion);
    }

    private SafeAccessTokenHandle AbrirTokenRed()
    {
        var (dominio, usuario) = ResolverDominioYUsuario();

        if (!LogonUser(
                usuario,
                dominio,
                _red.Clave,
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

    private (string Dominio, string Usuario) ResolverDominioYUsuario()
    {
        var usuario = _red.Usuario.Trim();

        if (usuario.Contains('\\'))
        {
            var partes = usuario.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
            return (partes[0], partes[1]);
        }

        var servidor = _rutas.RaizUnc.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries)[0];
        return (servidor, usuario);
    }

    private void EstablecerConexionWNetSiAplica()
    {
        if (!OperatingSystem.IsWindows() || !UsaCredenciales || _conexionWNetEstablecida)
            return;

        var (dominio, usuario) = ResolverDominioYUsuario();
        var usuarioCompleto = string.IsNullOrWhiteSpace(dominio) ? usuario : $"{dominio}\\{usuario}";

        var resultado = WNetAddConnection2(
            new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = _rutas.RaizUnc.TrimEnd('\\')
            },
            _red.Clave,
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
}

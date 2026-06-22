using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;
using Models;
using Models.Dto;

namespace Infrastructure;

public class RedDisponibleService
{
    private const int Logon32LogonNewCredentials = 9;
    private const int Logon32ProviderWinnt50 = 3;

    private readonly RutasSettings _rutas;
    private readonly RedSettings _red;
    private readonly ILogger<RedDisponibleService> _logger;
    private bool _conexionWNetEstablecida;

    public string? UltimoErrorMensaje { get; private set; }

    public RedDisponibleService(
        IOptions<RutasSettings> rutasOptions,
        IOptions<RedSettings> redOptions,
        ILogger<RedDisponibleService> logger)
    {
        _rutas = rutasOptions.Value;
        _red = redOptions.Value;
        _logger = logger;
    }

    public bool UsaCredenciales =>
        _red.UsarCredencialesConfiguradas &&
        !string.IsNullOrWhiteSpace(_red.Usuario) &&
        !string.IsNullOrWhiteSpace(_red.Clave);

    public bool EstaDisponible()
    {
        try
        {
            return EjecutarConAcceso(() =>
            {
                if (!Directory.Exists(_rutas.RaizUnc))
                {
                    UltimoErrorMensaje = $"No se puede acceder a la ruta UNC: {_rutas.RaizUnc}";
                    _logger.LogError(
                        "RedNoDisponible | RaizUnc={RaizUnc} | Mensaje={Mensaje}",
                        _rutas.RaizUnc,
                        UltimoErrorMensaje);
                    return false;
                }

                return true;
            });
        }
        catch
        {
            return false;
        }
    }

    public T EjecutarConAcceso<T>(Func<T> operacion)
    {
        UltimoErrorMensaje = null;

        try
        {
            if (OperatingSystem.IsWindows() && UsaCredenciales)
                return EjecutarConImpersonacion(operacion);

            EstablecerConexionWNetSiAplica();
            return operacion();
        }
        catch (Exception ex)
        {
            UltimoErrorMensaje = ex.Message;
            _logger.LogError(ex, "RedNoDisponible | RaizUnc={RaizUnc}", _rutas.RaizUnc);
            throw;
        }
    }

    public void EjecutarConAcceso(Action operacion) =>
        EjecutarConAcceso(() =>
        {
            operacion();
            return true;
        });

    public async Task EjecutarConAccesoAsync(Func<Task> operacion)
    {
        UltimoErrorMensaje = null;

        try
        {
            if (OperatingSystem.IsWindows() && UsaCredenciales)
            {
                using var token = AbrirTokenRed();
                await WindowsIdentity.RunImpersonatedAsync(token, operacion);
                return;
            }

            EstablecerConexionWNetSiAplica();
            await operacion();
        }
        catch (Exception ex)
        {
            UltimoErrorMensaje = ex.Message;
            _logger.LogError(ex, "RedNoDisponible | RaizUnc={RaizUnc}", _rutas.RaizUnc);
            throw;
        }
    }

    public async Task<T> EjecutarConAccesoAsync<T>(Func<Task<T>> operacion)
    {
        UltimoErrorMensaje = null;

        try
        {
            if (OperatingSystem.IsWindows() && UsaCredenciales)
            {
                using var token = AbrirTokenRed();
                return await WindowsIdentity.RunImpersonatedAsync(token, operacion);
            }

            EstablecerConexionWNetSiAplica();
            return await operacion();
        }
        catch (Exception ex)
        {
            UltimoErrorMensaje = ex.Message;
            _logger.LogError(ex, "RedNoDisponible | RaizUnc={RaizUnc}", _rutas.RaizUnc);
            throw;
        }
    }

    private T EjecutarConImpersonacion<T>(Func<T> operacion)
    {
        using var token = AbrirTokenRed();
        return WindowsIdentity.RunImpersonated(token, operacion);
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

        var servidor = _rutas.RaizUnc.TrimStart('\\')
            .Split('\\', StringSplitOptions.RemoveEmptyEntries)[0];

        return (servidor, usuario);
    }

    private void EstablecerConexionWNetSiAplica()
    {
        if (!OperatingSystem.IsWindows() || !UsaCredenciales || _conexionWNetEstablecida)
            return;

        var usuarioCompleto = ResolverDominioYUsuario();
        var usuario = $"{usuarioCompleto.Dominio}\\{usuarioCompleto.Usuario}";

        var resultado = WNetAddConnection2(
            new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = _rutas.RaizUnc.TrimEnd('\\')
            },
            _red.Clave,
            usuario,
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

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;

namespace MoverDocumentos.Core.Services;

public class RedDisponibleService
{
    private readonly RutasSettings _rutas;
    private readonly RedSettings _red;
    private readonly ILogger<RedDisponibleService> _logger;
    private bool _conexionEstablecida;

    public RedDisponibleService(
        IOptions<RutasSettings> rutasOptions,
        IOptions<RedSettings> redOptions,
        ILogger<RedDisponibleService> logger)
    {
        _rutas = rutasOptions.Value;
        _red = redOptions.Value;
        _logger = logger;
    }

    public bool EstaDisponible()
    {
        try
        {
            if (_red.UsarCredencialesConfiguradas &&
                !string.IsNullOrWhiteSpace(_red.Usuario))
            {
                EstablecerConexionUncSiAplica();
            }

            return Directory.Exists(_rutas.RaizUnc);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "RedNoDisponible | RaizUnc={RaizUnc}",
                _rutas.RaizUnc);
            return false;
        }
    }

    private void EstablecerConexionUncSiAplica()
    {
        if (_conexionEstablecida)
            return;

        var resultado = WNetAddConnection2(
            new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = _rutas.RaizUnc
            },
            _red.Clave,
            _red.Usuario,
            0);

        if (resultado != 0 && resultado != 1219)
            throw new Win32Exception(resultado);

        _conexionEstablecida = true;
    }

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

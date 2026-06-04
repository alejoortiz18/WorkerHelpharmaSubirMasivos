using MoverDocumentos;
using MoverDocumentos.Core;
using MoverDocumentos.Core.Configuration;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MoverDocumentos";
});

builder.Services.Configure<RutasSettings>(builder.Configuration.GetSection("Rutas"));
builder.Services.Configure<RedSettings>(builder.Configuration.GetSection("Red"));
builder.Services.Configure<LoteSettings>(builder.Configuration.GetSection("Lote"));
builder.Services.Configure<ArchivoSettings>(builder.Configuration.GetSection("Archivo"));
builder.Services.Configure<ReintentosSettings>(builder.Configuration.GetSection("Reintentos"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

builder.Services.AddMoverDocumentosCore();
builder.Services.AddHostedService<Worker>();

if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(settings =>
    {
        settings.SourceName = "MoverDocumentos";
        settings.LogName = "Application";
    });
}

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation(
    "Startup | ContentRoot={ContentRoot} | Env={Env}",
    builder.Environment.ContentRootPath,
    builder.Environment.EnvironmentName);

host.Run();

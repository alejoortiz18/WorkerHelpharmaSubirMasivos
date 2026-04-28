using Infrastructure;
using IronBarCode;
using MasivosWorker;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using Services;

var builder = Host.CreateApplicationBuilder(args);

// 🔥 ESTO ES LO QUE TE FALTABA
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MasivosWorker";
});

// Configuración
builder.Services.Configure<IronBarcodeSettings>(
    builder.Configuration.GetSection("IronBarcode"));

builder.Services.Configure<RutasSettings>(
    builder.Configuration.GetSection("Rutas"));

builder.Services.AddSingleton<IronBarcodeLicenseInitializer>();
builder.Services.AddSingleton<FileManagerInfraestructure>();
builder.Services.AddSingleton<FileWatcherInfraestructure>();
builder.Services.AddSingleton<BarcodeRegionService>();
builder.Services.AddHttpClient<SoporteApiService>();
builder.Services.AddHttpClient<SoporteFisicoApiService>();

// Worker
builder.Services.AddHostedService<Worker>();
builder.Services.Configure<FileSettings>(
    builder.Configuration.GetSection("FileSettings"));
var host = builder.Build();

// Inicializar licencia
host.Services.GetRequiredService<IronBarcodeLicenseInitializer>();

var ironSettings = host.Services.GetRequiredService<IOptions<IronBarcodeSettings>>().Value;
License.LicenseKey = ironSettings.LicenseKey;

host.Run();
using MasivosWorker;
using Microsoft.Extensions.Options;
using IronBarCode;
using Microsoft.Extensions.Options;
using Models.Dto;
using Infrastructure;
using Services;

var builder = Host.CreateApplicationBuilder(args);

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

var host = builder.Build();
host.Services.GetRequiredService<IronBarcodeLicenseInitializer>();

// Aplicar licencia
var ironSettings = host.Services.GetRequiredService<IOptions<IronBarcodeSettings>>().Value;
License.LicenseKey = ironSettings.LicenseKey;

host.Run();
using MasivosWorker;
using Microsoft.Extensions.Options;
using IronBarCode;
using Microsoft.Extensions.Options;
using Models.Dto;
using Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Configuración
builder.Services.Configure<IronBarcodeSettings>(
    builder.Configuration.GetSection("IronBarcode"));

builder.Services.Configure<RutasSettings>(
    builder.Configuration.GetSection("Rutas"));

builder.Services.AddSingleton<FileManagerService>();
builder.Services.AddSingleton<FileWatcherService>();

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Aplicar licencia
var ironSettings = host.Services.GetRequiredService<IOptions<IronBarcodeSettings>>().Value;
License.LicenseKey = ironSettings.LicenseKey;

host.Run();
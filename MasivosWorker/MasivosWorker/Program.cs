using Infrastructure;
using MasivosWorker;
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

builder.Services.Configure<ApiCredentialsSettings>(
    builder.Configuration.GetSection("ApiCredentials"));

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

host.Run();
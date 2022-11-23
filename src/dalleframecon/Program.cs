using dalleframecon.HostedServices;
using dalleframecon.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using dalleframecon.Handlers;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging((context, builder) =>
{
    builder.ClearProviders();
    builder.AddDebug();
});

string configurationFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "configuration.json");

builder.ConfigureAppConfiguration((builder) => builder
    .AddJsonFile(configurationFilePath)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>());

builder.ConfigureServices((context, services) =>
{
    // Setup configuration options
    IConfiguration configurationRoot = context.Configuration;
    services.Configure<AzureCognitiveServicesOptions>(configurationRoot.GetSection("AzureCognitiveServices"));
    services.Configure<OpenAiServiceOptions>(configurationRoot.GetSection("OpenAI"));

    services.AddSingleton<Dalle2Handler>();

    services.AddSingleton<AzCognitiveServicesWakeWordListener>();
    services.AddSingleton<AzCognitiveServicesListener>();
    services.AddSingleton<AzCognitiveServicesSpeaker>();
    
    services.AddHostedService<ScreenRenderService>();
});

IHost host = builder.Build();
await host.RunAsync();

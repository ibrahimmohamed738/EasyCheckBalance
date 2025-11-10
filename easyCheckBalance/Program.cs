using easyCheckBalance;
using easyCheckBalance.Core;
using Serilog;


var builder = Host.CreateApplicationBuilder(args);

// Adding serilog 
Log.Logger = new LoggerConfiguration()
    .ReadFrom
    .Configuration(builder.Configuration)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "Short Way Balance Check USSD App";
    })
    .UseSerilog()
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHttpClient<DahabAPI>();
        services.AddTransient<Responder>();
        services.AddTransient<Repository>();
        services.Configure<Config>(builder.Configuration.GetSection("Config"));
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();

using easyCheckBalance;
using easyCheckBalance.Core;
using Serilog;


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHttpClient<DahabAPI>();
        services.AddTransient<Responder>();
        services.AddTransient<Repository>();
        services.AddHostedService<Worker>();
        services.Configure<Config>(hostContext.Configuration.GetSection("Config"));
    }).UseWindowsService().Build();
try
{
    Log.Information("Starting up the Service. ");
    await host.RunAsync();
    return;
}
catch (Exception ex)
{
    Log.Fatal("Problem with Starting Service. " + ex.Message);
    return;
}
finally
{
    Log.CloseAndFlush();
}

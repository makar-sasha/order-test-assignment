using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Order.Core.Messaging;
using Order.Core.Orders;
using Order.Handler;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Orders;
using Serilog;

var builder = Host.CreateDefaultBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Information() 
    .CreateLogger();

builder.UseSerilog();

builder.ConfigureAppConfiguration((_, config) =>
{
    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();
});

builder.ConfigureServices((hostContext, services) =>
{
    services.Configure<OrderFilesSettings>(hostContext.Configuration.GetSection("OrderFilesSettings"));
    services.AddSingleton(sp => sp.GetRequiredService<IOptions<OrderFilesSettings>>().Value);

    services.AddTransient<IOrderRepository>(provider =>
    {
        var connectionString = hostContext.Configuration.GetConnectionString("SQLite")
                               ?? throw new InvalidOperationException("The connection string 'SQLite' is not configured.");
        return new OrderSqliteRepository(connectionString);
    });
    
    services.AddSingleton<IFileName, FileName>();
    services.AddSingleton<ISignal, SignalLocalWin>();
    
    services.AddHttpClient<OrderFiles>((provider, client) =>
    {
        var settings = provider.GetRequiredService<OrderFilesSettings>();
        client.Timeout = TimeSpan.FromSeconds(settings.HttpClientTimeoutSeconds);
    });

    services.AddHostedService<OrderFiles>();
});

try
{
    var app = builder.Build();
    using (var scope = app.Services.CreateScope())
    {
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        try
        {
            await orderRepository.InitializeSchema(CancellationToken.None);
            Log.Information("Database schema initialized successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to initialize database schema.");
            throw;
        }
    } 
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
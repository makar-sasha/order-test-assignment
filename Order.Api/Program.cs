using Microsoft.AspNetCore.Mvc;
using Order.Api;
using Order.Core.Messaging;
using Order.Core.Orders;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Orders;
using Polly;
using Polly.Wrap;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Information()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddLogging();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddTransient<IOrderRepository>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("SQLite")
                           ?? throw new InvalidOperationException("The connection string 'SQLite' is not configured.");
    return new OrderSqliteRepository(connectionString);
});

builder.Services.AddSingleton<ISignal, SignalLocalWin>();

builder.Services.AddSingleton<AsyncPolicyWrap>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<Program>>();

    var retryPolicy = Policy
        .Handle<Exception>()
        .RetryAsync(3);

    var fallbackPolicy = Policy
        .Handle<Exception>()
        .FallbackAsync((_) =>
            {
                logger.LogError("Signal operation failed after retries. Executing fallback.");
                return Task.CompletedTask;
            });

    return Policy.WrapAsync(fallbackPolicy, retryPolicy);
});

builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        if (exception != null)
        {
            logger.LogError(exception, "Unhandled exception occurred while processing request {RequestPath}.", context.Request.Path);
        }

        var errorResponse = new { error = "An unexpected error occurred. Please try again later." };
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(errorResponse);
    });
});

app.MapOrderEndpoints();

app.Run();

using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;

using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("QuotesApi"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddConsoleExporter();
    })
    .UseAzureMonitorExporter();

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.AddConsoleExporter();
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);

// Day 17: the Angular frontend now runs as a static site on Azure Static
// Web Apps instead of only a local dev server, so the browser's Origin
// header is the SWA's *.azurestaticapps.net hostname, not localhost. A
// cross-origin browser request with no matching CORS policy is silently
// blocked by the browser (verified before this change: the live endpoint
// returned no Access-Control-Allow-Origin header for any Origin at all).
// Both the deployed SWA origin and the local dev origins are allowed here
// so `ng serve` keeps working unchanged. No credentials/cookies are
// involved (auth is a Bearer header, not cookies), so AllowCredentials is
// intentionally omitted.
builder.Services.AddCors(options =>
{
    options.AddPolicy("QuotesFrontend", policy =>
        policy.WithOrigins(
                "http://localhost:4200",
                "http://localhost:4201",
                "https://delightful-smoke-0b2c56200.7.azurestaticapps.net")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors("QuotesFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapQuoteEndpoints();
app.MapCollectionEndpoints();
app.MapAuthEndpoints();
app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}

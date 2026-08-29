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

// UseAzureMonitorExporter() throws at startup — "A connection string was not found" —
// if APPLICATIONINSIGHTS_CONNECTION_STRING is unset, which takes the whole host down
// before it ever listens. That is right for the deployed Container App, where the
// setting is always present, and wrong for `dotnet run` on a laptop, where telemetry
// export is not the point. Attach the exporter only when there is somewhere to export
// to; the console exporters below keep working either way.
var appInsightsConnectionString =
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

var openTelemetry = builder.Services.AddOpenTelemetry()
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
    });

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    openTelemetry.UseAzureMonitorExporter();
}

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.AddConsoleExporter();
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors("frontend");

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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuotesApi.Data;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Quotes")
            ?? "Data Source=quotes.db";

        services.AddDbContext<QuoteDbContext>(options =>
            options
                .UseSqlite(connectionString)
                .EnableSensitiveDataLogging()
                .LogTo(
                    Console.WriteLine,
                    new[]
                    {
                        DbLoggerCategory.Database.Command.Name
                    },
                    LogLevel.Information));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}

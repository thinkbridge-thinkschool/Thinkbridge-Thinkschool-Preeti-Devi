using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using EfCoreTrackingBenchmark;

const int RowCount = 10_000;

using (var setupContext = new BenchmarkContext())
{
    setupContext.Database.EnsureDeleted();
    setupContext.Database.EnsureCreated();

    if (!setupContext.Quotes.Any())
    {
        var quotes = Enumerable.Range(1, RowCount)
            .Select(i => new Quote
            {
                Id = i,
                Text = $"Quote number {i}",
                Author = $"Author {i % 100}"
            });

        setupContext.Quotes.AddRange(quotes);
        setupContext.SaveChanges();
    }
}

Console.WriteLine($"Database contains {RowCount:N0} rows.");
Console.WriteLine();

Console.WriteLine("=== Identity Resolution / Tracking ===");

using (var context = new BenchmarkContext())
{
    var first = context.Quotes.First(q => q.Id == 1);
    var second = context.Quotes.First(q => q.Id == 1);

    Console.WriteLine($"First and second reference are same object: {ReferenceEquals(first, second)}");
    Console.WriteLine($"Entity state: {context.Entry(first).State}");
}

Console.WriteLine();

Console.WriteLine("=== Tracked Query ===");

var trackedResult = Measure(() =>
{
    using var context = new BenchmarkContext();

    var rows = context.Quotes
        .ToList();

    return rows.Count;
});

Console.WriteLine($"Rows read: {trackedResult.Rows:N0}");
Console.WriteLine($"Time: {trackedResult.ElapsedMs:F2} ms");
Console.WriteLine($"Allocated: {trackedResult.AllocatedBytes:N0} bytes");

Console.WriteLine();

Console.WriteLine("=== AsNoTracking Query ===");

var noTrackingResult = Measure(() =>
{
    using var context = new BenchmarkContext();

    var rows = context.Quotes
        .AsNoTracking()
        .ToList();

    return rows.Count;
});

Console.WriteLine($"Rows read: {noTrackingResult.Rows:N0}");
Console.WriteLine($"Time: {noTrackingResult.ElapsedMs:F2} ms");
Console.WriteLine($"Allocated: {noTrackingResult.AllocatedBytes:N0} bytes");

Console.WriteLine();

Console.WriteLine("=== Comparison ===");

Console.WriteLine(
    $"Time difference: {trackedResult.ElapsedMs - noTrackingResult.ElapsedMs:F2} ms");

Console.WriteLine(
    $"Allocation difference: {trackedResult.AllocatedBytes - noTrackingResult.AllocatedBytes:N0} bytes");

static BenchmarkResult Measure(Func<int> operation)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long before = GC.GetAllocatedBytesForCurrentThread();

    var stopwatch = Stopwatch.StartNew();

    int rows = operation();

    stopwatch.Stop();

    long after = GC.GetAllocatedBytesForCurrentThread();

    return new BenchmarkResult(
        rows,
        stopwatch.Elapsed.TotalMilliseconds,
        after - before);
}

record BenchmarkResult(
    int Rows,
    double ElapsedMs,
    long AllocatedBytes);
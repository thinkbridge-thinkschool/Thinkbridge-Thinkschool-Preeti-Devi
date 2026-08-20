# Day 10 - EF Core Change Tracker + AsNoTracking

## Objective

Demonstrate EF Core change tracking, identity resolution, and the read-performance difference between tracked queries and `AsNoTracking()` queries.

The benchmark reads 10,000 rows from SQLite.

## Identity Resolution

### Query

csharp
var first = context.Quotes.First(q => q.Id == 1);
var second = context.Quotes.First(q => q.Id == 1);

Console.WriteLine(ReferenceEquals(first, second));
Console.WriteLine(context.Entry(first).State);

### Observed Result

text
First and second reference are same object: True
Entity state: Unchanged

This demonstrates that within the same `DbContext`, EF Core returns the same tracked entity instance for the same database row.

## Query Variant 1 - Tracking

### Query

var rows = context.Quotes
    .ToList();

This is the default EF Core behavior for entity queries. The returned entities are tracked by the `DbContext`.

## Query Variant 2 - No Tracking

### Query

var rows = context.Quotes
    .AsNoTracking()
    .ToList();

`AsNoTracking()` tells EF Core not to track the returned entities, which is useful for read-only operations.

## Benchmark

The benchmark reads 10,000 rows using both query variants.

### Tracking

Rows read: 10,000
Time: 175.39 ms
Allocated: 9,615,104 bytes

### AsNoTracking

Rows read: 10,000
Time: 36.06 ms
Allocated: 3,782,432 bytes

### Difference

Time difference: 139.33 ms
Allocation difference: 5,832,672 bytes

In this run, `AsNoTracking()` was approximately 4.86x faster and allocated approximately 60.7% less memory than the tracked query.

These measurements are from this local benchmark run and can vary between machines and runs.

## When I Would NOT Use AsNoTracking

I would not use `AsNoTracking()` when I need to modify the queried entities and save those changes through the same `DbContext`, because the entities need to be tracked for EF Core to detect and persist the changes normally.

## Key Learning

EF Core tracking provides identity resolution and change detection, but it adds overhead to read-only queries; `AsNoTracking()` is therefore useful on read-heavy paths where the returned entities do not need to be modified.

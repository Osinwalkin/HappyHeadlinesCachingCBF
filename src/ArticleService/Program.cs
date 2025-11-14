using Prometheus;
using System.Text.Json;
using StackExchange.Redis;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.Enrich.FromLogContext()
                 .WriteTo.Console(new JsonFormatter()); // Output logs as structured JSON
});

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registrer Redis connection
// Connection hele applikationen kan bruge
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!, true);
    return ConnectionMultiplexer.Connect(configuration);
});

// Registrer ArticleCacheWorker som en hosted service
builder.Services.AddHostedService<ArticleCacheWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Lav test endpoint for at checke cache
// Checker om worker putter data ind i Redis
app.MapGet("/articles/{id}", async (int id, IConnectionMultiplexer redis, ILogger<Program> logger, HttpContext httpContext) =>
{
    // NEW: Manually read the Trace ID from the incoming request header
    var traceId = httpContext.Request.Headers["Trace-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();

    // NEW: Add the TraceId to the logging context for all logs in this request
    using (Serilog.Context.LogContext.PushProperty("TraceId", traceId))
    {
        var db = redis.GetDatabase();
        var key = $"article:{id}";
        var cachedArticle = await db.StringGetAsync(key);

        if (!cachedArticle.IsNullOrEmpty)
        {
            CachingMetrics.IncrementCacheHits("articles");
            logger.LogInformation("Cache HIT for article {ArticleId}", id); // Using structured logging
            return Results.Ok(JsonSerializer.Deserialize<Article>(cachedArticle!));
        }
        else
        {
            CachingMetrics.IncrementCacheMisses("articles");
            logger.LogWarning("Cache MISS for article {ArticleId}", id); // Using structured logging
            return Results.NotFound("Article not found in cache.");
        }
    }
});

app.MapControllers();
app.UseMetricServer();
app.Run();

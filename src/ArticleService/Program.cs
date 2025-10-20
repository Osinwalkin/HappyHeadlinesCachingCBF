using Prometheus;
using System.Text.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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
app.MapGet("/articles/{id}", async (int id, IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    var key = $"article:{id}";

    var cachedArticle = await db.StringGetAsync(key);

    if (!cachedArticle.IsNullOrEmpty)
    {
        CachingMetrics.IncrementCacheHits("articles");
        Console.WriteLine($"--> Cache HIT for article:{id}");
        return Results.Ok(JsonSerializer.Deserialize<Article>(cachedArticle!));
    }
    else
    {
        CachingMetrics.IncrementCacheMisses("articles");
        Console.WriteLine($"--> Cache MISS for article:{id}");
        return Results.NotFound("Article not found in cache.");
    }
});

app.MapControllers();
app.UseMetricServer();
app.Run();

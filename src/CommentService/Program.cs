using Prometheus;
using System.Text.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registrer Redis Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!, true);
    return ConnectionMultiplexer.Connect(configuration);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthorization();

// Cache miss logic
app.MapGet("/comments/article/{articleId}", async (int articleId, IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    var key = $"comments:article:{articleId}";

    // Check cachen først
    var cachedComments = await db.StringGetAsync(key);

    if (!cachedComments.IsNullOrEmpty)
    {
        CachingMetrics.IncrementCacheHits("comments");
        Console.WriteLine($"--> Cache HIT for comments on article:{articleId}");
        var comments = JsonSerializer.Deserialize<List<Comment>>(cachedComments!);
        return Results.Ok(comments);
    }

    // Cache miss, hent fra databasen
    CachingMetrics.IncrementCacheMisses("comments");
    Console.WriteLine($"--> Cache MISS for comments on article:{articleId}");
    var commentsFromDb = GetCommentsFromDatabase(articleId);

    if (commentsFromDb.Any())
    {
        var serializedComments = JsonSerializer.Serialize(commentsFromDb);
        await db.StringSetAsync(key, serializedComments);
    }

    return Results.Ok(commentsFromDb);
});

List<Comment> GetCommentsFromDatabase(int articleId)
{
    // returnerer mock data
    return new List<Comment>
    {
        new Comment { Id = 101, ArticleId = articleId, Author = "Alice", Text = "Great article!" },
        new Comment { Id = 102, ArticleId = articleId, Author = "Bob", Text = "Very insightful." }
    };
}

app.MapControllers();
app.UseMetricServer();
app.Run();
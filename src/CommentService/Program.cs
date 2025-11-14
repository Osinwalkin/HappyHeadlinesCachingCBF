using Prometheus;
using System.Text.Json;
using StackExchange.Redis;
using Polly;
using Polly.Extensions.Http;
using System.Net;


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

// CIRCUIT BREAKER POLICY

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 3,
        durationOfBreak: TimeSpan.FromSeconds(60)
    );


builder.Services.AddHttpClient("ProfanityServiceClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:9999");
})
.AddPolicyHandler(circuitBreakerPolicy);

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

// ENDPOINT TO DEMONSTRATE FAULT ISOLATION
app.MapPost("/comments", async (Comment comment, IHttpClientFactory clientFactory) =>
{
    Console.WriteLine("\n--> Received new comment. Attempting to check for profanity...");
    var httpClient = clientFactory.CreateClient("ProfanityServiceClient");

    try
    {
        var response = await httpClient.PostAsJsonAsync("/check", comment);
        
        Console.WriteLine("--> Profanity check successful. Saving to database.");
        SaveComment(comment, needsModeration: false);
        return Results.Ok("Comment posted successfully.");
    }
    catch (Polly.CircuitBreaker.BrokenCircuitException)
    {
        Console.WriteLine("--> Circuit is open. ProfanityService is down. Using FALLBACK.");
        SaveComment(comment, needsModeration: true);
        return Results.Accepted("Comment accepted, will be moderated later.");
    }
    catch (HttpRequestException)
    {
        Console.WriteLine("--> ProfanityService is unreachable. Circuit breaker is counting this failure.");
        SaveComment(comment, needsModeration: true);
        return Results.Accepted("Comment accepted due to a service error, will be moderated later.");
    }
});


app.MapControllers();
app.UseMetricServer();
app.Run();

void SaveComment(Comment comment, bool needsModeration)
{
    Console.WriteLine($"--> Saving comment from '{comment.Author}' to DB. Needs Moderation: {needsModeration}");
}

List<Comment> GetCommentsFromDatabase(int articleId)
{
    return new List<Comment>
    {
        new Comment { Id = 101, ArticleId = articleId, Author = "Alice", Text = "Great article!" },
        new Comment { Id = 102, ArticleId = articleId, Author = "Bob", Text = "Very insightful." }
    };
}

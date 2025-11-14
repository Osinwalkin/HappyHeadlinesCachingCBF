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

// --- CORRECTED CIRCUIT BREAKER POLICY ---

// --- FINAL, SIMPLIFIED POLICY ---

// 1. Define ONLY the circuit breaker policy.
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError() // This handles network errors like 5xx, 408, or HttpRequestException.
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 3,
        durationOfBreak: TimeSpan.FromSeconds(60)
    );

// 2. Register ONLY this policy with the HttpClient.
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

// --- NEW POST ENDPOINT TO DEMONSTRATE FAULT ISOLATION ---
app.MapPost("/comments", async (Comment comment, IHttpClientFactory clientFactory) =>
{
    Console.WriteLine("\n--> Received new comment. Attempting to check for profanity...");
    var httpClient = clientFactory.CreateClient("ProfanityServiceClient");

    try
    {
        // This call is now protected ONLY by the circuit breaker.
        var response = await httpClient.PostAsJsonAsync("/check", comment);
        
        // This code will only run if the call is successful.
        Console.WriteLine("--> Profanity check successful. Saving to database.");
        SaveComment(comment, needsModeration: false);
        return Results.Ok("Comment posted successfully.");
    }
    catch (Polly.CircuitBreaker.BrokenCircuitException)
    {
        // This block executes ONLY when the circuit is open. This is our FALLBACK logic.
        Console.WriteLine("--> Circuit is open. ProfanityService is down. Using FALLBACK.");
        SaveComment(comment, needsModeration: true);
        return Results.Accepted("Comment accepted, will be moderated later.");
    }
    catch (HttpRequestException)
    {
        // This block executes for the initial failures that the circuit breaker is counting.
        Console.WriteLine("--> ProfanityService is unreachable. Circuit breaker is counting this failure.");
        SaveComment(comment, needsModeration: true);
        // We still accept the comment, but the log shows it's a transient failure.
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

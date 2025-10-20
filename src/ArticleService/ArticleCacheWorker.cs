using System.Text.Json;
using StackExchange.Redis;

public class ArticleCacheWorker : BackgroundService
{
    private readonly ILogger<ArticleCacheWorker> _logger;
    private readonly IConnectionMultiplexer _redis;

    public ArticleCacheWorker(ILogger<ArticleCacheWorker> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Article Cache Worker starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Updating article cache...");

            try
            {
                // Få Redis database instance
                var db = _redis.GetDatabase();

                // Simuler at fetche data fra database
                var articlesFromDb = GetArticlesFromLast14Days();

                // Gem artikler i Redis cache
                foreach (var article in articlesFromDb)
                {
                    var key = $"article:{article.Id}";
                    var value = JsonSerializer.Serialize(article);
                    await db.StringSetAsync(key, value);
                }

                _logger.LogInformation("Article cache updated successfully with {Count} articles.", articlesFromDb.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the article cache.");
            }

            // Vent 10 minutter
            // Man kan sætte dette til 1 minut hvis man vil teste
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    // Mock database call
    private List<Article> GetArticlesFromLast14Days()
    {
        return new List<Article>
        {
            new Article { Id = 1, Title = "Good News Today", Content = "...", PublishedDate = DateTime.UtcNow.AddDays(-1) },
            new Article { Id = 2, Title = "Another Positive Story", Content = "...", PublishedDate = DateTime.UtcNow.AddDays(-5) },
            new Article { Id = 3, Title = "Global Uplift", Content = "...", PublishedDate = DateTime.UtcNow.AddDays(-10) }
        };
    }
}
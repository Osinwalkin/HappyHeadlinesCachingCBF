using Prometheus;

public static class CachingMetrics
{
    private static readonly Counter CacheHitsCounter = Metrics.CreateCounter(
        "cache_hits_total",
        "Total number of cache hits.",
        new CounterConfiguration
        {
            LabelNames = new[] { "cache_name" }
        });

    private static readonly Counter CacheMissesCounter = Metrics.CreateCounter(
        "cache_misses_total",
        "Total number of cache misses.",
        new CounterConfiguration
        {
            LabelNames = new[] { "cache_name" }
        });

    public static void IncrementCacheHits(string cacheName)
    {
        CacheHitsCounter.WithLabels(cacheName).Inc();
    }

    public static void IncrementCacheMisses(string cacheName)
    {
        CacheMissesCounter.WithLabels(cacheName).Inc();
    }
}
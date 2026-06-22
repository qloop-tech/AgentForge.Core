using StackExchange.Redis;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaWebhookIdempotencyStore(IConnectionMultiplexer redis)
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<bool> TryRegisterAsync(string? key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _database.StringSetAsync(
                ToRedisKey(key),
                DateTimeOffset.UtcNow.ToString("O"),
                Retention,
                When.NotExists)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        await _database.KeyDeleteAsync(ToRedisKey(key)).ConfigureAwait(false);
    }

    private static string ToRedisKey(string key)
        => $"agentforge:openwa:webhook:dedupe:{key}";
}

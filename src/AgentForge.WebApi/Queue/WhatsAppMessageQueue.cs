using StackExchange.Redis;

namespace AgentForge.WebApi.Queue;

public sealed class WhatsAppMessageQueue(
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WhatsAppMessageQueue> logger) : BackgroundService
{
    private const string StreamName = "agentforge:whatsapp:incoming";
    private const string DeadLetterStreamName = "agentforge:whatsapp:incoming:dead";
    private const string ConsumerGroup = "agentforge-webapi";
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PhoneLockExpiry = TimeSpan.FromMinutes(5);

    private readonly IDatabase _database = redis.GetDatabase();
    private readonly string _consumerName = $"{Environment.MachineName}-{Environment.ProcessId}";
    private readonly int _maxAttempts = Math.Max(1, configuration.GetValue("WHATSAPP_QUEUE_MAX_ATTEMPTS", 5));

    public async Task EnqueueAsync(
        string phone,
        string body,
        string? dedupeKey,
        string? deliveryId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Persist the inbound WhatsApp message in Redis Streams so it survives process restarts
        // and can be replayed or retried by the background consumer.
        await _database.StreamAddAsync(
                StreamName,
                [
                    new NameValueEntry("phone", phone),
                    new NameValueEntry("body", body),
                    new NameValueEntry("dedupeKey", dedupeKey ?? string.Empty),
                    new NameValueEntry("deliveryId", deliveryId ?? string.Empty),
                    new NameValueEntry("receivedAt", DateTimeOffset.UtcNow.ToString("O")),
                    new NameValueEntry("attempts", 0),
                ])
            .ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync().ConfigureAwait(false);
        logger.LogInformation("WhatsApp Redis Streams queue started as consumer {Consumer}", _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Prefer already-pending work first so the consumer can drain backlog before pulling new entries.
            var entries = await ReadNextBatchAsync(stoppingToken).ConfigureAwait(false);
            if (entries.Length == 0)
            {
                await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                continue;
            }

            foreach (var entry in entries)
            {
                await ProcessEntryAsync(entry, stoppingToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("WhatsApp Redis Streams queue stopped");
    }

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            // Consumer groups let multiple workers safely share the same Redis Stream without double-processing.
            await _database.StreamCreateConsumerGroupAsync(StreamName, ConsumerGroup, "0-0", createStream: true)
                .ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // The group already exists, which is expected on subsequent starts.
        }
    }

    private async Task<StreamEntry[]> ReadNextBatchAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read entries that this consumer previously claimed but has not finished yet before
        // pulling brand-new messages from the stream.
        var pendingForConsumer = await _database.StreamReadGroupAsync(
                StreamName,
                ConsumerGroup,
                _consumerName,
                "0-0",
                count: 10)
            .ConfigureAwait(false);

        if (pendingForConsumer.Length > 0)
        {
            return pendingForConsumer;
        }

        // When no pending work exists, fetch the next unclaimed messages from the stream.
        return await _database.StreamReadGroupAsync(
                StreamName,
                ConsumerGroup,
                _consumerName,
                ">",
                count: 10)
            .ConfigureAwait(false);
    }

    private async Task ProcessEntryAsync(StreamEntry entry, CancellationToken cancellationToken)
    {
        var message = WhatsAppQueueMessage.FromStreamEntry(entry);
        var lockKey = $"agentforge:whatsapp:lock:{message.Phone}";
        var lockValue = _consumerName;

        // Serialize work per phone so a second message cannot race in and interleave replies
        // for the same conversation.
        while (!await _database.StringSetAsync(lockKey, lockValue, PhoneLockExpiry, When.NotExists).ConfigureAwait(false))
        {
            await Task.Delay(IdleDelay, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var agentChat = scope.ServiceProvider.GetRequiredService<AgentChatService>();
            await agentChat.HandleAsync(message.Phone, message.Body, cancellationToken).ConfigureAwait(false);

            // A successful handler run means the work is complete, so the stream entry can be
            // acknowledged and removed from the pending set.
            await AcknowledgeAsync(entry.Id).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(entry.Id, message, ex).ConfigureAwait(false);
        }
        finally
        {
            await ReleasePhoneLockAsync(lockKey, lockValue).ConfigureAwait(false);
        }
    }

    private async Task HandleFailureAsync(RedisValue messageId, WhatsAppQueueMessage message, Exception ex)
    {
        var nextAttempt = message.Attempts + 1;
        if (nextAttempt >= _maxAttempts)
        {
            // After the maximum retry budget is exhausted, preserve the failure for inspection
            // and remove the original message from the active stream.
            await _database.StreamAddAsync(
                    DeadLetterStreamName,
                    message.ToNameValueEntries(nextAttempt, ex.Message))
                .ConfigureAwait(false);
            await AcknowledgeAsync(messageId).ConfigureAwait(false);
            logger.LogError(
                ex,
                "Dead-lettered WhatsApp message from {Phone} after {Attempts} attempts",
                message.Phone,
                nextAttempt);
            return;
        }

        logger.LogWarning(
            ex,
            "WhatsApp message processing failed for {Phone}; retrying attempt {Attempt}/{MaxAttempts}",
            message.Phone,
            nextAttempt + 1,
            _maxAttempts);

        // Requeue the message with an incremented attempt count so the consumer can retry later
        // without losing the original context or error details.
        await Task.Delay(RetryDelay).ConfigureAwait(false);
        await _database.StreamAddAsync(
                StreamName,
                message.ToNameValueEntries(nextAttempt, ex.Message))
            .ConfigureAwait(false);
        await AcknowledgeAsync(messageId).ConfigureAwait(false);
    }

    private async Task AcknowledgeAsync(RedisValue messageId)
    {
        // Mark the entry as processed by this consumer group and delete it from the main stream
        // so it does not reappear on future reads.
        await _database.StreamAcknowledgeAsync(StreamName, ConsumerGroup, messageId).ConfigureAwait(false);
        await _database.StreamDeleteAsync(StreamName, [messageId]).ConfigureAwait(false);
    }

    private async Task ReleasePhoneLockAsync(string key, string expectedValue)
    {
        const string script = """
            if redis.call('GET', KEYS[1]) == ARGV[1] then
              return redis.call('DEL', KEYS[1])
            end
            return 0
            """;

        // Only release the phone lock if this consumer still owns it; otherwise another worker
        // may have already advanced the conversation and we should not remove that lock.
        await _database.ScriptEvaluateAsync(script, [key], [expectedValue]).ConfigureAwait(false);
    }

    private sealed record WhatsAppQueueMessage(
        string Phone,
        string Body,
        string DedupeKey,
        string DeliveryId,
        string ReceivedAt,
        int Attempts)
    {
        public static WhatsAppQueueMessage FromStreamEntry(StreamEntry entry)
        {
            var values = entry.Values.ToDictionary(
                value => (string)value.Name!,
                value => (string)value.Value!,
                StringComparer.Ordinal);

            return new WhatsAppQueueMessage(
                Get(values, "phone"),
                Get(values, "body"),
                Get(values, "dedupeKey"),
                Get(values, "deliveryId"),
                Get(values, "receivedAt"),
                int.TryParse(Get(values, "attempts"), out var attempts) ? attempts : 0);
        }

        public NameValueEntry[] ToNameValueEntries(int attempts, string? lastError)
        {
            return
            [
                new NameValueEntry("phone", Phone),
                new NameValueEntry("body", Body),
                new NameValueEntry("dedupeKey", DedupeKey),
                new NameValueEntry("deliveryId", DeliveryId),
                new NameValueEntry("receivedAt", ReceivedAt),
                new NameValueEntry("attempts", attempts),
                new NameValueEntry("lastError", lastError ?? string.Empty),
                new NameValueEntry("lastFailedAt", DateTimeOffset.UtcNow.ToString("O")),
            ];
        }

        private static string Get(IReadOnlyDictionary<string, string> values, string key)
            => values.TryGetValue(key, out var value) ? value : string.Empty;
    }
}

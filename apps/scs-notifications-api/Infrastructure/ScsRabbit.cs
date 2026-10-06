using System.Threading.RateLimiting;
using RabbitMQ.Client;

namespace Notifications.Api.Infrastructure;

public static class ScsRabbit
{
    /// <summary>
    /// RabbitMQ.Client 7.x requires an explicit CreateChannelOptions.
    /// </summary>
    public static CreateChannelOptions CreateChannelOptions() => new(
        publisherConfirmationsEnabled: false,
        publisherConfirmationTrackingEnabled: false,
        outstandingPublisherConfirmationsRateLimiter: new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10_000,
            TokenLimit = 10_000,
            QueueLimit = 0,
        }),
        consumerDispatchConcurrency: null);
}

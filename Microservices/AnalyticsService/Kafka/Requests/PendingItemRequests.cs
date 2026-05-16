using System.Collections.Concurrent;
using Kafka.Messaging.Contracts.Response;

namespace AnalyticsService.Kafka.Requests;

public static class PendingItemRequests
{
    public static ConcurrentDictionary<Guid,
            TaskCompletionSource<GetItemByIdResponse>>
        Requests = new();
}
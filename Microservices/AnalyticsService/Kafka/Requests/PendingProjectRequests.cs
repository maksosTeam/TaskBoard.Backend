using System.Collections.Concurrent;
using Kafka.Messaging.Contracts.Response;

namespace AnalyticsService.Kafka.Requests;

public static class PendingProjectRequests
{
    public static ConcurrentDictionary<Guid,
            TaskCompletionSource<GetProjectByIdResponse>>
        Requests = new();
}
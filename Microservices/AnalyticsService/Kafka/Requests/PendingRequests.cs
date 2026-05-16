using System.Collections.Concurrent;
using Kafka.Messaging.Contracts.Response;

public static class PendingRequests
{
    public static ConcurrentDictionary<Guid, TaskCompletionSource<GetItemsByProjectResponse>> Requests = new();
}
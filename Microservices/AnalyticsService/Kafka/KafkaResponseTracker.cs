using System.Collections.Concurrent;

namespace AnalyticsService.Kafka
{
    public class KafkaResponseTracker<TResponse>
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<TResponse>> _requests = new();

        public Task<TResponse> WaitAsync(string correlationId, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<TResponse>();

            token.Register(() =>
            {
                _requests.TryRemove(correlationId, out _);
                tcs.TrySetCanceled();
            });

            _requests[correlationId] = tcs;
            return tcs.Task;
        }

        public void Resolve(string correlationId, TResponse response)
        {
            if (_requests.TryRemove(correlationId, out var tcs))
            {
                tcs.TrySetResult(response);
            }
        }
    }
}
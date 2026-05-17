namespace SharedLibrary.Models
{
    public class RpcMessage<T>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
        public T Payload { get; set; }
    }
}
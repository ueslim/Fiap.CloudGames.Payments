using EasyNetQ;
using EasyNetQ.Topology;
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.Core.Observability; // BusTracePropagation
using Serilog;
using Serilog.Context;

namespace FIAP.CloudGames.MessageBus
{
    public class MessageBus : IMessageBus, IDisposable
    {
        private IBus _bus;
        private readonly string _connectionString;

        public MessageBus(string connectionString)
        {
            _connectionString = connectionString;
            TryConnect();
        }

        public bool IsConnected => _bus?.Advanced?.IsConnected ?? false;
        public IAdvancedBus AdvancedBus => _bus?.Advanced;

        public void Publish<T>(T message) where T : IntegrationEvent
            => PublishAsync(message).GetAwaiter().GetResult();

        public async Task PublishAsync<T>(T message) where T : IntegrationEvent
        {
            TryConnect();

            // 1) Gera/obtém correlation_id atual (da requisição) — use seu helper ou gere um novo
            var correlationId = LogHelpers.GetCorrelationId() ?? Guid.NewGuid().ToString();

            // 2) Injeta correlation_id nos headers de mensagem
            var headers = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            BusTracePropagation.Inject(headers, correlationId);

            // 3) Cria a mensagem e copia headers
            var msg = new Message<T>(message);
            foreach (var kv in headers)
                msg.Properties.Headers[kv.Key] = kv.Value;

            // 4) Envelopa logs com o correlation_id
            using (LogContext.PushProperty("correlation_id", correlationId))
            {
                var conventions = _bus.Advanced.Conventions;
                var exchangeName = conventions.ExchangeNamingConvention(typeof(T));
                var topic = conventions.TopicNamingConvention(typeof(T));

                var exchange = await _bus.Advanced.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic);
                await _bus.Advanced.PublishAsync(exchange, topic, mandatory: false, message: msg);

                Log.Information(
                    "Bus publish event={eventType} exchange={exchange} topic={topic} aggregateId={aggId}",
                    typeof(T).Name, exchangeName, topic, message.AggregateId);
            }
        }

        public void Subscribe<T>(string subscriptionId, Action<T> onMessage) where T : class
            => SubscribeAsync<T>(subscriptionId, msg => { onMessage(msg); return Task.CompletedTask; });

        public void SubscribeAsync<T>(string subscriptionId, Func<T, Task> onMessage) where T : class
        {
            TryConnect();

            var conventions = _bus.Advanced.Conventions;
            var exchangeName = conventions.ExchangeNamingConvention(typeof(T));
            var topic = conventions.TopicNamingConvention(typeof(T));
            var queueName = conventions.QueueNamingConvention(typeof(T), subscriptionId);

            var exchange = _bus.Advanced.ExchangeDeclare(exchangeName, ExchangeType.Topic);
            var queue = _bus.Advanced.QueueDeclare(queueName);
            _bus.Advanced.Bind(exchange, queue, topic);

            _bus.Advanced.Consume<T>(queue, async (msg, info) =>
            {
                // 1) Extrai correlation_id do header (injetado pelo publisher)
                var correlationId = TryGetCorrelationId(msg.Properties.Headers) ?? Guid.NewGuid().ToString();

                // 2) Envelopa logs deste consumo com o mesmo correlation_id
                using (LogContext.PushProperty("correlation_id", correlationId))
                {
                    Log.Information("Bus consume event={eventType} queue={queue} topic={topic}", typeof(T).Name, queueName, topic);

                    await onMessage(msg.Body);
                }
            });
        }

        public TResponse Request<TRequest, TResponse>(TRequest request)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage
        {
            TryConnect();
            return _bus.Rpc.RequestAsync<TRequest, TResponse>(request).GetAwaiter().GetResult();
        }

        public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage
        {
            TryConnect();
            return await _bus.Rpc.RequestAsync<TRequest, TResponse>(request);
        }

        public IDisposable Respond<TRequest, TResponse>(Func<TRequest, TResponse> responder)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage
        {
            TryConnect();
            var registration = _bus.Rpc.RespondAsync<TRequest, TResponse>(req => Task.FromResult(responder(req)));
            return registration.GetAwaiter().GetResult();
        }

        public IDisposable RespondAsync<TRequest, TResponse>(Func<TRequest, Task<TResponse>> responder)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage
        {
            TryConnect();
            var registration = _bus.Rpc.RespondAsync(responder);
            return registration.GetAwaiter().GetResult();
        }

        private void TryConnect()
        {
            if (_bus != null && _bus.Advanced.IsConnected) return;
            _bus = RabbitHutch.CreateBus(_connectionString);
        }

        public void Dispose() => _bus?.Dispose();

        private static string? TryGetCorrelationId(IDictionary<string, object>? headers)
        {
            if (headers == null) return null;

            if (headers.TryGetValue(BusTracePropagation.CorrelationHeader, out var raw) && raw != null)
                return raw.ToString();

            foreach (var kv in headers)
                if (kv.Key.Equals(BusTracePropagation.CorrelationHeader, StringComparison.OrdinalIgnoreCase))
                    return kv.Value?.ToString();

            return null;
        }
    }
}
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.MessageBus;

namespace FIAP.CloudGames.Payment.API.Services
{
    public class FakeOrderIntegrationHandler : BackgroundService
    {
        private readonly IMessageBus _bus;
        private readonly ILogger<FakeOrderIntegrationHandler> _logger;

        public FakeOrderIntegrationHandler(IMessageBus bus, ILogger<FakeOrderIntegrationHandler> logger)
        {
            _bus = bus;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            const string subscriptionId = "audit-payment-authorized";

            _bus.SubscribeAsync<PaymentAuthorizedIntegrationEvent>(subscriptionId,
                async message =>
                {
                    _logger.LogInformation("PaymentAuthorized received. OrderId={OrderId}", message.OrderId);
                    await Task.CompletedTask;
                });

            return Task.CompletedTask;
        }
    }
}
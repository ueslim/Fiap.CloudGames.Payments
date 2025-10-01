using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.MessageBus;
using FIAP.CloudGames.Payment.Domain.Models;

namespace FIAP.CloudGames.Payment.API.Services
{
    public class PaymentIntegrationHandler : BackgroundService
    {
        private readonly IMessageBus _bus;
        private readonly IServiceProvider _serviceProvider;

        public PaymentIntegrationHandler(IServiceProvider serviceProvider, IMessageBus bus)
        {
            _serviceProvider = serviceProvider;
            _bus = bus;
        }

        private void SetResponder()
        {
        }

        private void SetSubscribers()
        {
            _bus.SubscribeAsync<OrderStartedIntegrationEvent>("OrderStartedIntegrationEvent", async message => { await AuthorizePayment(message); });
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            SetResponder();
            SetSubscribers();
            return Task.CompletedTask;
        }

        private async Task<ResponseMessage> AuthorizePayment(OrderStartedIntegrationEvent message)
        {
            using var scope = _serviceProvider.CreateScope();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            var payment = new Domain.Models.Payment
            {
                OrderId = message.OrderId,
                PaymentType = (PaymentType)message.PaymentType,
                Value = message.Value,
                CreditCard = new CreditCard(message.CardName, message.CardNumber, message.CardExpirationDate, message.CvvCard)
            };

            var response = await paymentService.AuthorizePayment(payment);

            return response;
        }
    }
}
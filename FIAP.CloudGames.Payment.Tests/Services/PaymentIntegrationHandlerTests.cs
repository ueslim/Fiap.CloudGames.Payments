using System;
using System.Threading;
using System.Threading.Tasks;
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.MessageBus;
using FIAP.CloudGames.Payment.API.Services;
using FIAP.CloudGames.Payment.Domain.Models;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FIAP.CloudGames.Payment.Tests.Services
{
    public class PaymentIntegrationHandlerTests
    {
        private static ServiceProvider ProviderWith(IPaymentService svc)
        {
            var services = new ServiceCollection();
            services.AddScoped(_ => svc);
            return services.BuildServiceProvider(validateScopes: true);
        }

        [Fact]
        public async Task ExecuteAsync_Should_Subscribe_To_OrderStartedIntegrationEvent()
        {
            // Arrange
            var paymentSvc = new Mock<IPaymentService>(MockBehavior.Strict);
            var provider = ProviderWith(paymentSvc.Object);

            var bus = new Mock<IMessageBus>(MockBehavior.Loose);

            // Verificaremos que a inscrição foi feita com o tópico/assinatura esperada
            bus.Setup(b => b.SubscribeAsync<OrderStartedIntegrationEvent>(
                    It.IsAny<string>(),
                    It.IsAny<Func<OrderStartedIntegrationEvent, Task>>()))
               .Verifiable();

            var sut = new PaymentIntegrationHandler(provider, bus.Object);

            // Act
            await sut.StartAsync(CancellationToken.None);
            await sut.StopAsync(CancellationToken.None);

            // Assert
            bus.Verify(b => b.SubscribeAsync<OrderStartedIntegrationEvent>(
                It.Is<string>(s => s == "OrderStartedIntegrationEvent"),
                It.IsAny<Func<OrderStartedIntegrationEvent, Task>>()),
                Times.Once);
        }

        [Fact]
        public async Task Subscribe_Handler_Should_Call_Service_AuthorizePayment_With_Mapped_Model()
        {
            // Arrange
            var paymentSvc = new Mock<IPaymentService>(MockBehavior.Strict);
            paymentSvc
                .Setup(s => s.AuthorizePayment(It.IsAny<FIAP.CloudGames.Payment.Domain.Models.Payment>()))
                .ReturnsAsync(new ResponseMessage(new ValidationResult()));

            var provider = ProviderWith(paymentSvc.Object);

            var bus = new Mock<IMessageBus>(MockBehavior.Loose);

            // Captura o delegate registrado na subscription para invocarmos manualmente
            Func<OrderStartedIntegrationEvent, Task> subscriber = null!;
            bus.Setup(b => b.SubscribeAsync<OrderStartedIntegrationEvent>(
                    It.IsAny<string>(),
                    It.IsAny<Func<OrderStartedIntegrationEvent, Task>>()))
               .Callback<string, Func<OrderStartedIntegrationEvent, Task>>((_, f) => subscriber = f);

            var sut = new PaymentIntegrationHandler(provider, bus.Object);
            await sut.StartAsync(CancellationToken.None);

            var evt = new OrderStartedIntegrationEvent
            {
                OrderId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                Value = 123.45m,
                PaymentType = (int)PaymentType.CreditCard,
                CardName = "John Doe",
                CardNumber = "4111111111111111",
                CardExpirationDate = "12/29",
                CvvCard = "123"
            };

            // Act
            await subscriber(evt);

            // Assert
            paymentSvc.Verify(s => s.AuthorizePayment(
                It.Is<FIAP.CloudGames.Payment.Domain.Models.Payment>(p =>
                    p.OrderId == evt.OrderId &&
                    p.Value == evt.Value &&
                    p.PaymentType == PaymentType.CreditCard &&
                    p.CreditCard.CardName == evt.CardName &&
                    p.CreditCard.CardNumber == evt.CardNumber &&
                    p.CreditCard.ExpirationDate == evt.CardExpirationDate &&
                    p.CreditCard.CVV == evt.CvvCard
                )), Times.Once);
        }
    }
}

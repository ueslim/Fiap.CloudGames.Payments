using FIAP.CloudGames.Core.Data;
using FIAP.CloudGames.Core.DomainObjects;
using FIAP.CloudGames.Core.Mediator;
using FIAP.CloudGames.Core.Messages;
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.MessageBus;
using FIAP.CloudGames.Payment.API.Facade;
using FIAP.CloudGames.Payment.API.Services;
using FIAP.CloudGames.Payment.Domain.Events;
using FIAP.CloudGames.Payment.Domain.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace FIAP.CloudGames.Payment.Tests.Services
{
    public class PaymentServiceTests
    {
        private static Payment.Domain.Models.Payment BuildPayment(decimal value = 100m) => new()
        {
            OrderId = Guid.NewGuid(),
            PaymentType = Payment.Domain.Models.PaymentType.CreditCard,
            Value = value,
            CreditCard = new Payment.Domain.Models.CreditCard("John Doe", "4111111111111111", "12/29", "123")
        };

        private static Payment.Domain.Models.Transaction Tx(TransactionStatus s, decimal total) => new()
        {
            Id = Guid.NewGuid(),
            Status = s,
            TotalValue = total,
            CardBrand = "MC",
            AuthorizationCode = "AUTH",
            TransactionCost = 1.23m,
            NSU = "NSU",
            TID = "TID"
        };

        private static Mock<IMediatorHandler> MediatorLoose()
        {
            var m = new Mock<IMediatorHandler>(MockBehavior.Loose);
            // Permitir publicação de qualquer evento de domínio
            m.Setup(x => x.PublishEvent(It.IsAny<Event>())).Returns(Task.CompletedTask);
            return m;
        }

        [Fact]
        public async Task AuthorizePayment_Should_Persist_And_PublishAuthorized_When_Gateway_Authorized()
        {
            var payment = BuildPayment(120m);

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            facade.Setup(f => f.AuthorizePayment(payment)).ReturnsAsync(Tx(TransactionStatus.Authorized, 120m));

            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
            repo.Setup(r => r.AddPayment(payment));
            var uow = new Mock<IUnitOfWork>(); uow.Setup(u => u.Commit()).ReturnsAsync(true);
            repo.SetupGet(r => r.UnitOfWork).Returns(uow.Object);

            var bus = new Mock<IMessageBus>(MockBehavior.Strict);
            // Deve publicar PaymentAuthorizedIntegrationEvent
            bus.Setup(b => b.PublishAsync(It.IsAny<PaymentAuthorizedIntegrationEvent>())).Returns(Task.CompletedTask).Verifiable();

            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var resp = await sut.AuthorizePayment(payment);

            resp.ValidationResult.IsValid.Should().BeTrue();
            repo.Verify(r => r.AddPayment(It.IsAny<Payment.Domain.Models.Payment>()), Times.Once);
            uow.Verify(u => u.Commit(), Times.Once);

            bus.Verify(b => b.PublishAsync(It.Is<PaymentAuthorizedIntegrationEvent>(e => e.OrderId == payment.OrderId)), Times.Once);
            // Mediator.PublishEvent(...) chamado via PublishAndClearAsync(payment) (ao menos 1 evento: PaymentCreatedEvent)
            mediator.Verify(m => m.PublishEvent(It.IsAny<Event>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task AuthorizePayment_Should_Return_Error_And_PublishRefused_When_Gateway_Refuses()
        {
            var payment = BuildPayment();

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            facade.Setup(f => f.AuthorizePayment(payment)).ReturnsAsync(Tx(TransactionStatus.Denied, payment.Value));

            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);

            var bus = new Mock<IMessageBus>(MockBehavior.Strict);
            bus.Setup(b => b.PublishAsync(It.IsAny<PaymentRefusedIntegrationEvent>())).Returns(Task.CompletedTask).Verifiable();

            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var resp = await sut.AuthorizePayment(payment);

            resp.ValidationResult.IsValid.Should().BeFalse();
            repo.Verify(r => r.AddPayment(It.IsAny<Payment.Domain.Models.Payment>()), Times.Never);
            // Publica refused por GatewayRefused
            bus.Verify(b => b.PublishAsync(It.Is<PaymentRefusedIntegrationEvent>(e =>
                e.OrderId == payment.OrderId && e.Reason == "GatewayRefused")), Times.Once);

            // Não deve publicar PaymentAuthorized
            bus.Verify(b => b.PublishAsync(It.IsAny<PaymentAuthorizedIntegrationEvent>()), Times.Never);
        }

        [Fact]
        public async Task AuthorizePayment_Should_Cancel_And_PublishRefused_When_Commit_Fails()
        {
            var payment = BuildPayment();

            var authorizedTx = Tx(TransactionStatus.Authorized, payment.Value);
            authorizedTx.PaymentId = Guid.NewGuid();

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            facade.Setup(f => f.AuthorizePayment(payment)).ReturnsAsync(authorizedTx);
            facade.Setup(f => f.CancelAuthorization(authorizedTx))
                  .ReturnsAsync(Tx(TransactionStatus.Canceled, payment.Value));

            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
            repo.Setup(r => r.AddPayment(payment));
            var uow = new Mock<IUnitOfWork>();
            uow.Setup(u => u.Commit()).ReturnsAsync(false); // falha intencional
            repo.SetupGet(r => r.UnitOfWork).Returns(uow.Object);

            var bus = new Mock<IMessageBus>(MockBehavior.Strict);
            bus.Setup(b => b.PublishAsync(It.IsAny<PaymentRefusedIntegrationEvent>())).Returns(Task.CompletedTask).Verifiable();

            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var resp = await sut.AuthorizePayment(payment);

            resp.ValidationResult.IsValid.Should().BeFalse();

            // CancelAuthorization foi chamado
            facade.Verify(f => f.CancelAuthorization(It.Is<Payment.Domain.Models.Transaction>(t => t.Status == TransactionStatus.Authorized)), Times.Once);

            // Publica refused por PersistenceFailed
            bus.Verify(b => b.PublishAsync(It.Is<PaymentRefusedIntegrationEvent>(e =>
                e.OrderId == payment.OrderId && e.Reason == "PersistenceFailed")), Times.Once);

            // Não deve publicar PaymentAuthorized
            bus.Verify(b => b.PublishAsync(It.IsAny<PaymentAuthorizedIntegrationEvent>()), Times.Never);
        }

        [Fact]
        public async Task CapturePayment_Should_Add_Transaction_When_Paid_Commit_And_Raise_Domain_Event()
        {
            var orderId = Guid.NewGuid();
            var authorized = Tx(TransactionStatus.Authorized, 100m);
            authorized.PaymentId = Guid.NewGuid();

            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
            repo.Setup(r => r.GetTransactionsByOrderId(orderId)).ReturnsAsync(new List<Transaction> { authorized });
            repo.Setup(r => r.AddTransaction(It.Is<Transaction>(t => t.Status == TransactionStatus.Paid)));
            var uow = new Mock<IUnitOfWork>(); uow.Setup(u => u.Commit()).ReturnsAsync(true);
            repo.SetupGet(r => r.UnitOfWork).Returns(uow.Object);

            // usado para recuperar o aggregate e adicionar evento
            var persistedPayment = new Payment.Domain.Models.Payment { OrderId = orderId, Value = 100m, };
            persistedPayment.AddEvent(new TransactionCapturedEvent(persistedPayment.Id, authorized.Id, 100m));
            repo.Setup(r => r.GetPaymentByOrderId(orderId)).ReturnsAsync(persistedPayment);

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            facade.Setup(f => f.CapturePayment(authorized)).ReturnsAsync(Tx(TransactionStatus.Paid, 100m));

            var bus = new Mock<IMessageBus>(MockBehavior.Loose); // não publica integração aqui
            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var resp = await sut.CapturePayment(orderId);

            resp.ValidationResult.IsValid.Should().BeTrue();
            repo.Verify(r => r.AddTransaction(It.Is<Transaction>(t => t.Status == TransactionStatus.Paid)), Times.Once);
            uow.Verify(u => u.Commit(), Times.Once);

            // Publica evento de domínio TransactionCapturedEvent
            mediator.Verify(m => m.PublishEvent(It.IsAny<Event>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CapturePayment_Should_Return_Error_When_Not_Paid()
        {
            var orderId = Guid.NewGuid();
            var authorized = Tx(TransactionStatus.Authorized, 50m);
            authorized.PaymentId = Guid.NewGuid();

            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
            repo.Setup(r => r.GetTransactionsByOrderId(orderId)).ReturnsAsync(new List<Transaction> { authorized });

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            facade.Setup(f => f.CapturePayment(authorized)).ReturnsAsync(Tx(TransactionStatus.Denied, 100m));

            var bus = new Mock<IMessageBus>(MockBehavior.Loose);
            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var resp = await sut.CapturePayment(orderId);

            resp.ValidationResult.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task CapturePayment_Should_Throw_When_Authorized_Not_Found()
        {
            var orderId = Guid.NewGuid();
            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
            repo.Setup(r => r.GetTransactionsByOrderId(orderId)).ReturnsAsync(new List<Transaction>());

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            var bus = new Mock<IMessageBus>(MockBehavior.Loose);
            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var act = async () => await sut.CapturePayment(orderId);
            await act.Should().ThrowAsync<DomainException>().WithMessage($"*{orderId}*");
        }

        [Fact]
        public async Task CancelPayment_Should_Add_Transaction_When_Canceled_Commit_And_Raise_Domain_Event()
        {
            var orderId = Guid.NewGuid();
            var authorized = Tx(TransactionStatus.Authorized, 50m);
            authorized.PaymentId = Guid.NewGuid();

            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
            repo.Setup(r => r.GetTransactionsByOrderId(orderId)).ReturnsAsync(new List<Transaction> { authorized });
            repo.Setup(r => r.AddTransaction(It.Is<Transaction>(t => t.Status == TransactionStatus.Canceled)));
            var uow = new Mock<IUnitOfWork>(); uow.Setup(u => u.Commit()).ReturnsAsync(true);
            repo.SetupGet(r => r.UnitOfWork).Returns(uow.Object);

            var persistedPayment = new Payment.Domain.Models.Payment { OrderId = orderId, Value = 50m, };
            persistedPayment.AddEvent(new TransactionCancelledEvent(persistedPayment.Id, authorized.Id, authorized.TotalValue));
            repo.Setup(r => r.GetPaymentByOrderId(orderId)).ReturnsAsync(persistedPayment);

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            facade.Setup(f => f.CancelAuthorization(authorized)).ReturnsAsync(Tx(TransactionStatus.Canceled, 50m));

            var bus = new Mock<IMessageBus>(MockBehavior.Loose);
            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var resp = await sut.CancelPayment(orderId);

            resp.ValidationResult.IsValid.Should().BeTrue();
            repo.Verify(r => r.AddTransaction(It.Is<Transaction>(t => t.Status == TransactionStatus.Canceled)), Times.Once);
            uow.Verify(u => u.Commit(), Times.Once);

            mediator.Verify(m => m.PublishEvent(It.IsAny<Event>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CancelPayment_Should_Throw_When_Authorized_Not_Found()
        {
            var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
            var orderId = Guid.NewGuid();
            repo.Setup(r => r.GetTransactionsByOrderId(orderId)).ReturnsAsync(Enumerable.Empty<Transaction>());

            var facade = new Mock<IPaymentFacade>(MockBehavior.Strict);
            var bus = new Mock<IMessageBus>(MockBehavior.Loose);
            var mediator = MediatorLoose();

            var sut = new PaymentService(facade.Object, repo.Object, bus.Object, mediator.Object);

            var act = async () => await sut.CancelPayment(orderId);
            await act.Should().ThrowAsync<DomainException>().WithMessage($"*{orderId}*");
        }
    }
}
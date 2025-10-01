using FIAP.CloudGames.Core.DomainObjects;
using FIAP.CloudGames.Core.Mediator;
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.MessageBus;
using FIAP.CloudGames.Payment.API.Facade;
using FIAP.CloudGames.Payment.Domain.Events;
using FIAP.CloudGames.Payment.Domain.Models;
using FIAP.CloudGames.Payment.Infra.Eventing;
using FluentValidation.Results;

namespace FIAP.CloudGames.Payment.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentFacade _paymentFacade;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IMessageBus _bus;
        private readonly IMediatorHandler _mediator;

        public PaymentService(IPaymentFacade paymentFacade,
                                IPaymentRepository paymentRepository,
                                IMessageBus bus,
                                IMediatorHandler mediator)
        {
            _paymentFacade = paymentFacade;
            _paymentRepository = paymentRepository;
            _bus = bus;
            _mediator = mediator;
        }

        public async Task<ResponseMessage> AuthorizePayment(Domain.Models.Payment payment)
        {
            payment.AddEvent(new PaymentCreatedEvent(payment.Id, payment.OrderId, payment.Value));

            var transaction = await _paymentFacade.AuthorizePayment(payment);

            var validationResult = new ValidationResult();

            if (transaction.Status != TransactionStatus.Authorized)
            {
                validationResult.Errors.Add(new ValidationFailure("Pagamento", "Pagamento recusado, entre em contato com a sua operadora de cartão"));

                await _bus.PublishAsync(new PaymentRefusedIntegrationEvent
                {
                    OrderId = payment.OrderId,
                    Reason = "GatewayRefused"
                });

                return new ResponseMessage(validationResult);
            }

            payment.AddTransaction(transaction);

            _paymentRepository.AddPayment(payment);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                validationResult.Errors.Add(new ValidationFailure("Pagamento", "Houve um erro ao realizar o pagamento."));

                await _bus.PublishAsync(new PaymentRefusedIntegrationEvent
                {
                    OrderId = payment.OrderId,
                    Reason = "PersistenceFailed"
                });

                await _paymentFacade.CancelAuthorization(transaction);

                return new ResponseMessage(validationResult);
            }

            await _mediator.PublishAndClearAsync(payment);

            await _bus.PublishAsync(new PaymentAuthorizedIntegrationEvent { OrderId = payment.OrderId });

            return new ResponseMessage(validationResult);
        }

        public async Task<ResponseMessage> CapturePayment(Guid orderId)
        {
            var transactions = await _paymentRepository.GetTransactionsByOrderId(orderId);
            var authorizedTransaction = transactions?.FirstOrDefault(t => t.Status == TransactionStatus.Authorized);
            var validationResult = new ValidationResult();

            if (authorizedTransaction == null) throw new DomainException($"Transação não encontrada para o pedido {orderId}");

            var transaction = await _paymentFacade.CapturePayment(authorizedTransaction);

            if (transaction.Status != TransactionStatus.Paid)
            {
                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível capturar o pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            transaction.PaymentId = authorizedTransaction.PaymentId;
            _paymentRepository.AddTransaction(transaction);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível persistir a captura do pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            var payment = await _paymentRepository.GetPaymentByOrderId(orderId);
            payment.AddEvent(new TransactionCapturedEvent(
                payment.Id,
                authorizedTransaction.Id,
                transaction.TotalValue));

            await _mediator.PublishAndClearAsync(payment);

            return new ResponseMessage(validationResult);
        }

        public async Task<ResponseMessage> CancelPayment(Guid orderId)
        {
            var transactions = await _paymentRepository.GetTransactionsByOrderId(orderId);
            var authorizedTransaction = transactions?.FirstOrDefault(t => t.Status == TransactionStatus.Authorized);
            var validationResult = new ValidationResult();

            if (authorizedTransaction == null) throw new DomainException($"Transação não encontrada para o pedido {orderId}");

            var transaction = await _paymentFacade.CancelAuthorization(authorizedTransaction);

            if (transaction.Status != TransactionStatus.Canceled)
            {
                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível cancelar o pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            transaction.PaymentId = authorizedTransaction.PaymentId;
            _paymentRepository.AddTransaction(transaction);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível persistir o cancelamento do pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            var payment = await _paymentRepository.GetPaymentByOrderId(orderId);
            payment.AddEvent(new TransactionCancelledEvent(
                payment.Id,
                authorizedTransaction.Id,
                authorizedTransaction.TotalValue));
            await _mediator.PublishAndClearAsync(payment);

            return new ResponseMessage(validationResult);
        }
    }
}
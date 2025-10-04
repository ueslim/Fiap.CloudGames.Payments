using FIAP.CloudGames.Core.DomainObjects;
using FIAP.CloudGames.Core.Mediator;
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.Core.Observability;
using FIAP.CloudGames.MessageBus;
using FIAP.CloudGames.Payment.API.Facade;
using FIAP.CloudGames.Payment.Domain.Events;
using FIAP.CloudGames.Payment.Domain.Models;
using FIAP.CloudGames.Payment.Infra.Eventing;
using FluentValidation.Results;
using Serilog;

namespace FIAP.CloudGames.Payment.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentFacade _paymentFacade;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IMediatorHandler _mediator;
        //private readonly IMessageBus _bus;

        public PaymentService(IPaymentFacade paymentFacade,
                                IPaymentRepository paymentRepository,
                                IMediatorHandler mediator)
        {
            _paymentFacade = paymentFacade;
            _paymentRepository = paymentRepository;
            _mediator = mediator;
        }

        public async Task<ResponseMessage> AuthorizePayment(Domain.Models.Payment payment)
        {
            var cid = LogHelpers.GetCorrelationId();

            Log.Information("Payment authorize start orderId={orderId} value={value} correlationId={cid}", payment.OrderId, payment.Value, cid);

            payment.AddEvent(new PaymentCreatedEvent(payment.Id, payment.OrderId, payment.Value));

            var transaction = await _paymentFacade.AuthorizePayment(payment);

            Log.Information("Payment authorize gateway status={status} orderId={orderId} txId={txId} correlationId={cid}", transaction.Status, payment.OrderId, transaction.Id, cid);

            var validationResult = new ValidationResult();

            if (transaction.Status != TransactionStatus.Authorized)
            {
                Log.Warning("Payment authorize refused orderId={orderId} correlationId={cid}", payment.OrderId, cid);

                validationResult.Errors.Add(new ValidationFailure("Pagamento", "Pagamento recusado, entre em contato com a sua operadora de cartão"));

                //Futuro
                //await _bus.PublishAsync(new PaymentRefusedIntegrationEvent
                //{
                //    OrderId = payment.OrderId,
                //    Reason = "GatewayRefused"
                //});

                return new ResponseMessage(validationResult);
            }

            payment.AddTransaction(transaction);

            _paymentRepository.AddPayment(payment);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                Log.Error("Payment authorize commit failed orderId={orderId} correlationId={cid}", payment.OrderId, cid);

                validationResult.Errors.Add(new ValidationFailure("Pagamento", "Houve um erro ao realizar o pagamento."));

                //Futuro
                //await _bus.PublishAsync(new PaymentRefusedIntegrationEvent
                //{
                //    OrderId = payment.OrderId,
                //    Reason = "PersistenceFailed"
                //});

                await _paymentFacade.CancelAuthorization(transaction);

                return new ResponseMessage(validationResult);
            }

            await _mediator.PublishAndClearAsync(payment);

            //await _bus.PublishAsync(new PaymentAuthorizedIntegrationEvent { OrderId = payment.OrderId });

            Log.Information("Payment authorize success orderId={orderId} paymentId={paymentId} correlationId={cid}", payment.OrderId, payment.Id, cid);

            return new ResponseMessage(validationResult);
        }

        public async Task<ResponseMessage> CapturePayment(Guid orderId)
        {
            var cid = LogHelpers.GetCorrelationId();
            Log.Information("Payment capture start orderId={orderId} correlationId={cid}", orderId, cid);

            var transactions = await _paymentRepository.GetTransactionsByOrderId(orderId);
            var authorizedTransaction = transactions?.FirstOrDefault(t => t.Status == TransactionStatus.Authorized);
            var validationResult = new ValidationResult();

            if (authorizedTransaction == null) throw new DomainException($"Transação não encontrada para o pedido {orderId}");

            var transaction = await _paymentFacade.CapturePayment(authorizedTransaction);

            Log.Information("Payment capture gateway status={status} orderId={orderId} txId={txId} correlationId={cid}", transaction.Status, orderId, authorizedTransaction.Id, cid);

            if (transaction.Status != TransactionStatus.Paid)
            {
                Log.Warning("Payment capture not paid orderId={orderId} correlationId={cid}", orderId, cid);

                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível capturar o pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            transaction.PaymentId = authorizedTransaction.PaymentId;
            _paymentRepository.AddTransaction(transaction);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                Log.Error("Payment capture commit failed orderId={orderId} correlationId={cid}", orderId, cid);

                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível persistir a captura do pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            var payment = await _paymentRepository.GetPaymentByOrderId(orderId);

            payment.AddEvent(new TransactionCapturedEvent(payment.Id, authorizedTransaction.Id, transaction.TotalValue));

            await _mediator.PublishAndClearAsync(payment);

            Log.Information("Payment capture success orderId={orderId} paymentId={paymentId} txId={txId} correlationId={cid}", orderId, payment.Id, authorizedTransaction.Id, cid);

            return new ResponseMessage(validationResult);
        }

        public async Task<ResponseMessage> CancelPayment(Guid orderId)
        {
            var cid = LogHelpers.GetCorrelationId();
            Log.Information("Payment cancel start orderId={orderId} correlationId={cid}", orderId, cid);

            var transactions = await _paymentRepository.GetTransactionsByOrderId(orderId);
            var authorizedTransaction = transactions?.FirstOrDefault(t => t.Status == TransactionStatus.Authorized);
            var validationResult = new ValidationResult();

            if (authorizedTransaction == null) throw new DomainException($"Transação não encontrada para o pedido {orderId}");

            var transaction = await _paymentFacade.CancelAuthorization(authorizedTransaction);

            Log.Information("Payment cancel gateway status={status} orderId={orderId} txId={txId} correlationId={cid}", transaction.Status, orderId, authorizedTransaction.Id, cid);

            if (transaction.Status != TransactionStatus.Canceled)
            {
                Log.Warning("Payment cancel failed orderId={orderId} correlationId={cid}", orderId, cid);

                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível cancelar o pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            transaction.PaymentId = authorizedTransaction.PaymentId;
            _paymentRepository.AddTransaction(transaction);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                Log.Error("Payment cancel commit failed orderId={orderId} correlationId={cid}", orderId, cid);

                validationResult.Errors.Add(new ValidationFailure("Pagamento", $"Não foi possível persistir o cancelamento do pagamento do pedido {orderId}"));

                return new ResponseMessage(validationResult);
            }

            var payment = await _paymentRepository.GetPaymentByOrderId(orderId);
            payment.AddEvent(new TransactionCancelledEvent(payment.Id, authorizedTransaction.Id, authorizedTransaction.TotalValue));

            await _mediator.PublishAndClearAsync(payment);

            Log.Information("Payment cancel success orderId={orderId} paymentId={paymentId} txId={txId} correlationId={cid}", orderId, payment.Id, authorizedTransaction.Id, cid);

            return new ResponseMessage(validationResult);
        }
    }
}
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.MessageBus;
using FIAP.CloudGames.Payment.API.Facade;
using FIAP.CloudGames.Payment.Domain.Models;
using FluentValidation.Results;

namespace FIAP.CloudGames.Payment.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentFacade _paymentFacade;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IMessageBus _bus;

        public PaymentService(IPaymentFacade paymentFacade,
                                IPaymentRepository paymentRepository,
                                IMessageBus bus)
        {
            _paymentFacade = paymentFacade;
            _paymentRepository = paymentRepository;
            _bus = bus;
        }

        public async Task<ResponseMessage> AuthorizePayment(Domain.Models.Payment payment)
        {
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

            await _bus.PublishAsync(new PaymentAuthorizedIntegrationEvent { OrderId = payment.OrderId });

            return new ResponseMessage(validationResult);
        }
    }
}
using FIAP.CloudGames.Core.Messages.Integration;
using FIAP.CloudGames.MessageBus;
using FIAP.CloudGames.Payment.API.Services;
using FIAP.CloudGames.Payment.API.Utils;
using FIAP.CloudGames.Payment.Domain.Models;
using FIAP.CloudGames.WebAPI.Core.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CloudGames.Payment.API.Controllers
{
    [Authorize]
    public class PaymentController : MainController
    {
        // Não haverá endpoints pois a comunicação com Payment será feita através de integration event messages
        // Implementado endpoints get somente para testes de banco

        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentService _paymentService;
        private readonly IMessageBus _bus;

        // ENDPOINTS DE TESTS SOMENTE
        public PaymentController(IPaymentRepository paymentRepository, IPaymentService paymentService, IMessageBus bus)
        {
            _paymentRepository = paymentRepository;
            _paymentService = paymentService;
            _bus = bus;
        }

        [AllowAnonymous]
        [HttpGet("payments")]
        public async Task<IEnumerable<PaymentDto>> Index()
        {
            var result = await _paymentRepository.GetAll();
            return result.Select(PaymentTestDataGenerator.MapPayment).ToList();
        }

        [AllowAnonymous]
        [HttpGet("ThrowFakeOrderProcessingStartedIntegrationEvent")]
        public IActionResult ThrowOrderProcessingStartedIntegrationEvent()
        {
            var payment = PaymentTestDataGenerator.GenerateRandomPayment();

            var msg = new OrderStartedIntegrationEvent
            {
                OrderId = payment.OrderId,
                PaymentType = 1,
                Value = payment.Value,
                CardName = payment.CreditCard.CardName,
                CardNumber = payment.CreditCard.CardNumber,
                CardExpirationDate = payment.CreditCard.ExpirationDate,
                CvvCard = payment.CreditCard.CVV
            };

            _bus.Publish(msg); // fire-and-forget

            return Accepted(new
            {
                TrackingId = payment.OrderId,
                Payment = PaymentTestDataGenerator.MapPayment(payment)
            });
        }

        [AllowAnonymous]
        [HttpPost("payments/{orderId:guid}/capture")]
        public async Task<IActionResult> Capture(Guid orderId)
        {
            var resp = await _paymentService.CapturePayment(orderId);
            return CustomResponse(resp);
        }

        [AllowAnonymous]
        [HttpPost("payments/{orderId:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid orderId)
        {
            var resp = await _paymentService.CancelPayment(orderId);
            return CustomResponse(resp);
        }
    }
}
using FIAP.CloudGames.Payment.Infra.Data.Repository.EventSourcing;
using FIAP.CloudGames.Payment.Infra.Eventing;
using FIAP.CloudGames.WebAPI.Core.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CloudGames.Payment.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("eventstore")]
    [Produces("application/json")]
    public class EventStoreController : MainController
    {
        private readonly IEventStoreRepository _eventStoreRepository;

        public EventStoreController(IEventStoreRepository eventStoreRepository)
        {
            _eventStoreRepository = eventStoreRepository;
        }

        /// <summary>
        /// Retorna os eventos crus para um aggregate (PaymentId), em ordem cronológica.
        /// </summary>
        [HttpGet("{paymentId:guid}")]
        public async Task<IActionResult> GetByAggregateId(Guid paymentId)
        {
            var events = await _eventStoreRepository.All(paymentId);
            if (events == null || events.Count == 0)
                return NotFound($"Nenhum evento encontrado para PaymentId {paymentId}");

            var result = events
                .OrderBy(e => e.Timestamp)
                .Select(e => new
                {
                    e.Id,
                    e.AggregateId,
                    e.MessageType,
                    e.Data,
                    e.User,
                    e.Timestamp
                });

            return Ok(result);
        }

        /// <summary>
        /// Reconstitui o estado final do aggregate (snapshot) a partir dos eventos.
        /// </summary>
        [HttpGet("{paymentId:guid}/replay")]
        public async Task<IActionResult> Replay(Guid paymentId)
        {
            var events = await _eventStoreRepository.All(paymentId);
            if (events == null || events.Count == 0)
                return NotFound($"Nenhum evento encontrado para PaymentId {paymentId}");

            var payment = PaymentRehydrator.Rehydrate(events);

            return Ok(new
            {
                PaymentId = payment.Id,
                payment.OrderId,
                payment.Value,
                Transactions = payment.Transactions.Select(t => new
                {
                    t.Id,
                    t.TotalValue,
                    Status = t.Status.ToString()
                })
            });
        }

        /// <summary>
        /// Retorna a lista de snapshots após cada evento aplicado (todas as mudanças).
        /// </summary>
        [HttpGet("{paymentId:guid}/replay/steps")]
        public async Task<IActionResult> ReplaySteps(Guid paymentId)
        {
            var events = await _eventStoreRepository.All(paymentId);
            if (events == null || events.Count == 0)
                return NotFound($"Nenhum evento encontrado para PaymentId {paymentId}");

            var steps = PaymentRehydrator.ReplaySteps(events);
            return Ok(steps.OrderBy(s => s.At));
        }

        /// <summary>
        /// Retorna a timeline (metadados) de todas as mudanças (eventos).
        /// </summary>
        [HttpGet("{paymentId:guid}/timeline")]
        public async Task<IActionResult> Timeline(Guid paymentId)
        {
            var events = await _eventStoreRepository.All(paymentId);
            if (events == null || events.Count == 0)
                return NotFound($"Nenhum evento encontrado para PaymentId {paymentId}");

            var items = PaymentRehydrator.BuildTimeline(events);
            return Ok(items.OrderBy(i => i.At));
        }
    }
}
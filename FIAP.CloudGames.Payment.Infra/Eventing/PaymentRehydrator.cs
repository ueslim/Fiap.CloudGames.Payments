using FIAP.CloudGames.Core.Events;
using FIAP.CloudGames.Payment.Domain.Events;
using FIAP.CloudGames.Payment.Domain.Models;
using Newtonsoft.Json;

namespace FIAP.CloudGames.Payment.Infra.Eventing
{
    /// <summary>
    /// Utilitário de Reidratação (Event Sourcing) para Payment.
    /// - Rehydrate: aplica todos os eventos e retorna o estado final (snapshot).
    /// - ReplaySteps: retorna snapshots após cada evento (linha do tempo de estados).
    /// - BuildTimeline: retorna apenas os metadados de cada mudança (eventos).
    /// </summary>
    public static class PaymentRehydrator
    {
        public static Domain.Models.Payment Rehydrate(IEnumerable<StoredEvent> storedEvents)
        {
            var payment = new Domain.Models.Payment();

            foreach (var e in storedEvents.OrderBy(ev => ev.Timestamp))
            {
                switch (e.MessageType)
                {
                    case nameof(PaymentCreatedEvent):
                        {
                            var created = JsonConvert.DeserializeObject<PaymentCreatedEvent>(e.Data);
                            if (created != null)
                            {
                                payment.Id = created.PaymentId;
                                payment.OrderId = created.OrderId;
                                payment.Value = created.Value;
                            }
                            break;
                        }
                    case nameof(TransactionAddedEvent):
                        {
                            var tx = JsonConvert.DeserializeObject<TransactionAddedEvent>(e.Data);
                            if (tx != null)
                            {
                                payment.Transactions.Add(new Transaction
                                {
                                    Id = tx.TransactionId,
                                    TotalValue = tx.TotalValue,
                                    Status = (TransactionStatus)tx.Status,
                                    PaymentId = tx.PaymentId
                                });
                            }
                            break;
                        }
                    case nameof(TransactionCapturedEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionCapturedEvent>(e.Data);
                            if (ev != null)
                            {
                                var tx = payment.Transactions.FirstOrDefault(t => t.Id == ev.TransactionId);
                                if (tx != null)
                                {
                                    tx.Status = TransactionStatus.Paid;
                                    tx.TotalValue = ev.Value;
                                }
                                else
                                {
                                    // tolerância: se não existir, cria entrada
                                    payment.Transactions.Add(new Transaction
                                    {
                                        Id = ev.TransactionId,
                                        TotalValue = ev.Value,
                                        Status = TransactionStatus.Paid,
                                        PaymentId = ev.PaymentId
                                    });
                                }
                            }
                            break;
                        }
                    case nameof(TransactionCancelledEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionCancelledEvent>(e.Data);
                            if (ev != null)
                            {
                                var tx = payment.Transactions.FirstOrDefault(t => t.Id == ev.TransactionId);
                                if (tx != null)
                                {
                                    tx.Status = TransactionStatus.Canceled;
                                }
                                else
                                {
                                    // tolerância: se não existir, cria entrada
                                    payment.Transactions.Add(new Transaction
                                    {
                                        Id = ev.TransactionId,
                                        TotalValue = ev.Value,
                                        Status = TransactionStatus.Canceled,
                                        PaymentId = ev.PaymentId
                                    });
                                }
                            }
                            break;
                        }
                }
            }

            return payment;
        }

        public static List<PaymentReplayStep> ReplaySteps(IEnumerable<StoredEvent> storedEvents)
        {
            var payment = new Domain.Models.Payment();
            var steps = new List<PaymentReplayStep>();

            foreach (var e in storedEvents.OrderBy(ev => ev.Timestamp))
            {
                switch (e.MessageType)
                {
                    case nameof(PaymentCreatedEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<PaymentCreatedEvent>(e.Data);
                            if (ev != null)
                            {
                                payment.Id = ev.PaymentId;
                                payment.OrderId = ev.OrderId;
                                payment.Value = ev.Value;
                            }
                            break;
                        }
                    case nameof(TransactionAddedEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionAddedEvent>(e.Data);
                            if (ev != null)
                            {
                                payment.Transactions.Add(new Transaction
                                {
                                    Id = ev.TransactionId,
                                    TotalValue = ev.TotalValue,
                                    Status = (TransactionStatus)ev.Status,
                                    PaymentId = ev.PaymentId
                                });
                            }
                            break;
                        }
                    case nameof(TransactionCapturedEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionCapturedEvent>(e.Data);
                            if (ev != null)
                            {
                                var tx = payment.Transactions.FirstOrDefault(t => t.Id == ev.TransactionId);
                                if (tx != null)
                                {
                                    tx.Status = TransactionStatus.Paid;
                                    tx.TotalValue = ev.Value;
                                }
                                else
                                {
                                    payment.Transactions.Add(new Transaction
                                    {
                                        Id = ev.TransactionId,
                                        TotalValue = ev.Value,
                                        Status = TransactionStatus.Paid,
                                        PaymentId = ev.PaymentId
                                    });
                                }
                            }
                            break;
                        }
                    case nameof(TransactionCancelledEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionCancelledEvent>(e.Data);
                            if (ev != null)
                            {
                                var tx = payment.Transactions.FirstOrDefault(t => t.Id == ev.TransactionId);
                                if (tx != null)
                                {
                                    tx.Status = TransactionStatus.Canceled;
                                }
                                else
                                {
                                    payment.Transactions.Add(new Transaction
                                    {
                                        Id = ev.TransactionId,
                                        TotalValue = ev.Value,
                                        Status = TransactionStatus.Canceled,
                                        PaymentId = ev.PaymentId
                                    });
                                }
                            }
                            break;
                        }
                }

                // snapshot imutável do estado após aplicar o evento
                steps.Add(new PaymentReplayStep(
                    At: e.Timestamp,
                    Event: e.MessageType,
                    State: new PaymentSnapshot(
                        payment.Id,
                        payment.OrderId,
                        payment.Value,
                        payment.Transactions
                            .Select(t => new TransactionSnapshot(t.Id, t.TotalValue, t.Status.ToString()))
                            .ToList()
                    )
                ));
            }

            return steps;
        }

        public static List<PaymentTimelineItem> BuildTimeline(IEnumerable<StoredEvent> storedEvents)
        {
            var timeline = new List<PaymentTimelineItem>();

            foreach (var e in storedEvents.OrderBy(ev => ev.Timestamp))
            {
                switch (e.MessageType)
                {
                    case nameof(PaymentCreatedEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<PaymentCreatedEvent>(e.Data);
                            if (ev != null)
                            {
                                timeline.Add(new PaymentTimelineItem(
                                    At: e.Timestamp,
                                    Event: "PaymentCreated",
                                    PaymentId: ev.PaymentId,
                                    TransactionId: null,
                                    StatusAfter: null,
                                    Value: ev.Value
                                ));
                            }
                            break;
                        }
                    case nameof(TransactionAddedEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionAddedEvent>(e.Data);
                            if (ev != null)
                            {
                                timeline.Add(new PaymentTimelineItem(
                                    At: e.Timestamp,
                                    Event: "TransactionAdded",
                                    PaymentId: ev.PaymentId,
                                    TransactionId: ev.TransactionId,
                                    StatusAfter: ((TransactionStatus)ev.Status).ToString(),
                                    Value: ev.TotalValue
                                ));
                            }
                            break;
                        }
                    case nameof(TransactionCapturedEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionCapturedEvent>(e.Data);
                            if (ev != null)
                            {
                                timeline.Add(new PaymentTimelineItem(
                                    At: e.Timestamp,
                                    Event: "TransactionCaptured",
                                    PaymentId: ev.PaymentId,
                                    TransactionId: ev.TransactionId,
                                    StatusAfter: TransactionStatus.Paid.ToString(),
                                    Value: ev.Value
                                ));
                            }
                            break;
                        }
                    case nameof(TransactionCancelledEvent):
                        {
                            var ev = JsonConvert.DeserializeObject<TransactionCancelledEvent>(e.Data);
                            if (ev != null)
                            {
                                timeline.Add(new PaymentTimelineItem(
                                    At: e.Timestamp,
                                    Event: "TransactionCancelled",
                                    PaymentId: ev.PaymentId,
                                    TransactionId: ev.TransactionId,
                                    StatusAfter: TransactionStatus.Canceled.ToString(),
                                    Value: ev.Value
                                ));
                            }
                            break;
                        }
                }
            }

            return timeline;
        }

        public record TransactionSnapshot(Guid Id, decimal TotalValue, string Status);
        public record PaymentSnapshot(Guid PaymentId, Guid OrderId, decimal Value, List<TransactionSnapshot> Transactions);
        public record PaymentReplayStep(DateTime At, string Event, PaymentSnapshot State);

        public record PaymentTimelineItem(
            DateTime At,
            string Event,
            Guid PaymentId,
            Guid? TransactionId,
            string? StatusAfter,
            decimal? Value
        );
    }
}
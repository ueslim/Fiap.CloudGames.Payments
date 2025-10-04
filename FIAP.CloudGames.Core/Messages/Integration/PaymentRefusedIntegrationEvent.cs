namespace FIAP.CloudGames.Core.Messages.Integration
{
    public class PaymentRefusedIntegrationEvent : IntegrationEvent
    {
        public Guid CustomerId { get; set; }
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
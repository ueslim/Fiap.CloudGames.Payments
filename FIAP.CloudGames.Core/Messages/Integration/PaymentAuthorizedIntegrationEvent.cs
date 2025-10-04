namespace FIAP.CloudGames.Core.Messages.Integration
{
    public class PaymentAuthorizedIntegrationEvent : IntegrationEvent
    {
        public Guid OrderId { get; set; }
    }
}
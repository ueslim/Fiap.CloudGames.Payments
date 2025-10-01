using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIAP.CloudGames.Payment.Infra.Data.Mappings
{
    public class TransacaoMapping : IEntityTypeConfiguration<Domain.Models.Transaction>
    {
        public void Configure(EntityTypeBuilder<Domain.Models.Transaction> builder)
        {
            builder.HasKey(c => c.Id);

            // 1 : N => Pagamento : Transacao
            builder.HasOne(c => c.Payment)
                .WithMany(c => c.Transactions);

            builder.ToTable("Transactions");
        }
    }
}
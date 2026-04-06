using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("exchange_rates");

        builder.HasKey(er => er.Id);
        builder.Property(er => er.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(er => er.FromCurrency).HasColumnName("from_currency").HasMaxLength(3).IsRequired();
        builder.Property(er => er.ToCurrency).HasColumnName("to_currency").HasMaxLength(3).IsRequired();
        builder.Property(er => er.Rate).HasColumnName("rate").HasColumnType("decimal(18,8)").IsRequired();
        builder.Property(er => er.FetchedAt).HasColumnName("fetched_at").HasColumnType("datetime(6)").IsRequired();

        builder.HasIndex(er => new { er.FromCurrency, er.ToCurrency }).IsUnique().HasDatabaseName("uk_pair");
        builder.HasIndex(er => er.FetchedAt).IsDescending(true).HasDatabaseName("idx_fetched");
    }
}

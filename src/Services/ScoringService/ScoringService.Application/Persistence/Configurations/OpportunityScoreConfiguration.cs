using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScoringService.Domain.Entities;

namespace ScoringService.Application.Persistence.Configurations;

public class OpportunityScoreConfiguration : IEntityTypeConfiguration<OpportunityScore>
{
    public void Configure(EntityTypeBuilder<OpportunityScore> builder)
    {
        builder.ToTable("opportunity_scores");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(p => p.MatchId).HasColumnName("match_id").HasColumnType("char(36)").IsRequired();
        builder.Property(p => p.ProfitMarginPct).HasColumnName("profit_margin_pct").HasColumnType("decimal(8,4)");
        builder.Property(p => p.DemandScore).HasColumnName("demand_score").HasColumnType("decimal(5,2)");
        builder.Property(p => p.CompetitionScore).HasColumnName("competition_score").HasColumnType("decimal(5,2)");
        builder.Property(p => p.PriceStabilityScore).HasColumnName("price_stability_score").HasColumnType("decimal(5,2)");
        builder.Property(p => p.MatchConfidenceScore).HasColumnName("match_confidence_score").HasColumnType("decimal(5,2)");
        builder.Property(p => p.CompositeScore).HasColumnName("composite_score").HasColumnType("decimal(5,2)");
        builder.Property(p => p.LandedCostVnd).HasColumnName("landed_cost_vnd").HasColumnType("decimal(18,4)");
        builder.Property(p => p.VietnamRetailVnd).HasColumnName("vietnam_retail_vnd").HasColumnType("decimal(18,4)");
        builder.Property(p => p.PriceDifferenceVnd).HasColumnName("price_difference_vnd").HasColumnType("decimal(18,4)");
        builder.Property(p => p.CalculatedAt).HasColumnName("calculated_at").HasColumnType("datetime(6)");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");

        builder.HasIndex(p => p.MatchId).IsUnique().HasDatabaseName("uk_match_id");
        builder.HasIndex(p => p.CompositeScore).HasDatabaseName("idx_composite_desc").IsDescending(false);
    }
}

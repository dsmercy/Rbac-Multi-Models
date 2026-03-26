using AuditLogging.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditLogging.Infrastructure.Persistence.Configurations;

public sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", "audit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.LogType).IsRequired();
        builder.Property(a => a.CorrelationId).IsRequired();
        builder.Property(a => a.ActorUserId).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(200).IsRequired();
        builder.Property(a => a.ResourceId).IsRequired(false);
        builder.Property(a => a.ScopeId).IsRequired(false);
        builder.Property(a => a.IsGranted).IsRequired(false);
        builder.Property(a => a.DenialReason).HasMaxLength(100).IsRequired(false);
        builder.Property(a => a.CacheHit).IsRequired(false);
        builder.Property(a => a.EvaluationLatencyMs).IsRequired(false);
        builder.Property(a => a.PolicyId).HasMaxLength(100).IsRequired(false);
        builder.Property(a => a.DelegationChain).HasMaxLength(500).IsRequired(false);
        builder.Property(a => a.TargetEntityType).HasMaxLength(100).IsRequired(false);
        builder.Property(a => a.TargetEntityId).IsRequired(false);
        builder.Property(a => a.OldValue).HasColumnType("jsonb").IsRequired(false);
        builder.Property(a => a.NewValue).HasColumnType("jsonb").IsRequired(false);
        builder.Property(a => a.IsPlatformAction).HasDefaultValue(false).IsRequired();
        builder.Property(a => a.Timestamp).IsRequired();

        // Primary time-range query: tenant + date range (most common audit log query pattern)
        builder.HasIndex(a => new { a.TenantId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_TenantId_Timestamp");

        // Filter by actor user within a tenant
        builder.HasIndex(a => new { a.TenantId, a.ActorUserId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_TenantId_ActorUserId_Timestamp");

        // Filter by resource within a tenant
        builder.HasIndex(a => new { a.TenantId, a.ResourceId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_TenantId_ResourceId_Timestamp");

        // Correlation ID lookup for distributed tracing
        builder.HasIndex(a => a.CorrelationId)
            .HasDatabaseName("IX_AuditLogs_CorrelationId");
    }
}

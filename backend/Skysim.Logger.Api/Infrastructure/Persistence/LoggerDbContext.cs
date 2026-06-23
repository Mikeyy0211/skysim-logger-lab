using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Domain.Entities;

namespace Skysim.Logger.Api.Infrastructure.Persistence;

public class LoggerDbContext : DbContext
{
    public LoggerDbContext(DbContextOptions<LoggerDbContext> options) : base(options)
    {
    }

    public DbSet<LogFlow> LogFlows => Set<LogFlow>();
    public DbSet<LogAction> LogActions => Set<LogAction>();
    public DbSet<LogActionDetail> LogActionDetails => Set<LogActionDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // log_flows
        modelBuilder.Entity<LogFlow>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(e => e.FlowId)
                .IsUnique()
                .HasDatabaseName("idx_log_flows_flow_id");

            entity.HasIndex(e => e.CustomerEmail)
                .HasDatabaseName("idx_log_flows_customer_email");

            entity.HasIndex(e => e.CustomerPhone)
                .HasDatabaseName("idx_log_flows_customer_phone");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_log_flows_user_id");

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("idx_log_flows_order_id");

            entity.HasIndex(e => e.PaymentId)
                .HasDatabaseName("idx_log_flows_payment_id");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_log_flows_status");

            entity.HasIndex(e => e.FlowType)
                .HasDatabaseName("idx_log_flows_flow_type");

            entity.HasIndex(e => e.CheckoutType)
                .HasDatabaseName("idx_log_flows_checkout_type");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_log_flows_created_at");

            entity.HasIndex(e => e.CompletedAt)
                .HasDatabaseName("idx_log_flows_completed_at");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()");

            entity.HasMany(e => e.Actions)
                .WithOne(a => a.Flow)
                .HasForeignKey(a => a.FlowId)
                .HasPrincipalKey(f => f.FlowId);
        });

        // log_actions
        modelBuilder.Entity<LogAction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(e => e.EventId)
                .IsUnique()
                .HasDatabaseName("idx_log_actions_event_id");

            entity.HasIndex(e => e.FlowId)
                .HasDatabaseName("idx_log_actions_flow_id");

            entity.HasIndex(e => e.ServiceName)
                .HasDatabaseName("idx_log_actions_service_name");

            entity.HasIndex(e => e.ActionType)
                .HasDatabaseName("idx_log_actions_action_type");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_log_actions_status");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_log_actions_created_at");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()");

            entity.HasOne(e => e.Detail)
                .WithOne(d => d.Action)
                .HasForeignKey<LogActionDetail>(d => d.ActionId)
                .HasPrincipalKey<LogAction>(a => a.Id);
        });

        // log_action_details
        modelBuilder.Entity<LogActionDetail>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(e => e.ActionId)
                .IsUnique()
                .HasDatabaseName("idx_log_action_details_action_id");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()");
        });
    }
}

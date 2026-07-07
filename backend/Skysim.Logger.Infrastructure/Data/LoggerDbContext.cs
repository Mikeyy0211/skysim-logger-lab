using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Infrastructure.Data;

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

        modelBuilder.Entity<LogFlow>(entity =>
        {
            entity.ToTable("log_flows");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FlowId).HasColumnName("flow_id");
            entity.Property(e => e.FlowType).HasColumnName("flow_type");
            entity.Property(e => e.CheckoutType).HasColumnName("checkout_type");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CustomerEmail).HasColumnName("customer_email");
            entity.Property(e => e.CustomerPhone).HasColumnName("customer_phone");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.UserEmail).HasColumnName("user_email");
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.PartnerId).HasColumnName("partner_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.OrderCode).HasColumnName("order_code");
            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
            entity.Property(e => e.TotalSteps).HasColumnName("total_steps");
            entity.Property(e => e.SuccessSteps).HasColumnName("success_steps");
            entity.Property(e => e.FailedSteps).HasColumnName("failed_steps");
            entity.Property(e => e.LastActionType).HasColumnName("last_action_type");
            entity.Property(e => e.LastMessage).HasColumnName("last_message");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.FlowId).IsUnique().HasDatabaseName("idx_log_flows_flow_id");
            entity.HasIndex(e => e.CustomerEmail).HasDatabaseName("idx_log_flows_customer_email");
            entity.HasIndex(e => e.CustomerPhone).HasDatabaseName("idx_log_flows_customer_phone");
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_log_flows_user_id");
            entity.HasIndex(e => e.UserEmail).HasDatabaseName("idx_log_flows_user_email");
            entity.HasIndex(e => e.Username).HasDatabaseName("idx_log_flows_username");
            entity.HasIndex(e => e.PartnerId).HasDatabaseName("idx_log_flows_partner_id");
            entity.HasIndex(e => e.OrderCode).HasDatabaseName("idx_log_flows_order_code");
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_log_flows_order_id");
            entity.HasIndex(e => e.PaymentId).HasDatabaseName("idx_log_flows_payment_id");
            entity.HasIndex(e => e.TransactionId).HasDatabaseName("idx_log_flows_transaction_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_log_flows_status");
            entity.HasIndex(e => e.FlowType).HasDatabaseName("idx_log_flows_flow_type");
            entity.HasIndex(e => e.CheckoutType).HasDatabaseName("idx_log_flows_checkout_type");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_log_flows_created_at");
            entity.HasIndex(e => e.CompletedAt).HasDatabaseName("idx_log_flows_completed_at");

            entity.HasMany(e => e.Actions)
                .WithOne(a => a.Flow)
                .HasForeignKey(a => a.FlowId)
                .HasPrincipalKey(f => f.FlowId);
        });

        modelBuilder.Entity<LogAction>(entity =>
        {
            entity.ToTable("log_actions");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.FlowId).HasColumnName("flow_id");
            entity.Property(e => e.StepOrder).HasColumnName("step_order");
            entity.Property(e => e.ServiceName).HasColumnName("service_name");
            entity.Property(e => e.ActionType).HasColumnName("action_type");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.ErrorCode).HasColumnName("error_code");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.RequestTime).HasColumnName("request_time");
            entity.Property(e => e.ResponseTime).HasColumnName("response_time");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.EventId).IsUnique().HasDatabaseName("idx_log_actions_event_id");
            entity.HasIndex(e => e.FlowId).HasDatabaseName("idx_log_actions_flow_id");
            entity.HasIndex(e => e.ServiceName).HasDatabaseName("idx_log_actions_service_name");
            entity.HasIndex(e => e.ActionType).HasDatabaseName("idx_log_actions_action_type");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_log_actions_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_log_actions_created_at");

            entity.HasOne(e => e.Detail)
                .WithOne(d => d.Action)
                .HasForeignKey<LogActionDetail>(d => d.ActionId)
                .HasPrincipalKey<LogAction>(a => a.Id);
        });

        modelBuilder.Entity<LogActionDetail>(entity =>
        {
            entity.ToTable("log_action_details");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionId).HasColumnName("action_id");
            entity.Property(e => e.RequestPayload).HasColumnName("request_payload").HasColumnType("jsonb");
            entity.Property(e => e.ResponsePayload).HasColumnName("response_payload").HasColumnType("jsonb");
            entity.Property(e => e.ErrorPayload).HasColumnName("error_payload").HasColumnType("jsonb");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ActionId).IsUnique().HasDatabaseName("idx_log_action_details_action_id");
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Domain.Entities;
using Skysim.Logger.Api.Domain.Enums;
using Skysim.Logger.Api.Domain.Services;
using Status = Skysim.Logger.Api.Domain.Enums.Status;

namespace Skysim.Logger.Api.Infrastructure.Persistence.Repositories;

public class LogFlowRepository : ILogFlowRepository
{
    private readonly LoggerDbContext _db;

    public LogFlowRepository(LoggerDbContext db)
    {
        _db = db;
    }

    public async Task<LogFlow> UpsertAsync(
        LogEventMessage message,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var isTerminal = FlowDomainService.IsTerminalAction(message.ActionType, message.Status);
        var isSuccess = message.Status == Status.Success;

        // Upsert log_flows using raw SQL for efficiency
        var sql = @"
            INSERT INTO log_flows (id, flow_id, flow_type, checkout_type, status,
                customer_email, customer_phone, user_id, order_id, payment_id,
                total_steps, success_steps, failed_steps,
                last_action_type, last_message, started_at, completed_at,
                created_at, updated_at)
            VALUES (
                gen_random_uuid(),
                @flowId,
                @flowType,
                @checkoutType,
                @status,
                @customerEmail,
                @customerPhone,
                @userId,
                @orderId,
                @paymentId,
                1,
                @successSteps,
                @failedSteps,
                @lastActionType,
                @lastMessage,
                @startedAt,
                @completedAt,
                @now,
                @now)
            ON CONFLICT (flow_id) DO UPDATE SET
                status = EXCLUDED.status,
                total_steps = log_flows.total_steps + 1,
                success_steps = log_flows.success_steps + @successSteps,
                failed_steps = log_flows.failed_steps + @failedSteps,
                last_action_type = EXCLUDED.last_action_type,
                last_message = EXCLUDED.last_message,
                updated_at = @now,
                completed_at = CASE
                    WHEN @isTerminal THEN @now
                    ELSE log_flows.completed_at
                END
            RETURNING id, flow_id, flow_type, checkout_type, status,
                customer_email, customer_phone, user_id, order_id, payment_id,
                total_steps, success_steps, failed_steps,
                last_action_type, last_message, started_at, completed_at,
                created_at, updated_at";

        var successSteps = isSuccess ? 1 : 0;
        var failedSteps = !isSuccess && isTerminal ? 1 : 0;

        var currentTransaction = _db.Database.CurrentTransaction;
        await using var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)_db.Database.GetDbConnection());

        if (currentTransaction != null)
        {
            cmd.Transaction = (NpgsqlTransaction)currentTransaction.GetDbTransaction();
        }
        cmd.Parameters.AddWithValue("@flowId", message.FlowId);
        cmd.Parameters.AddWithValue("@flowType", message.FlowType.ToString());
        cmd.Parameters.AddWithValue("@checkoutType", (object?)message.CheckoutType?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", message.Status.ToString());
        cmd.Parameters.AddWithValue("@customerEmail", (object?)message.CustomerEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@customerPhone", (object?)message.CustomerPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@userId", (object?)message.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@orderId", (object?)message.OrderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@paymentId", (object?)message.PaymentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@successSteps", successSteps);
        cmd.Parameters.AddWithValue("@failedSteps", failedSteps);
        cmd.Parameters.AddWithValue("@lastActionType", message.ActionType.ToString());
        cmd.Parameters.AddWithValue("@lastMessage", (object?)message.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@startedAt", message.CreatedAt);
        cmd.Parameters.AddWithValue("@completedAt", isTerminal ? now : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@isTerminal", isTerminal);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new LogFlow
        {
            Id = reader.GetGuid(0),
            FlowId = reader.GetString(1),
            FlowType = reader.GetString(2),
            CheckoutType = reader.IsDBNull(3) ? null : reader.GetString(3),
            Status = reader.GetString(4),
            CustomerEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
            CustomerPhone = reader.IsDBNull(6) ? null : reader.GetString(6),
            UserId = reader.IsDBNull(7) ? null : reader.GetString(7),
            OrderId = reader.IsDBNull(8) ? null : reader.GetString(8),
            PaymentId = reader.IsDBNull(9) ? null : reader.GetString(9),
            TotalSteps = reader.GetInt32(10),
            SuccessSteps = reader.GetInt32(11),
            FailedSteps = reader.GetInt32(12),
            LastActionType = reader.IsDBNull(13) ? null : reader.GetString(13),
            LastMessage = reader.IsDBNull(14) ? null : reader.GetString(14),
            StartedAt = reader.GetDateTime(15),
            CompletedAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
            CreatedAt = reader.GetDateTime(17),
            UpdatedAt = reader.GetDateTime(18)
        };
    }

}

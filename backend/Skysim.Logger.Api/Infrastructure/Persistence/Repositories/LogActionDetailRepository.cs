using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using Skysim.Logger.Api.Domain.Entities;

namespace Skysim.Logger.Api.Infrastructure.Persistence.Repositories;

public class LogActionDetailRepository : ILogActionDetailRepository
{
    private readonly LoggerDbContext _db;

    public LogActionDetailRepository(LoggerDbContext db)
    {
        _db = db;
    }

    public async Task<LogActionDetail> UpsertAsync(
        LogActionDetail detail,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var sql = @"
            INSERT INTO log_action_details (id, action_id, request_payload, response_payload,
                error_payload, metadata, created_at, updated_at)
            VALUES (
                @id,
                @actionId,
                @requestPayload,
                @responsePayload,
                @errorPayload,
                @metadata,
                @now,
                @now)
            ON CONFLICT (action_id) DO UPDATE SET
                request_payload = COALESCE(EXCLUDED.request_payload, log_action_details.request_payload),
                response_payload = COALESCE(EXCLUDED.response_payload, log_action_details.response_payload),
                error_payload = COALESCE(EXCLUDED.error_payload, log_action_details.error_payload),
                metadata = COALESCE(EXCLUDED.metadata, log_action_details.metadata),
                updated_at = @now
            RETURNING id, action_id, request_payload, response_payload, error_payload, metadata,
                created_at, updated_at";

        var currentTransaction = _db.Database.CurrentTransaction;
        await using var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)_db.Database.GetDbConnection());

        if (currentTransaction != null)
        {
            cmd.Transaction = (NpgsqlTransaction)currentTransaction.GetDbTransaction();
        }
        cmd.Parameters.Add(new NpgsqlParameter("@id", detail.Id));
        cmd.Parameters.Add(new NpgsqlParameter("@actionId", detail.ActionId));
        cmd.Parameters.Add(new NpgsqlParameter("@requestPayload", NpgsqlDbType.Jsonb)
        {
            Value = (object?)detail.RequestPayload ?? DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("@responsePayload", NpgsqlDbType.Jsonb)
        {
            Value = (object?)detail.ResponsePayload ?? DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("@errorPayload", NpgsqlDbType.Jsonb)
        {
            Value = (object?)detail.ErrorPayload ?? DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("@metadata", NpgsqlDbType.Jsonb)
        {
            Value = (object?)detail.Metadata ?? DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("@now", now));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new LogActionDetail
        {
            Id = reader.GetGuid(0),
            ActionId = reader.GetGuid(1),
            RequestPayload = reader.IsDBNull(2) ? null : reader.GetString(2),
            ResponsePayload = reader.IsDBNull(3) ? null : reader.GetString(3),
            ErrorPayload = reader.IsDBNull(4) ? null : reader.GetString(4),
            Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
            CreatedAt = reader.GetDateTime(6),
            UpdatedAt = reader.GetDateTime(7)
        };
    }
}

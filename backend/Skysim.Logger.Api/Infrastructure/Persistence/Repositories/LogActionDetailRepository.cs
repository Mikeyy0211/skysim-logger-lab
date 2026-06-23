using Microsoft.EntityFrameworkCore;
using Npgsql;
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

        await using var connection = _db.Database.GetDbConnection() as NpgsqlConnection;
        if (connection == null) throw new InvalidOperationException("Database connection is not a NpgsqlConnection.");

        await connection.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", detail.Id);
        cmd.Parameters.AddWithValue("@actionId", detail.ActionId);
        cmd.Parameters.AddWithValue("@requestPayload", (object?)detail.RequestPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@responsePayload", (object?)detail.ResponsePayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@errorPayload", (object?)detail.ErrorPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@metadata", (object?)detail.Metadata ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now", now);

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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skysim.Logger.Api.Domain.Entities;

[Table("log_actions")]
public class LogAction
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("event_id")]
    public Guid EventId { get; set; }

    [Required]
    [Column("flow_id")]
    [MaxLength(100)]
    public string FlowId { get; set; } = string.Empty;

    [Required]
    [Column("step_order")]
    public int StepOrder { get; set; }

    [Required]
    [Column("service_name")]
    [MaxLength(50)]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    [Column("action_type")]
    [MaxLength(50)]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("message")]
    public string? Message { get; set; }

    [Column("error_code")]
    [MaxLength(50)]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("request_time")]
    public DateTime? RequestTime { get; set; }

    [Column("response_time")]
    public DateTime? ResponseTime { get; set; }

    [Column("duration_ms")]
    public int? DurationMs { get; set; }

    [Column("correlation_id")]
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(FlowId))]
    public LogFlow? Flow { get; set; }

    public LogActionDetail? Detail { get; set; }
}

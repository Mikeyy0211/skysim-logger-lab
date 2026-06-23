using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skysim.Logger.Api.Domain.Entities;

[Table("log_action_details")]
public class LogActionDetail
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("action_id")]
    public Guid ActionId { get; set; }

    [Column("request_payload", TypeName = "jsonb")]
    public string? RequestPayload { get; set; }

    [Column("response_payload", TypeName = "jsonb")]
    public string? ResponsePayload { get; set; }

    [Column("error_payload", TypeName = "jsonb")]
    public string? ErrorPayload { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(ActionId))]
    public LogAction? Action { get; set; }
}

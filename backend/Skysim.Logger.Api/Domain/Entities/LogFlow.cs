using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skysim.Logger.Api.Domain.Entities;

[Table("log_flows")]
public class LogFlow
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("flow_id")]
    [MaxLength(100)]
    public string FlowId { get; set; } = string.Empty;

    [Required]
    [Column("flow_type")]
    [MaxLength(50)]
    public string FlowType { get; set; } = string.Empty;

    [Column("checkout_type")]
    [MaxLength(20)]
    public string? CheckoutType { get; set; }

    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("customer_email")]
    [MaxLength(255)]
    public string? CustomerEmail { get; set; }

    [Column("customer_phone")]
    [MaxLength(30)]
    public string? CustomerPhone { get; set; }

    [Column("user_id")]
    [MaxLength(100)]
    public string? UserId { get; set; }

    [Column("order_id")]
    [MaxLength(100)]
    public string? OrderId { get; set; }

    [Column("payment_id")]
    [MaxLength(100)]
    public string? PaymentId { get; set; }

    [Column("total_steps")]
    public int TotalSteps { get; set; }

    [Column("success_steps")]
    public int SuccessSteps { get; set; }

    [Column("failed_steps")]
    public int FailedSteps { get; set; }

    [Column("last_action_type")]
    [MaxLength(50)]
    public string? LastActionType { get; set; }

    [Column("last_message")]
    public string? LastMessage { get; set; }

    [Required]
    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public ICollection<LogAction> Actions { get; set; } = new List<LogAction>();
}

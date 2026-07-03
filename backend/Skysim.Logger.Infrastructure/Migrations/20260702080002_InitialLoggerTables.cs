using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skysim.Logger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLoggerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "log_flows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    flow_id = table.Column<string>(type: "text", nullable: false),
                    flow_type = table.Column<string>(type: "text", nullable: false),
                    checkout_type = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    customer_email = table.Column<string>(type: "text", nullable: true),
                    customer_phone = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    order_id = table.Column<string>(type: "text", nullable: true),
                    payment_id = table.Column<string>(type: "text", nullable: true),
                    total_steps = table.Column<int>(type: "integer", nullable: false),
                    success_steps = table.Column<int>(type: "integer", nullable: false),
                    failed_steps = table.Column<int>(type: "integer", nullable: false),
                    last_action_type = table.Column<string>(type: "text", nullable: true),
                    last_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_flows", x => x.id);
                    table.UniqueConstraint("AK_log_flows_flow_id", x => x.flow_id);
                });

            migrationBuilder.CreateTable(
                name: "log_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_id = table.Column<string>(type: "text", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    service_name = table.Column<string>(type: "text", nullable: false),
                    action_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    request_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    response_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_log_actions_log_flows_flow_id",
                        column: x => x.flow_id,
                        principalTable: "log_flows",
                        principalColumn: "flow_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "log_action_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    action_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_payload = table.Column<string>(type: "jsonb", nullable: true),
                    response_payload = table.Column<string>(type: "jsonb", nullable: true),
                    error_payload = table.Column<string>(type: "jsonb", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_action_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_log_action_details_log_actions_action_id",
                        column: x => x.action_id,
                        principalTable: "log_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_log_action_details_action_id",
                table: "log_action_details",
                column: "action_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_log_actions_action_type",
                table: "log_actions",
                column: "action_type");

            migrationBuilder.CreateIndex(
                name: "idx_log_actions_created_at",
                table: "log_actions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_log_actions_event_id",
                table: "log_actions",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_log_actions_flow_id",
                table: "log_actions",
                column: "flow_id");

            migrationBuilder.CreateIndex(
                name: "idx_log_actions_service_name",
                table: "log_actions",
                column: "service_name");

            migrationBuilder.CreateIndex(
                name: "idx_log_actions_status",
                table: "log_actions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_checkout_type",
                table: "log_flows",
                column: "checkout_type");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_completed_at",
                table: "log_flows",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_created_at",
                table: "log_flows",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_customer_email",
                table: "log_flows",
                column: "customer_email");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_customer_phone",
                table: "log_flows",
                column: "customer_phone");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_flow_id",
                table: "log_flows",
                column: "flow_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_flow_type",
                table: "log_flows",
                column: "flow_type");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_order_id",
                table: "log_flows",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_payment_id",
                table: "log_flows",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_status",
                table: "log_flows",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_log_flows_user_id",
                table: "log_flows",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_action_details");

            migrationBuilder.DropTable(
                name: "log_actions");

            migrationBuilder.DropTable(
                name: "log_flows");
        }
    }
}

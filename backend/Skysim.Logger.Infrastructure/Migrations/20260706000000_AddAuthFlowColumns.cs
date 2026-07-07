using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skysim.Logger.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAuthFlowColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "user_email",
            table: "log_flows",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "username",
            table: "log_flows",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "partner_id",
            table: "log_flows",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "order_code",
            table: "log_flows",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_log_flows_user_email",
            table: "log_flows",
            column: "user_email");

        migrationBuilder.CreateIndex(
            name: "idx_log_flows_partner_id",
            table: "log_flows",
            column: "partner_id");

        migrationBuilder.CreateIndex(
            name: "idx_log_flows_order_code",
            table: "log_flows",
            column: "order_code");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_log_flows_user_email",
            table: "log_flows");

        migrationBuilder.DropIndex(
            name: "idx_log_flows_partner_id",
            table: "log_flows");

        migrationBuilder.DropIndex(
            name: "idx_log_flows_order_code",
            table: "log_flows");

        migrationBuilder.DropColumn(name: "user_email", table: "log_flows");
        migrationBuilder.DropColumn(name: "username", table: "log_flows");
        migrationBuilder.DropColumn(name: "partner_id", table: "log_flows");
        migrationBuilder.DropColumn(name: "order_code", table: "log_flows");
    }
}

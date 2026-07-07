using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skysim.Logger.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTransactionIdToLogFlows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "transaction_id",
            table: "log_flows",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_log_flows_transaction_id",
            table: "log_flows",
            column: "transaction_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_log_flows_transaction_id",
            table: "log_flows");

        migrationBuilder.DropColumn(
            name: "transaction_id",
            table: "log_flows");
    }
}
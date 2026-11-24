using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GitEventOccurredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "occurred_at",
                schema: "git",
                table: "git_events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "occurred_at",
                schema: "git",
                table: "git_events");
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Workflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "workflow_id",
                schema: "config",
                table: "work_item_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "statuses",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(31)", maxLength: 31, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_statuses", x => x.id);
                    table.CheckConstraint("ck_statuses_category", "category IN ('not-started', 'active', 'blocked', 'done', 'cancelled')");
                    table.CheckConstraint("ck_statuses_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("ck_statuses_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
                    table.CheckConstraint("ck_statuses_scope", "scope IN ('project', 'work-item')");
                    table.CheckConstraint("ck_statuses_sort_order", "sort_order >= 0");
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(31)", maxLength: 31, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    initial_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transitions = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflows", x => x.id);
                    table.CheckConstraint("ck_workflows_initial_status", "initial_status_id = ANY (status_ids)");
                    table.CheckConstraint("ck_workflows_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
                    table.CheckConstraint("ck_workflows_scope", "scope IN ('project', 'work-item')");
                    table.ForeignKey(
                        name: "fk_workflows_statuses_initial_status_id",
                        column: x => x.initial_status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_item_types_workflow_id",
                schema: "config",
                table: "work_item_types",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "ix_statuses_scope_key",
                schema: "config",
                table: "statuses",
                columns: new[] { "scope", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_statuses_scope_sort_order",
                schema: "config",
                table: "statuses",
                columns: new[] { "scope", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_workflows_initial_status_id",
                schema: "config",
                table: "workflows",
                column: "initial_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflows_key",
                schema: "config",
                table: "workflows",
                column: "key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_work_item_types_workflows_workflow_id",
                schema: "config",
                table: "work_item_types",
                column: "workflow_id",
                principalSchema: "config",
                principalTable: "workflows",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_item_types_workflows_workflow_id",
                schema: "config",
                table: "work_item_types");

            migrationBuilder.DropTable(
                name: "workflows",
                schema: "config");

            migrationBuilder.DropTable(
                name: "statuses",
                schema: "config");

            migrationBuilder.DropIndex(
                name: "ix_work_item_types_workflow_id",
                schema: "config",
                table: "work_item_types");

            migrationBuilder.DropColumn(
                name: "workflow_id",
                schema: "config",
                table: "work_item_types");
        }
    }
}

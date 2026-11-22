using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkItemTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "config");

            migrationBuilder.CreateTable(
                name: "relation_types",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(31)", maxLength: 31, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    inverse_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_directed = table.Column<bool>(type: "boolean", nullable: false),
                    is_acyclic = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_pairs = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relation_types", x => x.id);
                    table.CheckConstraint("ck_relation_types_acyclic_directed", "is_directed OR NOT is_acyclic");
                    table.CheckConstraint("ck_relation_types_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
                    table.CheckConstraint("ck_relation_types_undirected_name", "is_directed OR inverse_name = name");
                });

            migrationBuilder.CreateTable(
                name: "work_item_types",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(31)", maxLength: 31, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    icon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    fields = table.Column<string>(type: "jsonb", nullable: false),
                    allowed_child_type_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_item_types", x => x.id);
                    table.CheckConstraint("ck_work_item_types_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("ck_work_item_types_icon", "icon IN ('feature', 'task', 'bug', 'subtask', 'milestone', 'epic', 'story')");
                    table.CheckConstraint("ck_work_item_types_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
                    table.CheckConstraint("ck_work_item_types_sort_order", "sort_order >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_relation_types_key",
                schema: "config",
                table: "relation_types",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_item_types_key",
                schema: "config",
                table: "work_item_types",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_item_types_sort_order",
                schema: "config",
                table: "work_item_types",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "relation_types",
                schema: "config");

            migrationBuilder.DropTable(
                name: "work_item_types",
                schema: "config");
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "work");

            migrationBuilder.CreateTable(
                name: "project_counters",
                schema: "work",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_counters", x => x.project_id);
                    table.ForeignKey(
                        name: "fk_project_counters_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    @ref = table.Column<string>(name: "ref", type: "character varying(22)", maxLength: 22, nullable: false),
                    type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    fields = table.Column<string>(type: "jsonb", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_items", x => x.id);
                    table.CheckConstraint("ck_work_items_dates", "start_date IS NULL OR due_date IS NULL OR due_date >= start_date");
                    table.CheckConstraint("ck_work_items_parent", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_work_items_priority", "priority IN ('low', 'medium', 'high', 'critical')");
                    table.ForeignKey(
                        name: "fk_work_items_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_items_statuses_status_id",
                        column: x => x.status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_items_work_item_types_type_id",
                        column: x => x.type_id,
                        principalSchema: "config",
                        principalTable: "work_item_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_items_work_items_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "work",
                        principalTable: "work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_item_status_changes",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approval_overridden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_item_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_item_status_changes_statuses_from_status_id",
                        column: x => x.from_status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_item_status_changes_statuses_to_status_id",
                        column: x => x.to_status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_item_status_changes_users_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_item_status_changes_work_items_work_item_id",
                        column: x => x.work_item_id,
                        principalSchema: "work",
                        principalTable: "work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_item_status_changes_actor_id",
                schema: "work",
                table: "work_item_status_changes",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_status_changes_from_status_id",
                schema: "work",
                table: "work_item_status_changes",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_status_changes_to_status_id",
                schema: "work",
                table: "work_item_status_changes",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_status_changes_work_item_id_at",
                schema: "work",
                table: "work_item_status_changes",
                columns: new[] { "work_item_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_assignee_ids",
                schema: "work",
                table: "work_items",
                column: "assignee_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_parent_id",
                schema: "work",
                table: "work_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_is_archived",
                schema: "work",
                table: "work_items",
                columns: new[] { "project_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_number",
                schema: "work",
                table: "work_items",
                columns: new[] { "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_status_id",
                schema: "work",
                table: "work_items",
                columns: new[] { "project_id", "status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_ref",
                schema: "work",
                table: "work_items",
                column: "ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_status_id",
                schema: "work",
                table: "work_items",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_type_id",
                schema: "work",
                table: "work_items",
                column: "type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_counters",
                schema: "work");

            migrationBuilder.DropTable(
                name: "work_item_status_changes",
                schema: "work");

            migrationBuilder.DropTable(
                name: "work_items",
                schema: "work");
        }
    }
}

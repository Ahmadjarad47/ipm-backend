using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Projects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.CreateTable(
                name: "project_templates",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_type_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    default_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initial_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_role_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    suggested_members = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_templates", x => x.id);
                    table.CheckConstraint("ck_project_templates_default_type_allowed", "default_type_id = ANY (allowed_type_ids)");
                    table.CheckConstraint("ck_project_templates_duration", "duration_days >= 1");
                    table.ForeignKey(
                        name: "fk_project_templates_statuses_initial_status_id",
                        column: x => x.initial_status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_templates_work_item_types_default_type_id",
                        column: x => x.default_type_id,
                        principalSchema: "config",
                        principalTable: "work_item_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_type_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    default_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_overrides = table.Column<string>(type: "jsonb", nullable: false),
                    members_revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.CheckConstraint("ck_projects_code", "code ~ '^[A-Z][A-Z0-9]{1,9}$'");
                    table.CheckConstraint("ck_projects_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_projects_default_type_allowed", "default_type_id = ANY (allowed_type_ids)");
                    table.ForeignKey(
                        name: "fk_projects_statuses_status_id",
                        column: x => x.status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_projects_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_projects_work_item_types_default_type_id",
                        column: x => x.default_type_id,
                        principalSchema: "config",
                        principalTable: "work_item_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_members_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_members_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_status_changes",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approval_overridden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_status_changes_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_status_changes_statuses_from_status_id",
                        column: x => x.from_status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_status_changes_statuses_to_status_id",
                        column: x => x.to_status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_status_changes_users_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_members_project_id_user_id",
                schema: "projects",
                table: "project_members",
                columns: new[] { "project_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_members_role_id",
                schema: "projects",
                table: "project_members",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_members_user_id",
                schema: "projects",
                table: "project_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_status_changes_actor_id",
                schema: "projects",
                table: "project_status_changes",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_status_changes_from_status_id",
                schema: "projects",
                table: "project_status_changes",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_status_changes_project_id_at",
                schema: "projects",
                table: "project_status_changes",
                columns: new[] { "project_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_project_status_changes_to_status_id",
                schema: "projects",
                table: "project_status_changes",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_templates_default_type_id",
                schema: "projects",
                table: "project_templates",
                column: "default_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_templates_initial_status_id",
                schema: "projects",
                table: "project_templates",
                column: "initial_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_templates_name",
                schema: "projects",
                table: "project_templates",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_code",
                schema: "projects",
                table: "projects",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_default_type_id",
                schema: "projects",
                table: "projects",
                column: "default_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_owner_id",
                schema: "projects",
                table: "projects",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_status_id",
                schema: "projects",
                table: "projects",
                column: "status_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_members",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "project_status_changes",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "project_templates",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "projects");
        }
    }
}

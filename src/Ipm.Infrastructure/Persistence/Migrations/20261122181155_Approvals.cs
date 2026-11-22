using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Approvals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "approvals");

            migrationBuilder.CreateTable(
                name: "approval_templates",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(24)", maxLength: 24, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    stages = table.Column<string>(type: "jsonb", nullable: false),
                    protected_fields = table.Column<List<string>>(type: "text[]", nullable: false),
                    rule = table.Column<string>(type: "jsonb", nullable: true),
                    allow_self_approval = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_templates", x => x.id);
                    table.CheckConstraint("ck_approval_templates_event", "event IN ('project-approve', 'project-close', 'work-item-approve', 'sensitive-change')");
                });

            migrationBuilder.CreateTable(
                name: "delegations",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delegations", x => x.id);
                    table.CheckConstraint("ck_delegations_dates", "to_date >= from_date");
                    table.ForeignKey(
                        name: "fk_delegations_asp_net_users_from_user_id",
                        column: x => x.from_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delegations_asp_net_users_to_user_id",
                        column: x => x.to_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    current_stage = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    decisions = table.Column<string>(type: "jsonb", nullable: false),
                    stages = table.Column<string>(type: "jsonb", nullable: false),
                    resolved_approvers = table.Column<string>(type: "jsonb", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    target_status_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stage_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_reminded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_requests", x => x.id);
                    table.CheckConstraint("ck_approval_requests_status", "status IN ('pending-approval', 'approved', 'rejected', 'returned', 'superseded')");
                    table.CheckConstraint("ck_approval_requests_subject_type", "subject_type IN ('project', 'work-item')");
                    table.ForeignKey(
                        name: "fk_approval_requests_approval_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "approvals",
                        principalTable: "approval_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_requests_asp_net_users_requester_id",
                        column: x => x.requester_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_requests_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_requests_statuses_target_status_id",
                        column: x => x.target_status_id,
                        principalSchema: "config",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_project_id_status",
                schema: "approvals",
                table: "approval_requests",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_requester_id",
                schema: "approvals",
                table: "approval_requests",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_subject_type_subject_id_status",
                schema: "approvals",
                table: "approval_requests",
                columns: new[] { "subject_type", "subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_target_status_id",
                schema: "approvals",
                table: "approval_requests",
                column: "target_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_template_id",
                schema: "approvals",
                table: "approval_requests",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_templates_event_is_active",
                schema: "approvals",
                table: "approval_templates",
                columns: new[] { "event", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_delegations_from_user_id_is_active",
                schema: "approvals",
                table: "delegations",
                columns: new[] { "from_user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_delegations_to_user_id",
                schema: "approvals",
                table: "delegations",
                column: "to_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "delegations",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "approval_templates",
                schema: "approvals");
        }
    }
}

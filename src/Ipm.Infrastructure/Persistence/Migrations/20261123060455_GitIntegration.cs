using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GitIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "git");

            migrationBuilder.AddColumn<string>(
                name: "ref_pattern",
                schema: "projects",
                table: "projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "repositories",
                schema: "git",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_connected = table.Column<bool>(type: "boolean", nullable: false),
                    access_token_cipher = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    webhook_secret_cipher = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_sync_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_sync_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repositories", x => x.id);
                    table.CheckConstraint("ck_repositories_provider", "provider IN ('gitea', 'github', 'gitlab')");
                    table.ForeignKey(
                        name: "fk_repositories_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "git_events",
                schema: "git",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    branch = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    work_item_refs = table.Column<List<string>>(type: "text[]", nullable: false),
                    manual_refs = table.Column<List<string>>(type: "text[]", nullable: false),
                    pr_number = table.Column<int>(type: "integer", nullable: true),
                    pr_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    delivery_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_events", x => x.id);
                    table.CheckConstraint("ck_git_events_kind", "kind IN ('push', 'pull-request', 'merge')");
                    table.ForeignKey(
                        name: "fk_git_events_git_repositories_repository_id",
                        column: x => x.repository_id,
                        principalSchema: "git",
                        principalTable: "repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_git_events_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                schema: "git",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    delivery_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(100)", maxLength: 100, nullable: false),
                    signature_valid = table.Column<bool>(type: "boolean", nullable: false),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    linked_refs = table.Column<List<string>>(type: "text[]", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_deliveries", x => x.id);
                    table.CheckConstraint("ck_webhook_deliveries_outcome", "outcome IN ('processed', 'duplicate', 'rejected', 'failed')");
                    table.ForeignKey(
                        name: "fk_webhook_deliveries_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_webhook_deliveries_repositories_repository_id",
                        column: x => x.repository_id,
                        principalSchema: "git",
                        principalTable: "repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_git_events_project_id_created_at",
                schema: "git",
                table: "git_events",
                columns: new[] { "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_git_events_repository_id_pr_number",
                schema: "git",
                table: "git_events",
                columns: new[] { "repository_id", "pr_number" },
                unique: true,
                filter: "pr_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_git_events_repository_id_sha",
                schema: "git",
                table: "git_events",
                columns: new[] { "repository_id", "sha" },
                unique: true,
                filter: "kind = 'push'");

            migrationBuilder.CreateIndex(
                name: "ix_git_events_work_item_refs",
                schema: "git",
                table: "git_events",
                column: "work_item_refs")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_repositories_project_id_name",
                schema: "git",
                table: "repositories",
                columns: new[] { "project_id", "name" },
                unique: true,
                filter: "is_connected");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_project_id_created_at",
                schema: "git",
                table: "webhook_deliveries",
                columns: new[] { "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_repository_id_delivery_id",
                schema: "git",
                table: "webhook_deliveries",
                columns: new[] { "repository_id", "delivery_id" },
                unique: true,
                filter: "outcome IN ('processed', 'failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "git_events",
                schema: "git");

            migrationBuilder.DropTable(
                name: "webhook_deliveries",
                schema: "git");

            migrationBuilder.DropTable(
                name: "repositories",
                schema: "git");

            migrationBuilder.DropColumn(
                name: "ref_pattern",
                schema: "projects",
                table: "projects");
        }
    }
}

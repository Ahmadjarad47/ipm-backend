using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivityAuditNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "activity");

            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                schema: "public",
                table: "audit_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "hash",
                schema: "public",
                table: "audit_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "prev_hash",
                schema: "public",
                table: "audit_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                schema: "public",
                table: "audit_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "seq",
                schema: "public",
                table: "audit_log",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "public",
                table: "audit_log",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "subject_label",
                schema: "public",
                table: "audit_log",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "work_item_id",
                schema: "public",
                table: "audit_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    link = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                });

            migrationBuilder.Sql(Ipm.Infrastructure.Persistence.Auditing.AuditChainSql.Functions);
            migrationBuilder.Sql(Ipm.Infrastructure.Persistence.Auditing.AuditChainSql.Backfill);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_correlation_id",
                schema: "public",
                table: "audit_log",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_project_id_at",
                schema: "public",
                table: "audit_log",
                columns: new[] { "project_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_seq",
                schema: "public",
                table: "audit_log",
                column: "seq",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_work_item_id_at",
                schema: "public",
                table: "audit_log",
                columns: new[] { "work_item_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at_created_at",
                schema: "activity",
                table: "notifications",
                columns: new[] { "user_id", "read_at", "created_at" });

            migrationBuilder.Sql(Ipm.Infrastructure.Persistence.Auditing.AuditChainSql.Triggers);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_log_chain ON public.audit_log; DROP TRIGGER IF EXISTS audit_log_no_change ON public.audit_log; DROP TRIGGER IF EXISTS audit_log_no_truncate ON public.audit_log;");
            migrationBuilder.DropTable(
                name: "notifications",
                schema: "activity");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_correlation_id",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_project_id_at",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_seq",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_work_item_id_at",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "hash",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "prev_hash",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "project_id",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "seq",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "subject_label",
                schema: "public",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "work_item_id",
                schema: "public",
                table: "audit_log");
        }
    }
}

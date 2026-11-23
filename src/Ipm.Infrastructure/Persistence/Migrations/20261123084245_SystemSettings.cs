using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_hours = table.Column<int>(type: "integer", nullable: false),
                    comment_edit_hours = table.Column<int>(type: "integer", nullable: false),
                    attachment_max_mb = table.Column<int>(type: "integer", nullable: false),
                    attachment_extensions = table.Column<List<string>>(type: "text[]", nullable: false),
                    allow_cross_project_relations = table.Column<bool>(type: "boolean", nullable: false),
                    allow_self_approval = table.Column<bool>(type: "boolean", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    audit_retention_days = table.Column<int>(type: "integer", nullable: false),
                    projects_private_by_default = table.Column<bool>(type: "boolean", nullable: false),
                    progress_included_type_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.id);
                });

            // Spec 09 §6: the one settings record exists from the start, with the defaults the earlier parts used as constants.
            migrationBuilder.Sql(
                """
                INSERT INTO config.system_settings (id, session_hours, comment_edit_hours, attachment_max_mb, attachment_extensions, allow_cross_project_relations,
                    allow_self_approval, time_zone, audit_retention_days, projects_private_by_default, progress_included_type_ids, created_at)
                VALUES ('01970000-0000-7000-8000-000000000001', 8, 24, 10, ARRAY['pdf','png','jpg','jpeg','gif','txt','csv','docx','xlsx','pptx','zip'],
                    false, false, 'Asia/Riyadh', 2555, true, ARRAY[]::uuid[], now())
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "config");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditLogging.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogType = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGranted = table.Column<bool>(type: "boolean", nullable: true),
                    DenialReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CacheHit = table.Column<bool>(type: "boolean", nullable: true),
                    EvaluationLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    PolicyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DelegationChain = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TargetEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    OldValue = table.Column<string>(type: "jsonb", nullable: true),
                    NewValue = table.Column<string>(type: "jsonb", nullable: true),
                    IsPlatformAction = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                schema: "audit",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_ActorUserId_Timestamp",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "TenantId", "ActorUserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_ResourceId_Timestamp",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "TenantId", "ResourceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "audit");
        }
    }
}

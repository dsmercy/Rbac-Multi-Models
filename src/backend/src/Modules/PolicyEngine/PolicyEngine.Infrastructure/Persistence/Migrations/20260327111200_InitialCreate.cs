using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolicyEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "policy");

            migrationBuilder.CreateTable(
                name: "Policies",
                schema: "policy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Effect = table.Column<int>(type: "integer", nullable: false),
                    ConditionTreeJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_TenantId_IsActive",
                schema: "policy",
                table: "Policies",
                columns: new[] { "TenantId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_TenantId_ResourceId_IsActive",
                schema: "policy",
                table: "Policies",
                columns: new[] { "TenantId", "ResourceId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Policies",
                schema: "policy");
        }
    }
}

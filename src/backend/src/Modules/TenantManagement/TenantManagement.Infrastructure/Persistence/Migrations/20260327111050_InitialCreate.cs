using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenant");

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsBootstrapped = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MaxDelegationChainDepth = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    PermissionCacheTtlSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    TokenVersionCacheTtlSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 3600),
                    MaxUsersAllowed = table.Column<int>(type: "integer", nullable: false, defaultValue: 500),
                    MaxRolesAllowed = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    SuspendedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsDeleted",
                schema: "tenant",
                table: "Tenants",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "UQ_Tenants_Slug",
                schema: "tenant",
                table: "Tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "tenant");
        }
    }
}

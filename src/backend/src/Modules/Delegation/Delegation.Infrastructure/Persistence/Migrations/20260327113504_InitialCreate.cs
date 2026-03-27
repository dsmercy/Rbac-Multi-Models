using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Delegation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "delegation");

            migrationBuilder.CreateTable(
                name: "Delegations",
                schema: "delegation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChainDepth = table.Column<int>(type: "integer", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    permission_codes = table.Column<List<string>>(type: "text[]", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_TenantId_DelegateeId_IsRevoked",
                schema: "delegation",
                table: "Delegations",
                columns: new[] { "TenantId", "DelegateeId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_TenantId_DelegatorId_IsRevoked",
                schema: "delegation",
                table: "Delegations",
                columns: new[] { "TenantId", "DelegatorId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_TenantId_ExpiresAt",
                schema: "delegation",
                table: "Delegations",
                columns: new[] { "TenantId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Delegations",
                schema: "delegation");
        }
    }
}

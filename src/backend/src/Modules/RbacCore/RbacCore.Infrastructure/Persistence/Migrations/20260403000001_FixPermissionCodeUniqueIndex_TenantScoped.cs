using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RbacCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPermissionCodeUniqueIndex_TenantScoped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop global-unique index — code was unique across ALL tenants
            migrationBuilder.DropIndex(
                name: "IX_Permissions_Code",
                schema: "rbac",
                table: "Permissions");

            // Add tenant-scoped unique index — same code may exist in different tenants
            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId_Code",
                schema: "rbac",
                table: "Permissions",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Permissions_TenantId_Code",
                schema: "rbac",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "rbac",
                table: "Permissions",
                column: "Code",
                unique: true);
        }
    }
}

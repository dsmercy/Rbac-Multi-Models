using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RbacCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rbac");

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "rbac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "rbac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScopeHierarchy",
                schema: "rbac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AncestorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescendantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopeHierarchy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scopes",
                schema: "rbac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ParentScopeId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Scopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                schema: "rbac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "rbac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "rbac",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "rbac",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId_IsDeleted",
                schema: "rbac",
                table: "Permissions",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                schema: "rbac",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_IsDeleted",
                schema: "rbac",
                table: "Roles",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UQ_Roles_TenantId_Name",
                schema: "rbac",
                table: "Roles",
                columns: new[] { "TenantId", "Name", "IsDeleted" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ScopeHierarchy_TenantId_AncestorId",
                schema: "rbac",
                table: "ScopeHierarchy",
                columns: new[] { "TenantId", "AncestorId", "Depth" });

            migrationBuilder.CreateIndex(
                name: "IX_ScopeHierarchy_TenantId_DescendantId",
                schema: "rbac",
                table: "ScopeHierarchy",
                columns: new[] { "TenantId", "DescendantId", "Depth" });

            migrationBuilder.CreateIndex(
                name: "UQ_ScopeHierarchy_Ancestor_Descendant",
                schema: "rbac",
                table: "ScopeHierarchy",
                columns: new[] { "TenantId", "AncestorId", "DescendantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_TenantId_RoleId_IsActive",
                schema: "rbac",
                table: "UserRoleAssignments",
                columns: new[] { "TenantId", "RoleId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_TenantId_UserId_IsActive",
                schema: "rbac",
                table: "UserRoleAssignments",
                columns: new[] { "TenantId", "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_TenantId_UserId_ScopeId_IsActive",
                schema: "rbac",
                table: "UserRoleAssignments",
                columns: new[] { "TenantId", "UserId", "ScopeId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "ScopeHierarchy",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "Scopes",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "UserRoleAssignments",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "rbac");
        }
    }
}

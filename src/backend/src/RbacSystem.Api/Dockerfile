# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY ["src/RbacSystem.Api/RbacSystem.Api.csproj",                                           "src/RbacSystem.Api/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Domain/BuildingBlocks.Domain.csproj",               "src/BuildingBlocks/BuildingBlocks.Domain/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Application/BuildingBlocks.Application.csproj",     "src/BuildingBlocks/BuildingBlocks.Application/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Infrastructure/BuildingBlocks.Infrastructure.csproj","src/BuildingBlocks/BuildingBlocks.Infrastructure/"]
COPY ["src/Modules/Identity/Identity.Domain/Identity.Domain.csproj",                         "src/Modules/Identity/Identity.Domain/"]
COPY ["src/Modules/Identity/Identity.Application/Identity.Application.csproj",               "src/Modules/Identity/Identity.Application/"]
COPY ["src/Modules/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj",         "src/Modules/Identity/Identity.Infrastructure/"]
COPY ["src/Modules/TenantManagement/TenantManagement.Domain/TenantManagement.Domain.csproj", "src/Modules/TenantManagement/TenantManagement.Domain/"]
COPY ["src/Modules/TenantManagement/TenantManagement.Application/TenantManagement.Application.csproj","src/Modules/TenantManagement/TenantManagement.Application/"]
COPY ["src/Modules/TenantManagement/TenantManagement.Infrastructure/TenantManagement.Infrastructure.csproj","src/Modules/TenantManagement/TenantManagement.Infrastructure/"]
COPY ["src/Modules/RbacCore/RbacCore.Domain/RbacCore.Domain.csproj",                         "src/Modules/RbacCore/RbacCore.Domain/"]
COPY ["src/Modules/RbacCore/RbacCore.Application/RbacCore.Application.csproj",               "src/Modules/RbacCore/RbacCore.Application/"]
COPY ["src/Modules/RbacCore/RbacCore.Infrastructure/RbacCore.Infrastructure.csproj",         "src/Modules/RbacCore/RbacCore.Infrastructure/"]
COPY ["src/Modules/PermissionEngine/PermissionEngine.Domain/PermissionEngine.Domain.csproj", "src/Modules/PermissionEngine/PermissionEngine.Domain/"]
COPY ["src/Modules/PermissionEngine/PermissionEngine.Application/PermissionEngine.Application.csproj","src/Modules/PermissionEngine/PermissionEngine.Application/"]
COPY ["src/Modules/PermissionEngine/PermissionEngine.Infrastructure/PermissionEngine.Infrastructure.csproj","src/Modules/PermissionEngine/PermissionEngine.Infrastructure/"]
COPY ["src/Modules/PolicyEngine/PolicyEngine.Domain/PolicyEngine.Domain.csproj",             "src/Modules/PolicyEngine/PolicyEngine.Domain/"]
COPY ["src/Modules/PolicyEngine/PolicyEngine.Application/PolicyEngine.Application.csproj",   "src/Modules/PolicyEngine/PolicyEngine.Application/"]
COPY ["src/Modules/PolicyEngine/PolicyEngine.Infrastructure/PolicyEngine.Infrastructure.csproj","src/Modules/PolicyEngine/PolicyEngine.Infrastructure/"]
COPY ["src/Modules/Delegation/Delegation.Domain/Delegation.Domain.csproj",                   "src/Modules/Delegation/Delegation.Domain/"]
COPY ["src/Modules/Delegation/Delegation.Application/Delegation.Application.csproj",         "src/Modules/Delegation/Delegation.Application/"]
COPY ["src/Modules/Delegation/Delegation.Infrastructure/Delegation.Infrastructure.csproj",   "src/Modules/Delegation/Delegation.Infrastructure/"]
COPY ["src/Modules/AuditLogging/AuditLogging.Domain/AuditLogging.Domain.csproj",             "src/Modules/AuditLogging/AuditLogging.Domain/"]
COPY ["src/Modules/AuditLogging/AuditLogging.Application/AuditLogging.Application.csproj",   "src/Modules/AuditLogging/AuditLogging.Application/"]
COPY ["src/Modules/AuditLogging/AuditLogging.Infrastructure/AuditLogging.Infrastructure.csproj","src/Modules/AuditLogging/AuditLogging.Infrastructure/"]

RUN dotnet restore "src/RbacSystem.Api/RbacSystem.Api.csproj"

# Copy all source
COPY . .

RUN dotnet build "src/RbacSystem.Api/RbacSystem.Api.csproj" \
    -c Release \
    -o /app/build \
    --no-restore

RUN dotnet publish "src/RbacSystem.Api/RbacSystem.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup -S rbac && adduser -S rbac -G rbac
USER rbac

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "RbacSystem.Api.dll"]

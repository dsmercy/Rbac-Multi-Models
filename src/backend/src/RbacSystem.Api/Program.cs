using AuditLogging.Infrastructure;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Delegation.Infrastructure;
using Identity.Application.Services;
using Identity.Infrastructure;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PermissionEngine.Infrastructure;
using PolicyEngine.Infrastructure;
using RbacCore.Infrastructure;
using RbacSystem.Api.Hubs;
using RbacSystem.Api.Infrastructure;
using RbacSystem.Api.Middleware;
using RbacSystem.Api.Seeding;
using Serilog;
using System.Text;
using TenantManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog structured logging ────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .Enrich.WithProperty("Application", "RbacSystem")
       .WriteTo.Console(outputTemplate:
           "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
           "{Properties:j}{NewLine}{Exception}"));

// ── JWT Authentication ────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Disable claim type mapping so JWT claim names are preserved as-is in
        // ClaimsPrincipal (e.g. "tid", "tv", "sub" are not remapped to XML schema URNs).
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
            ClockSkew  = TimeSpan.FromSeconds(30),
            // With MapInboundClaims = false, role and name claims keep their JWT names.
            RoleClaimType = "roles",
            NameClaimType = "sub"
        };

        // Log malformed / corrupt JWT events (OWASP A07)
        options.Events = new JwtBearerEvents
        {
            // Browsers cannot set Authorization headers on WebSocket upgrades.
            // SignalR JS client passes the token as ?access_token=... on the initial
            // HTTP negotiate request and on the WebSocket upgrade URL.
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"].ToString();
                var path  = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/api/v1/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },

            OnAuthenticationFailed = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();

                logger.LogWarning(
                    "JWT authentication failed: {Error} | IP={Ip} UA={UA}",
                    ctx.Exception.GetType().Name,
                    ctx.HttpContext.Connection.RemoteIpAddress,
                    ctx.HttpContext.Request.Headers.UserAgent.ToString());

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── HttpContext / TenantContext ───────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── MediatR pipeline behaviors ────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

// ── Module registrations ──────────────────────────────────────────────────────
// Note: PermissionEngineModule must be registered BEFORE IdentityModule because
// it registers ITokenVersionService (used by LoginCommandHandler).
builder.Services
    .AddPermissionEngineModule(builder.Configuration) // registers ITokenVersionService
    .AddIdentityModule(builder.Configuration)
    .AddTenantManagementModule(builder.Configuration)
    .AddRbacCoreModule(builder.Configuration)
    .AddPolicyEngineModule(builder.Configuration)
    .AddDelegationModule(builder.Configuration)
    .AddAuditLoggingModule(builder.Configuration);

// IUserRoleProvider: Dapper-based; registered after IdentityModule so it can
// override the placeholder registration if any exists.
builder.Services.AddScoped<IUserRoleProvider, UserRoleProvider>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "RBAC System API",
        Version     = "v1",
        Description = "Enterprise multi-tenant RBAC system with ABAC policy engine, " +
                      "scoped hierarchy, time-bound delegation, and full audit trail."
    });

    // Include XML doc comments from the API project
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Description  = "JWT access token. The `tv` (token version) claim is validated " +
                       "against Redis on every permission-engine request to detect stale tokens."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Seed data (development only) ──────────────────────────────────────────────
builder.Services.AddTransient<DataSeeder>();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
// GlobalExceptionMiddleware must be first to catch all downstream exceptions,
// including StaleTokenException (→ 401) from TokenVersionValidationStep.
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

// Security headers (OWASP hardening)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"]    = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]            = "DENY";
    ctx.Response.Headers["Strict-Transport-Security"]  = "max-age=63072000; includeSubDomains";
    ctx.Response.Headers["X-XSS-Protection"]           = "0";
    ctx.Response.Headers["Referrer-Policy"]            = "no-referrer";
    ctx.Response.Headers["Permissions-Policy"]         = "geolocation=(), microphone=()";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// TenantValidationMiddleware runs AFTER auth so the JWT is already validated.
// It compares the {tid} route parameter against the JWT "tid" claim.
app.UseMiddleware<TenantValidationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// SignalR hub — tenant-isolated real-time RBAC invalidation events.
// JS client connects to /api/v1/hubs/rbac?access_token=<jwt>
app.MapHub<RbacHub>("/api/v1/hubs/rbac");

await app.SeedDevelopmentDataAsync();

app.Run();

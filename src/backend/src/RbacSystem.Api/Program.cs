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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ── HttpContext / TenantContext ───────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

// ── MediatR pipeline behaviors ────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

// ── Module registrations ──────────────────────────────────────────────────────
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddTenantManagementModule(builder.Configuration)
    .AddRbacCoreModule(builder.Configuration)
    .AddPermissionEngineModule(builder.Configuration)
    .AddPolicyEngineModule(builder.Configuration)
    .AddDelegationModule(builder.Configuration)
    .AddAuditLoggingModule(builder.Configuration);

builder.Services.AddScoped<IUserRoleProvider, UserRoleProvider>();


// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "RBAC System API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
});

// ── Seed data (development only) ────────────────
builder.Services.AddTransient<DataSeeder>();

// ── Rate limiting ─────────────────────────────────────────────────────────────
//builder.Services.AddRateLimiter(options =>
//{
//    options.AddFixedWindowLimiter("per-user", cfg =>
//    {
//        cfg.PermitLimit = 300;
//        cfg.Window = TimeSpan.FromMinutes(1);
//    });
//});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";
    ctx.Response.Headers["X-XSS-Protection"] = "0";
    await next();
});

//app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantValidationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

await app.SeedDevelopmentDataAsync();

app.Run();

using BuildingBlocks.Domain;
using FluentValidation;
using System.Net;
using System.Text.Json;

namespace RbacSystem.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception,
            "Unhandled exception for {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        var (statusCode, code, message) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "VALIDATION_ERROR",
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),

            DomainException de => (
                HttpStatusCode.UnprocessableEntity,
                de.Code,
                de.Message),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "NOT_FOUND",
                "The requested resource was not found."),

            UnauthorizedAccessException => (
                HttpStatusCode.Forbidden,
                "FORBIDDEN",
                "You do not have permission to perform this action."),

            InvalidOperationException ioe => (
                HttpStatusCode.BadRequest,
                "INVALID_OPERATION",
                ioe.Message),

            _ => (
                HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            code,
            message,
            traceId = context.TraceIdentifier
        });

        await context.Response.WriteAsync(body);
    }
}

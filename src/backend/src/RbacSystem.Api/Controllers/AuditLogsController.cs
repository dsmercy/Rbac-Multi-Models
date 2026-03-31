using AuditLogging.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Immutable audit log — query and export the append-only access decision history.
/// Audit records are never updated or deleted. GDPR erasure pseudonymises <c>ActorUserId</c>
/// rather than deleting records. Retention: 7 years.
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/audit-logs")]
[Authorize]
[Produces("application/json", "text/csv")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogsController(IAuditLogRepository auditLogRepository)
        => _auditLogRepository = auditLogRepository;

    /// <summary>Query audit logs with optional filters. Supports JSON and CSV export.</summary>
    /// <remarks>
    /// <para>
    /// Set <c>Accept: text/csv</c> to download a CSV file instead of JSON.
    /// The CSV includes all filtered records up to <c>pageSize</c> (max 200).
    /// </para>
    /// <para>
    /// Date parameters must be in UTC ISO-8601 format (e.g. <c>2026-01-15T00:00:00Z</c>).
    /// Non-UTC offsets are automatically converted to UTC.
    /// </para>
    /// <para>Default window is the last 7 days when <c>from</c>/<c>to</c> are omitted.</para>
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="from">Start of the time range (inclusive, UTC). Defaults to 7 days ago.</param>
    /// <param name="to">End of the time range (inclusive, UTC). Defaults to now.</param>
    /// <param name="userId">Filter by the actor's user UUID.</param>
    /// <param name="action">Filter by action string (exact match, e.g. <c>users:delete</c>).</param>
    /// <param name="resourceId">Filter by the resource UUID that was accessed.</param>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Records per page (max 200). Defaults to 50.</param>
    /// <response code="200">
    /// JSON: paginated result with <c>data</c>, <c>total</c>, <c>page</c>, <c>pageSize</c>, <c>totalPages</c>.
    /// CSV (<c>Accept: text/csv</c>): file download of the same records.
    /// </response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs(
        Guid tid,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        [FromQuery] Guid? resourceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 200);
        var fromDate = (from ?? DateTimeOffset.UtcNow.AddDays(-7)).ToUniversalTime();
        var toDate   = (to   ?? DateTimeOffset.UtcNow).ToUniversalTime();

        var logs = await _auditLogRepository.QueryAsync(
            tid, fromDate, toDate, userId, action, resourceId, page, pageSize, ct);

        var total = await _auditLogRepository.CountAsync(
            tid, fromDate, toDate, userId, action, resourceId, ct);

        if (Request.Headers.Accept.ToString().Contains("text/csv"))
            return ExportCsv(logs);

        return Ok(new AuditLogPagedResponse(
            logs, total, page, pageSize,
            (int)Math.Ceiling(total / (double)pageSize)));
    }

    private FileContentResult ExportCsv(IReadOnlyList<AuditLogging.Domain.Entities.AuditLog> logs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,CorrelationId,ActorUserId,Action,ResourceId,ScopeId,IsGranted,DenialReason,CacheHit,LatencyMs");

        foreach (var log in logs)
        {
            sb.AppendLine(
                $"{log.Timestamp:O}," +
                $"{log.CorrelationId}," +
                $"{log.ActorUserId}," +
                $"{log.Action}," +
                $"{log.ResourceId}," +
                $"{log.ScopeId}," +
                $"{log.IsGranted}," +
                $"{log.DenialReason}," +
                $"{log.CacheHit}," +
                $"{log.EvaluationLatencyMs}");
        }

        return File(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"audit-logs-{DateTimeOffset.UtcNow:yyyyMMdd}.csv");
    }
}

/// <summary>Paginated audit log response (JSON format).</summary>
public sealed record AuditLogPagedResponse(
    /// <summary>Audit log entries for the current page.</summary>
    IReadOnlyList<AuditLogging.Domain.Entities.AuditLog> Data,
    /// <summary>Total number of records matching the filters (across all pages).</summary>
    long Total,
    /// <summary>Current page number (1-based).</summary>
    int Page,
    /// <summary>Records per page (capped at 200).</summary>
    int PageSize,
    /// <summary>Total number of pages.</summary>
    int TotalPages);

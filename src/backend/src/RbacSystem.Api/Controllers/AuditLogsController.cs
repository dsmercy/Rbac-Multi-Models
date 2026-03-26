using AuditLogging.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace RbacSystem.Api.Controllers;

[ApiController]
[Route("api/v1/tenants/{tid:guid}/audit-logs")]
[Authorize]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogsController(IAuditLogRepository auditLogRepository)
        => _auditLogRepository = auditLogRepository;

    [HttpGet]
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
        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-7);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var logs = await _auditLogRepository.QueryAsync(
            tid, fromDate, toDate, userId, action, resourceId, page, pageSize, ct);

        var total = await _auditLogRepository.CountAsync(
            tid, fromDate, toDate, userId, action, resourceId, ct);

        // Support CSV export via Accept header
        if (Request.Headers.Accept.ToString().Contains("text/csv"))
            return ExportCsv(logs);

        return Ok(new
        {
            data = logs,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
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

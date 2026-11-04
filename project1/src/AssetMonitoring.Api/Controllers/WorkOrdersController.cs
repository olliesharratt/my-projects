using AssetMonitoring.Application.WorkOrders;
using AssetMonitoring.Domain.Entities;
using AssetMonitoring.Domain.Enums;
using AssetMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetMonitoring.Api.Controllers;

[ApiController, Route("api/work-orders"), Authorize]
public sealed class WorkOrdersController(MonitoringDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkOrderDto>>> List([FromQuery] WorkOrderStatus? status, [FromQuery] Guid? assetId, CancellationToken ct)
    {
        var query = db.WorkOrders.AsNoTracking();
        if (status is not null) query = query.Where(x => x.Status == status);
        if (assetId is not null) query = query.Where(x => x.AssetId == assetId);
        return Ok(await query.OrderByDescending(x => x.CreatedAt).Select(x => new WorkOrderDto(x.Id, x.AssetId, x.Title, x.Description, x.Status, x.AssignedTo, x.CreatedAt, x.CompletedAt)).ToListAsync(ct));
    }

    [HttpPost, Authorize(Policy = "EngineerOrAdmin")]
    public async Task<ActionResult<WorkOrderDto>> Create(CreateWorkOrderRequest request, CancellationToken ct)
    {
        if (!await db.Assets.AnyAsync(x => x.Id == request.AssetId, ct)) return NotFound(new { message = "Asset was not found." });
        var order = new WorkOrder { AssetId = request.AssetId, Title = request.Title, Description = request.Description, AssignedTo = request.AssignedTo, Asset = null! };
        db.WorkOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { id = order.Id }, new WorkOrderDto(order.Id, order.AssetId, order.Title, order.Description, order.Status, order.AssignedTo, order.CreatedAt, order.CompletedAt));
    }

    [HttpPatch("{id:guid}/status"), Authorize(Policy = "EngineerOrAdmin")]
    public async Task<ActionResult<WorkOrderDto>> ChangeStatus(Guid id, ChangeWorkOrderStatusRequest request, CancellationToken ct)
    {
        var order = await db.WorkOrders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return NotFound();
        if ((int)request.Status < (int)order.Status) return BadRequest(new { message = "Work orders cannot move backwards in the workflow." });
        order.Status = request.Status;
        order.CompletedAt = request.Status == WorkOrderStatus.Completed ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Ok(new WorkOrderDto(order.Id, order.AssetId, order.Title, order.Description, order.Status, order.AssignedTo, order.CreatedAt, order.CompletedAt));
    }
}

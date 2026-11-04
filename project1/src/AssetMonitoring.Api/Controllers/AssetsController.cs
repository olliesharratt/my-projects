using AssetMonitoring.Application.Assets;
using AssetMonitoring.Domain.Entities;
using AssetMonitoring.Domain.Enums;
using AssetMonitoring.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetMonitoring.Api.Controllers;

[ApiController, Route("api/assets"), Authorize]
public sealed class AssetsController(AssetRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? site = null, [FromQuery] string? type = null, [FromQuery] AssetStatus? status = null, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = repository.Query();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Site.Contains(search));
        if (!string.IsNullOrWhiteSpace(site)) query = query.Where(x => x.Site == site);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);
        if (status is not null) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AssetDto(x.Id, x.Name, x.Site, x.Type, x.Status, x.UpdatedAt)).ToListAsync(ct);
        return Ok(new PagedResult<AssetDto>(items, page, pageSize, total));
    }

    [HttpPost, Authorize(Policy = "EngineerOrAdmin")]
    public async Task<ActionResult<AssetDto>> Create(CreateAssetRequest request, CancellationToken ct)
    {
        var asset = new Asset { Name = request.Name, Site = request.Site, Type = request.Type };
        repository.Db.Assets.Add(asset); await repository.SaveAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = asset.Id }, new AssetDto(asset.Id, asset.Name, asset.Site, asset.Type, asset.Status, asset.UpdatedAt));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDto>> Get(Guid id, CancellationToken ct)
    {
        var asset = await repository.FindAsync(id, ct);
        return asset is null ? NotFound() : Ok(new AssetDto(asset.Id, asset.Name, asset.Site, asset.Type, asset.Status, asset.UpdatedAt));
    }
}

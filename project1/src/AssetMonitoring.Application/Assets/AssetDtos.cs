using AssetMonitoring.Domain.Enums;
using FluentValidation;

namespace AssetMonitoring.Application.Assets;

public sealed record AssetDto(Guid Id, string Name, string Site, string Type, AssetStatus Status, DateTimeOffset UpdatedAt);
public sealed record CreateAssetRequest(string Name, string Site, string Type);
public sealed record UpdateAssetRequest(string Name, string Site, string Type, AssetStatus Status);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class CreateAssetValidator : AbstractValidator<CreateAssetRequest>
{
    public CreateAssetValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Site).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(80);
    }
}

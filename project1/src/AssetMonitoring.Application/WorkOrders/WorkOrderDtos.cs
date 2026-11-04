using AssetMonitoring.Domain.Enums;
using FluentValidation;

namespace AssetMonitoring.Application.WorkOrders;

public sealed record WorkOrderDto(Guid Id, Guid AssetId, string Title, string? Description, WorkOrderStatus Status, string? AssignedTo, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
public sealed record CreateWorkOrderRequest(Guid AssetId, string Title, string? Description, string? AssignedTo);
public sealed record ChangeWorkOrderStatusRequest(WorkOrderStatus Status);

public sealed class CreateWorkOrderValidator : AbstractValidator<CreateWorkOrderRequest>
{
    public CreateWorkOrderValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

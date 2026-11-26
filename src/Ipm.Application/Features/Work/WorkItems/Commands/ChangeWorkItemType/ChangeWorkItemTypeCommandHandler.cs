using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ChangeWorkItemType;

internal sealed class ChangeWorkItemTypeCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<ChangeWorkItemTypeCommand, TypeChangeResponse>
{
    public async ValueTask<Result<TypeChangeResponse>> Handle(ChangeWorkItemTypeCommand command, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.WorkItemTypesManage, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (item, project) = (guarded.Value.Item, guarded.Value.Project);
        if (!item.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var editable = item.EnsureEditable();
        if (editable.IsFailure)
        {
            return editable.Error;
        }

        if (project.IsArchived)
        {
            return WorkItemErrors.ProjectArchived;
        }

        var conflicts = await TypeChangeAnalysis.ConflictsAsync(db, project, item, command.TypeId, command.Fields, cancellationToken);
        if (conflicts.Count > 0)
        {
            return new TypeChangeResponse(null, conflicts);
        }

        var direct = await SensitiveChangeGuard.EnsureAsync(db, guarded.Value.Caller, ApprovalSubjectType.WorkItem, item.Id, ["typeId"], cancellationToken); // 06b R8
        if (direct.IsFailure)
        {
            return direct.Error;
        }

        item.ChangeType(command.TypeId, TypeChangeAnalysis.Merge(item.Fields, command.Fields));
        await ApprovalSupersession.SupersedeAsync(db, ApprovalSubjectType.WorkItem, item.Id, ["typeId"], cancellationToken); // R11 of part 06
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TypeChangeResponse(item.ToResponse(), []);
    }
}

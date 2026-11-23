using System.Text.Json;
using System.Text.RegularExpressions;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Settings;
using Ipm.Application.Common;
using Ipm.Application.Common.Exceptions;
using Ipm.Application.Features.Config.Settings.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Settings.Commands.UpdateSettings;

internal sealed partial class UpdateSettingsCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, IOrganizationSettings settings)
    : ICommandHandler<UpdateSettingsCommand, SettingsResponse>
{
    private static readonly string[] Known =
    [
        "sessionHours", "commentEditHours", "attachmentMaxMb", "attachmentExtensions", "allowCrossProjectRelations", "allowSelfApproval",
        "timeZone", "auditRetentionDays", "projectsPrivateByDefault", "progressIncludedTypeIds",
    ];

    public async ValueTask<Result<SettingsResponse>> Handle(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        if (!caller.Value.Has(Permissions.SettingsManage))
        {
            return AuthErrors.Forbidden;
        }

        var current = await db.Settings.SingleAsync(cancellationToken);
        if (!current.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var errors = new Dictionary<string, string[]>();
        foreach (var key in command.Changes.Keys.Where(k => !Known.Contains(k)))
        {
            errors[key] = ["إعداد غير معروف."];
        }

        var values = current.Values();
        values = values with
        {
            SessionHours = Int(command, "sessionHours", values.SessionHours, 1, 24, "عدد صحيح بين 1 و24 ساعة", errors),
            CommentEditHours = Int(command, "commentEditHours", values.CommentEditHours, 0, 168, "عدد صحيح بين 0 و168 ساعة", errors),
            AttachmentMaxMb = Int(command, "attachmentMaxMb", values.AttachmentMaxMb, 1, 100, "عدد صحيح بين 1 و100 ميغابايت", errors),
            AuditRetentionDays = Int(command, "auditRetentionDays", values.AuditRetentionDays, 365, 36_500, "سنة على الأقل (365 يوماً) وحتى 36500", errors),
            AllowCrossProjectRelations = Flag(command, "allowCrossProjectRelations", values.AllowCrossProjectRelations, errors),
            AllowSelfApproval = Flag(command, "allowSelfApproval", values.AllowSelfApproval, errors),
            ProjectsPrivateByDefault = Flag(command, "projectsPrivateByDefault", values.ProjectsPrivateByDefault, errors),
        };

        if (command.Changes.TryGetValue("timeZone", out var zone))
        {
            if (zone.ValueKind == JsonValueKind.String && RiyadhClock.IsValid(zone.GetString()))
            {
                values = values with { TimeZone = zone.GetString()! };
            }
            else
            {
                errors["timeZone"] = ["منطقة زمنية غير صالحة (مثال: Asia/Riyadh)."];
            }
        }

        if (command.Changes.TryGetValue("attachmentExtensions", out var extensions))
        {
            var parsed = Extensions(extensions, errors);
            if (parsed is not null)
            {
                values = values with { AttachmentExtensions = parsed };
            }
        }

        if (command.Changes.TryGetValue("progressIncludedTypeIds", out var types))
        {
            var parsed = await TypeIdsAsync(types, errors, cancellationToken);
            if (parsed is not null)
            {
                values = values with { ProgressIncludedTypeIds = parsed };
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        current.Apply(values);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await settings.ChangedAsync(cancellationToken);
        return current.ToResponse();
    }

    private static int Int(UpdateSettingsCommand command, string key, int current, int min, int max, string message, Dictionary<string, string[]> errors)
    {
        if (!command.Changes.TryGetValue(key, out var value))
        {
            return current;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= min && number <= max)
        {
            return number;
        }

        errors[key] = [message];
        return current;
    }

    private static bool Flag(UpdateSettingsCommand command, string key, bool current, Dictionary<string, string[]> errors)
    {
        if (!command.Changes.TryGetValue(key, out var value))
        {
            return current;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        errors[key] = ["قيمة منطقية مطلوبة (true أو false)."];
        return current;
    }

    private static List<string>? Extensions(JsonElement value, Dictionary<string, string[]> errors)
    {
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
        {
            errors["attachmentExtensions"] = ["قائمة نصوص مطلوبة."];
            return null;
        }

        var list = value.EnumerateArray().Select(e => e.GetString()!.Trim().TrimStart('.').ToLowerInvariant()).Distinct().ToList();
        if (list.Count == 0)
        {
            errors["attachmentExtensions"] = ["نوع واحد على الأقل."];
            return null;
        }

        if (list.Any(e => SettingsValues.BlockedExtensions.Contains(e) || !ExtensionPattern().IsMatch(e)))
        {
            errors["attachmentExtensions"] = ["لا يُسمح بالملفات التنفيذية (exe، bat، sh…) ولا بامتدادات غير صالحة."];
            return null;
        }

        return list;
    }

    private async Task<List<Guid>?> TypeIdsAsync(JsonElement value, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String || !e.TryGetGuid(out _)))
        {
            errors["progressIncludedTypeIds"] = ["قائمة معرّفات أنواع عمل مطلوبة."];
            return null;
        }

        var ids = value.EnumerateArray().Select(e => e.GetGuid()).Distinct().ToList();
        var existing = await db.WorkItemTypes.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => t.Id).ToListAsync(cancellationToken);
        if (existing.Count != ids.Count)
        {
            errors["progressIncludedTypeIds"] = ["أنواع عمل غير معروفة."];
            return null;
        }

        return ids;
    }

    [GeneratedRegex("^[a-z0-9]{1,10}$")]
    private static partial Regex ExtensionPattern();
}

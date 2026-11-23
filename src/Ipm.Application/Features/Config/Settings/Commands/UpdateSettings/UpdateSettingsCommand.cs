using System.Text.Json;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Settings.Common;

namespace Ipm.Application.Features.Config.Settings.Commands.UpdateSettings;

/// <summary>Spec 09 R2 to R5 (settings.manage, If-Match): changes only the keys sent. An unknown key is a 400.</summary>
public sealed record UpdateSettingsCommand(IReadOnlyDictionary<string, JsonElement> Changes, uint ExpectedVersion) : ICommand<SettingsResponse>;

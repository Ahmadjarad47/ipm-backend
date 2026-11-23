using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Settings.Common;

namespace Ipm.Application.Features.Config.Settings.Queries.GetSettings;

/// <summary>Spec 09 R1: the organization settings, for any signed-in user.</summary>
public sealed record GetSettingsQuery : IQuery<SettingsResponse>;

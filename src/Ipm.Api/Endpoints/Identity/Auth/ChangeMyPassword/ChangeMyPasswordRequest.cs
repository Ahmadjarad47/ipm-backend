namespace Ipm.Api.Endpoints.Identity.Auth.ChangeMyPassword;

public sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);

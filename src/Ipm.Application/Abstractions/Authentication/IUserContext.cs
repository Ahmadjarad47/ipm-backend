namespace Ipm.Application.Abstractions.Authentication;

/// <summary>The user executing the current request.</summary>
public interface IUserContext
{
    bool IsAuthenticated { get; }

    /// <summary>Null for anonymous requests and background jobs.</summary>
    Guid? UserId { get; }
}

namespace Ipm.Application.Abstractions.Authentication;

/// <summary>The user executing the current request.</summary>
public interface IUserContext
{
    bool IsAuthenticated { get; }

    /// <summary>False for the seed and background work: nobody asked for the change, so nobody is notified about it.</summary>
    bool InRequest { get; }

    /// <summary>Null for anonymous requests and background jobs.</summary>
    Guid? UserId { get; }
}

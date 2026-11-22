namespace Ipm.Api.Endpoints;

/// <summary>
/// One HTTP endpoint mapped to one command or query. Implementations live next to their
/// module (Endpoints/&lt;Module&gt;/...) and are discovered automatically at startup.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}

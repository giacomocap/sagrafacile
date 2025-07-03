namespace SagraFacile.NET.API.Services.Interfaces;

public interface ISubscriptionService
{
    Task<bool> IsSubscriptionActiveAsync(Guid organizationId);
}

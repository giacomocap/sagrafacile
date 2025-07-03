using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services;

public class OpenSourceSubscriptionService : ISubscriptionService
{
    public Task<bool> IsSubscriptionActiveAsync(Guid organizationId) => Task.FromResult(true);
}

using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SagraFacile.NET.API.Services.SaaS;

// For local testing, this will be a mock. In production, it will call Stripe.
public class SaaSSubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;

    public SaaSSubscriptionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsSubscriptionActiveAsync(Guid organizationId)
    {
        var org = await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId);

        // In a real scenario, we would check a "SubscriptionStatus" field.
        // For now, we'll assume any organization existing in the DB is active.
        // This will be expanded later to check a real status from Stripe.
        return org != null;
    }
}

using LeaveLite.Application.Abstractions;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LeaveLite.Infrastructure.Persistence.Repositories;

internal sealed class AccrualPolicyRepository(LeaveLiteDbContext context) : IAccrualPolicyRepository
{
    public Task<AccrualPolicy?> GetByIdAsync(AccrualPolicyId id, CancellationToken cancellationToken = default)
        => context.AccrualPolicies.FirstOrDefaultAsync(policy => policy.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AccrualPolicy>> ListAsync(CancellationToken cancellationToken = default)
        => await context.AccrualPolicies
            .OrderBy(policy => policy.Name)
            .ToListAsync(cancellationToken);
}

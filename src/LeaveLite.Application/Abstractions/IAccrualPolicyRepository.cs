using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.Abstractions;

/// <summary>Persistence abstraction for <see cref="AccrualPolicy"/> configuration entities.</summary>
public interface IAccrualPolicyRepository
{
    Task<AccrualPolicy?> GetByIdAsync(AccrualPolicyId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccrualPolicy>> ListAsync(CancellationToken cancellationToken = default);
}

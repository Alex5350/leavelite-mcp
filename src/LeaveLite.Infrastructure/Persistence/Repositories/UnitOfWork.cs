using LeaveLite.Application.Abstractions;

namespace LeaveLite.Infrastructure.Persistence.Repositories;

/// <summary>Commits everything tracked by the scoped <see cref="Persistence.LeaveLiteDbContext"/>.</summary>
internal sealed class UnitOfWork(LeaveLiteDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}

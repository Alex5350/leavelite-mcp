namespace LeaveLite.Application.Abstractions;

/// <summary>Commits all changes tracked by the repositories in the current unit of work.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

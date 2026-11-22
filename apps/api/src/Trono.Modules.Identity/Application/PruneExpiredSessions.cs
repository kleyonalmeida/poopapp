namespace Trono.Modules.Identity.Application;

public interface ISessionRetentionStore
{
    Task<int> DeleteExpiredBatchAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);
}

public sealed class PruneExpiredSessions(ISessionRetentionStore store, TimeProvider clock)
{
    public Task<int> ExecuteAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        cancellationToken.ThrowIfCancellationRequested();
        return store.DeleteExpiredBatchAsync(clock.GetUtcNow().ToUniversalTime(), batchSize, cancellationToken);
    }
}

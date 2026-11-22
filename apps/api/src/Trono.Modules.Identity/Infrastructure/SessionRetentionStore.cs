using Trono.Modules.Identity.Application;
using Microsoft.EntityFrameworkCore;

namespace Trono.Modules.Identity.Infrastructure;

internal sealed class SessionRetentionStore(IdentityDbContext database) : ISessionRetentionStore
{
    public Task<int> DeleteExpiredBatchAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlInterpolatedAsync($"""
            WITH expired AS (
                SELECT "Id" FROM identity."Sessions"
                WHERE "ExpiresAt" <= {cutoff}
                ORDER BY "ExpiresAt", "Id"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            )
            DELETE FROM identity."Sessions" AS session
            USING expired
            WHERE session."Id" = expired."Id" AND session."ExpiresAt" <= {cutoff}
            """, cancellationToken);
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trono.Modules.Records.Application;
using Trono.Modules.Records.Domain;

namespace Trono.Modules.Records.Infrastructure;

internal sealed class OwnRecordStore(RecordsDbContext database) : IOwnRecordStore
{
    public async Task<RecordView?> FindAsync(Guid accountId, Guid id, CancellationToken cancellationToken)
    {
        var record = await database.Records.AsNoTracking().SingleOrDefaultAsync(value => value.AccountId == accountId && value.Id == id, cancellationToken);
        return record is null ? null : RecordView.From(record);
    }
    public async Task<RecordPage> ListAsync(Guid accountId, int offset, int limit, CancellationToken cancellationToken)
    {
        var records = await database.Records.AsNoTracking().Where(value => value.AccountId == accountId)
            .OrderByDescending(value => value.OccurredAt).ThenByDescending(value => value.Id)
            .Skip(offset).Take(limit + 1).ToListAsync(cancellationToken);
        return new RecordPage(records.Take(limit).Select(RecordView.From).ToArray(), records.Count > limit);
    }
    public async Task<RecordCreation> CreateAsync(Guid accountId, Guid key, RecordContent content, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Explicit account-ID contract; the same lock order as account deletion prevents orphan writes.
        var owner = await database.AccountReferences.FromSqlInterpolated(
            $"""SELECT "Id" FROM identity."AspNetUsers" WHERE "Id" = {accountId} FOR UPDATE""")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return new RecordCreation(CreationStatus.AccountGone);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content))));
        var receipt = await database.CreationReceipts.FindAsync([accountId, key], cancellationToken);
        if (receipt is not null)
        {
            if (receipt.ContentHash != hash) return new RecordCreation(CreationStatus.Conflict);
            var existing = await FindAsync(accountId, receipt.RecordId, cancellationToken);
            return new RecordCreation(existing is null ? CreationStatus.Gone : CreationStatus.Replayed, existing);
        }
        var record = new PrivateRecord(accountId, content);
        database.Records.Add(record);
        database.CreationReceipts.Add(new CreationReceipt { AccountId = accountId, Key = key, RecordId = record.Id, ContentHash = hash });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RecordCreation(CreationStatus.Created, RecordView.From(record));
    }
    public async Task<RecordView?> UpdateAsync(Guid accountId, Guid id, RecordContent content, CancellationToken cancellationToken)
    {
        var count = await database.Records.Where(value => value.AccountId == accountId && value.Id == id)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.OccurredAt, content.OccurredAt)
                .SetProperty(value => value.DurationSeconds, content.DurationSeconds).SetProperty(value => value.Type, content.Type)
                .SetProperty(value => value.Notes, content.Notes), cancellationToken);
        return count == 0 ? null : new RecordView(id, content.OccurredAt, content.DurationSeconds, content.Type, content.Notes);
    }
    public async Task<bool> DeleteAsync(Guid accountId, Guid id, CancellationToken cancellationToken) =>
        await database.Records.Where(value => value.AccountId == accountId && value.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}

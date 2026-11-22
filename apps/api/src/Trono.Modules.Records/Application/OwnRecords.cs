using System.Globalization;
using System.Text.RegularExpressions;
using Trono.Modules.Records.Domain;

namespace Trono.Modules.Records.Application;

public sealed record RecordView(Guid Id, DateTimeOffset OccurredAt, int? DurationSeconds, string? Type, string? Notes)
{
    public static RecordView From(PrivateRecord record) => new(record.Id, record.OccurredAt, record.DurationSeconds, record.Type, record.Notes);
}
public sealed record RecordPage(IReadOnlyList<RecordView> Items, bool HasMore);
public enum CreationStatus { Created, Replayed, Conflict, Gone, AccountGone }
public sealed record RecordCreation(CreationStatus Status, RecordView? Record = null);
public interface IOwnRecordStore
{
    Task<RecordView?> FindAsync(Guid accountId, Guid id, CancellationToken cancellationToken);
    Task<RecordPage> ListAsync(Guid accountId, int offset, int limit, CancellationToken cancellationToken);
    Task<RecordCreation> CreateAsync(Guid accountId, Guid key, RecordContent content, CancellationToken cancellationToken);
    Task<RecordView?> UpdateAsync(Guid accountId, Guid id, RecordContent content, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid accountId, Guid id, CancellationToken cancellationToken);
}

public sealed class OwnRecords(IOwnRecordStore store, TimeProvider clock)
{
    public Task<RecordView?> FindAsync(Guid accountId, Guid id, CancellationToken cancellationToken) => store.FindAsync(accountId, id, cancellationToken);
    public Task<bool> DeleteAsync(Guid accountId, Guid id, CancellationToken cancellationToken) => store.DeleteAsync(accountId, id, cancellationToken);
    public Task<RecordPage> ListAsync(Guid accountId, int offset, int limit, CancellationToken cancellationToken)
    {
        if (offset is < 0 or > 100000 || limit is < 1 or > 50)
            throw new RecordInputException("pagination", "Use offset entre 0 e 100000 e limit entre 1 e 50.");
        return store.ListAsync(accountId, offset, limit, cancellationToken);
    }
    public Task<RecordCreation> CreateAsync(Guid accountId, Guid key, string? occurredAt, int? duration, string? type, string? notes, CancellationToken cancellationToken)
    {
        if (key == Guid.Empty) throw new RecordInputException("Idempotency-Key", "Informe uma chave UUID para este envio.");
        var content = Parse(occurredAt, duration, type, notes);
        return store.CreateAsync(accountId, key, content, cancellationToken);
    }
    public async Task<RecordView?> UpdateAsync(Guid accountId, Guid id, string? occurredAt, int? duration, string? type, string? notes, CancellationToken cancellationToken)
    {
        var previous = await store.FindAsync(accountId, id, cancellationToken);
        if (previous is null) return null;
        var content = Parse(occurredAt, duration, type, notes, previous.OccurredAt);
        return await store.UpdateAsync(accountId, id, content, cancellationToken);
    }
    private RecordContent Parse(string? occurredAt, int? duration, string? type, string? notes, DateTimeOffset? previous = null)
    {
        if (occurredAt is null || occurredAt.Length > 40 || !Regex.IsMatch(occurredAt, @"(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)
            || !DateTimeOffset.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
            throw new RecordInputException("occurredAt", "Informe data e hora com fuso horário.");
        return RecordContent.Create(instant, duration, type, notes, clock.GetUtcNow(), previous);
    }
}

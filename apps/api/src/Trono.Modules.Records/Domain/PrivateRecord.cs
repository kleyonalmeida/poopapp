namespace Trono.Modules.Records.Domain;

public sealed class RecordInputException(string field, string message) : Exception("Registro inválido.")
{
    public IDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]> { [field] = [message] };
}

public sealed record RecordContent
{
    private RecordContent(DateTimeOffset occurredAt, int? durationSeconds, string? type, string? notes)
        => (OccurredAt, DurationSeconds, Type, Notes) = (occurredAt, durationSeconds, type, notes);

    public DateTimeOffset OccurredAt { get; }
    public int? DurationSeconds { get; }
    public string? Type { get; }
    public string? Notes { get; }

    public static RecordContent Create(DateTimeOffset occurredAt, int? durationSeconds, string? type, string? notes,
        DateTimeOffset now, DateTimeOffset? previousOccurrence = null)
    {
        // PostgreSQL stores microseconds. Normalize before comparison and idempotency hashing.
        occurredAt = new DateTimeOffset(occurredAt.UtcTicks / 10 * 10, TimeSpan.Zero);
        if (occurredAt > now.AddMinutes(5) || (occurredAt < now.AddDays(-30) && occurredAt != previousOccurrence))
            throw new RecordInputException("occurredAt", "Escolha uma data nos últimos 30 dias, sem horário futuro.");
        if (durationSeconds is < 1 or > 86400)
            throw new RecordInputException("durationSeconds", "Informe entre 1 e 86400 segundos, ou deixe vazio.");
        if (type is not null and not ("bode" or "serpentao" or "torazona" or "cachoeira" or "pedregulhos" or "erupcao"))
            throw new RecordInputException("type", "Escolha uma descrição do catálogo, ou deixe vazio.");
        notes = notes?.Trim();
        if (notes?.Length > 280 || notes?.Any(character => char.IsControl(character) && character is not '\n' and not '\r') == true)
            throw new RecordInputException("notes", "A história aceita até 280 caracteres, sem controles especiais.");
        return new RecordContent(occurredAt, durationSeconds, type, string.IsNullOrEmpty(notes) ? null : notes);
    }
}

public sealed class PrivateRecord
{
    private PrivateRecord() { }
    public PrivateRecord(Guid accountId, RecordContent content)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Conta obrigatória.", nameof(accountId));
        Id = Guid.NewGuid();
        AccountId = accountId;
        Update(content);
    }
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? Type { get; private set; }
    public string? Notes { get; private set; }
    public void Update(RecordContent content)
        => (OccurredAt, DurationSeconds, Type, Notes) = (content.OccurredAt, content.DurationSeconds, content.Type, content.Notes);
}

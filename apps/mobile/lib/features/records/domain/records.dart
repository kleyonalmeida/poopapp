class RecordDraft {
  const RecordDraft({
    required this.occurredAt,
    this.durationSeconds,
    this.type,
    this.notes,
  });
  final DateTime occurredAt;
  final int? durationSeconds;
  final String? type;
  final String? notes;
  Map<String, dynamic> toJson() => {
    'occurredAt': occurredAt.toUtc().toIso8601String(),
    'durationSeconds': durationSeconds,
    'type': type,
    'notes': notes,
  };
}

class RecordEntry {
  const RecordEntry(this.id, this.content);
  final String id;
  final RecordDraft content;
}

class RecordPage {
  const RecordPage(this.items, this.hasMore);
  final List<RecordEntry> items;
  final bool hasMore;
}

abstract interface class RecordsRepository {
  Future<RecordPage> list(int offset);
  Future<RecordEntry> create(RecordDraft draft, String key);
  Future<RecordEntry> update(String id, RecordDraft draft);
  Future<void> delete(String id);
}

const recordTypes = <String, String>{
  'bode': 'Bode',
  'serpentao': 'Serpentão',
  'torazona': 'Torazona',
  'cachoeira': 'Cachoeira',
  'pedregulhos': 'Pedregulhos',
  'erupcao': 'Erupção',
};

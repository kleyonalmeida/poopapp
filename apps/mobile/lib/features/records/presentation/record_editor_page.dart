import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../auth/presentation/auth_providers.dart';
import '../data/dio_records_repository.dart';
import '../domain/records.dart';
import 'records_provider.dart';
import 'records_page.dart';

class RecordEditorPage extends ConsumerStatefulWidget {
  const RecordEditorPage({super.key, this.entry});
  final RecordEntry? entry;
  @override
  ConsumerState<RecordEditorPage> createState() => _RecordEditorPageState();
}

class _RecordEditorPageState extends ConsumerState<RecordEditorPage> {
  final _form = GlobalKey<FormState>();
  final _notes = TextEditingController();
  final _duration = TextEditingController();
  late DateTime _at;
  late String _key;
  String? _type;
  String? _owner;
  String? _error;
  bool _busy = false;
  bool get _current =>
      mounted && ref.read(authControllerProvider).account?.id == _owner;
  @override
  void initState() {
    super.initState();
    final draft = widget.entry?.content;
    _at = draft?.occurredAt.toLocal() ?? DateTime.now();
    _notes.text = draft?.notes ?? '';
    _duration.text = draft?.durationSeconds?.toString() ?? '';
    _type = draft?.type;
    _key = newRecordKey();
    _owner = ref.read(authControllerProvider).account?.id;
  }

  @override
  void dispose() {
    _notes.dispose();
    _duration.dispose();
    super.dispose();
  }

  Future<void> _date() async {
    final now = DateTime.now();
    final earliest = now.subtract(const Duration(days: 30));
    final first = _at.isBefore(earliest) ? _at : earliest;
    final date = await showDatePicker(
      context: context,
      initialDate: _at,
      firstDate: first,
      lastDate: now,
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_at),
    );
    if (time == null || !mounted) return;
    setState(
      () => _at = DateTime(
        date.year,
        date.month,
        date.day,
        time.hour,
        time.minute,
      ),
    );
  }

  Future<void> _save() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final notes = _notes.text.trim();
    final content = RecordDraft(
      occurredAt: _at,
      durationSeconds: int.tryParse(_duration.text),
      type: _type,
      notes: notes.isEmpty ? null : notes,
    );
    try {
      final repository = ref.read(recordsRepositoryProvider);
      if (widget.entry == null) {
        await repository.create(content, _key);
      } else {
        await repository.update(widget.entry!.id, content);
      }
      if (mounted && _current) context.pop(true);
    } catch (error) {
      if (!_current) return;
      if (recordSessionExpired(error)) {
        await ref.read(authControllerProvider.notifier).restore();
      } else {
        setState(
          () => _error =
              'Não foi possível confirmar o salvamento. Confira os dados e o histórico antes de tentar novamente.',
        );
      }
    } finally {
      if (_current) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (ref.watch(authControllerProvider).account?.id != _owner) {
      return const Scaffold(body: SizedBox.shrink());
    }
    return Scaffold(
      appBar: AppBar(
        title: Text(
          widget.entry == null ? 'Registrar minha cagada' : 'Editar registro',
        ),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 520),
              child: Form(
                key: _form,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text(
                      'Só você vê este registro. Uma história épica é opcional.',
                    ),
                    const SizedBox(height: 16),
                    OutlinedButton(
                      onPressed: _busy ? null : _date,
                      child: Text('Data e hora: ${recordDate(_at)}'),
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<String>(
                      initialValue: _type ?? '',
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'Como foi? (opcional)',
                      ),
                      items: [
                        const DropdownMenuItem(
                          value: '',
                          child: Text('Sem descrição'),
                        ),
                        for (final type in recordTypes.entries)
                          DropdownMenuItem(
                            value: type.key,
                            child: Text(type.value),
                          ),
                      ],
                      onChanged: _busy
                          ? null
                          : (value) => setState(
                              () => _type = value == '' ? null : value,
                            ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      key: const Key('record-duration'),
                      controller: _duration,
                      enabled: !_busy,
                      decoration: const InputDecoration(
                        labelText: 'Duração em segundos (opcional)',
                      ),
                      keyboardType: TextInputType.number,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      validator: (value) =>
                          value != null &&
                              value.isNotEmpty &&
                              (int.tryParse(value) == null ||
                                  int.parse(value) < 1 ||
                                  int.parse(value) > 86400)
                          ? 'Use entre 1 e 86400 segundos.'
                          : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      key: const Key('record-notes'),
                      controller: _notes,
                      enabled: !_busy,
                      decoration: const InputDecoration(
                        labelText: 'Conte sua história (opcional)',
                      ),
                      minLines: 2,
                      maxLines: 5,
                      maxLength: 280,
                      validator: (value) =>
                          value != null && value.trim().length > 280
                          ? 'Use até 280 caracteres.'
                          : null,
                    ),
                    if (_error != null)
                      Semantics(liveRegion: true, child: Text(_error!)),
                    if (_busy) const LinearProgressIndicator(),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: _busy ? null : _save,
                      child: const Text('Salvar registro privado'),
                    ),
                    TextButton(
                      onPressed: _busy ? null : () => context.pop(true),
                      child: const Text('Voltar ao histórico'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

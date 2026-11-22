import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../auth/presentation/auth_providers.dart';
import '../domain/records.dart';
import 'records_provider.dart';

String recordDate(DateTime value) {
  final date = value.toLocal();
  String two(int number) => number.toString().padLeft(2, '0');
  return '${two(date.day)}/${two(date.month)}/${date.year} ${two(date.hour)}:${two(date.minute)}';
}

class RecordsPage extends ConsumerStatefulWidget {
  const RecordsPage({super.key});
  @override
  ConsumerState<RecordsPage> createState() => _RecordsPageState();
}

class _RecordsPageState extends ConsumerState<RecordsPage> {
  final _items = <RecordEntry>[];
  bool _busy = true;
  bool _hasMore = false;
  String? _error;
  String? _owner;
  bool get _current =>
      mounted && ref.read(authControllerProvider).account?.id == _owner;
  @override
  void initState() {
    super.initState();
    _owner = ref.read(authControllerProvider).account?.id;
    Future.microtask(() => _load());
  }

  Future<void> _load({bool more = false}) async {
    if (!_current) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final result = await ref
          .read(recordsRepositoryProvider)
          .list(more ? _items.length : 0);
      if (!_current) return;
      setState(() {
        if (!more) _items.clear();
        _items.addAll(result.items);
        _hasMore = result.hasMore;
      });
    } catch (error) {
      if (!_current) return;
      if (recordSessionExpired(error)) {
        await ref.read(authControllerProvider.notifier).restore();
      } else {
        setState(
          () => _error =
              'Não foi possível carregar os registros. Tente novamente.',
        );
      }
    } finally {
      if (_current) setState(() => _busy = false);
    }
  }

  Future<void> _edit([RecordEntry? entry]) async {
    final saved = await context.push<bool>(
      entry == null ? '/registros/novo' : '/registros/editar',
      extra: entry,
    );
    if (saved == true && _current) await _load();
  }

  Future<void> _delete(RecordEntry entry) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        scrollable: true,
        title: const Text('Excluir este registro?'),
        content: const Text(
          'Esta história será removida do seu trono. Não é possível desfazer.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Excluir agora'),
          ),
        ],
      ),
    );
    if (confirmed != true || !_current || _busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(recordsRepositoryProvider).delete(entry.id);
      if (_current) await _load();
    } catch (error) {
      if (!_current) return;
      if (recordSessionExpired(error)) {
        await ref.read(authControllerProvider.notifier).restore();
      } else {
        setState(
          () => _error =
              'Não foi possível confirmar a exclusão. Atualize o histórico para verificar.',
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
        title: const Text('Meu trono'),
        leading: IconButton(
          tooltip: 'Minha conta',
          onPressed: () => context.go('/conta'),
          icon: const Icon(Icons.arrow_back),
        ),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            const Text(
              'Histórias do seu trono',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            const Text('Só você vê estes registros. Solte a criatividade.'),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _busy ? null : () => _edit(),
              child: const Text('Registrar minha cagada'),
            ),
            TextButton(
              onPressed: _busy ? null : () => _load(),
              child: const Text('Atualizar histórico'),
            ),
            if (_busy) const LinearProgressIndicator(),
            if (_error != null)
              Semantics(liveRegion: true, child: Text(_error!)),
            if (!_busy && _error == null && _items.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 32),
                child: Text('Seu trono ainda não tem histórias.'),
              ),
            for (final item in _items)
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(recordDate(item.content.occurredAt)),
                      if (item.content.type != null)
                        Text(
                          recordTypes[item.content.type]!,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      if (item.content.notes != null) Text(item.content.notes!),
                      if (item.content.durationSeconds != null)
                        Text(
                          'Duração: ${item.content.durationSeconds} segundos',
                        ),
                      Wrap(
                        children: [
                          IconButton(
                            tooltip: 'Editar registro',
                            onPressed: _busy ? null : () => _edit(item),
                            icon: const Icon(Icons.edit_outlined),
                          ),
                          IconButton(
                            tooltip: 'Excluir registro',
                            onPressed: _busy ? null : () => _delete(item),
                            icon: const Icon(Icons.delete_outline),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            if (_hasMore)
              TextButton(
                onPressed: _busy ? null : () => _load(more: true),
                child: const Text('Carregar mais'),
              ),
          ],
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'auth_providers.dart';

class OwnProfilePage extends ConsumerStatefulWidget {
  const OwnProfilePage({super.key});

  @override
  ConsumerState<OwnProfilePage> createState() => _OwnProfilePageState();
}

class _OwnProfilePageState extends ConsumerState<OwnProfilePage> {
  final _form = GlobalKey<FormState>();
  String? _nickname;

  Future<void> _save() async {
    final form = _form.currentState!;
    if (!form.validate()) return;
    form.save();
    FocusScope.of(context).unfocus();
    final saved = await ref
        .read(authControllerProvider.notifier)
        .saveNickname(_nickname);
    if (saved && mounted) context.go('/conta');
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    final account = auth.account;
    if (account == null) return const Scaffold(body: SizedBox.shrink());
    return PopScope(
      canPop: !auth.busy,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Editar apelido'),
          leading: IconButton(
            tooltip: 'Voltar',
            icon: const Icon(Icons.arrow_back),
            onPressed: auth.busy ? null : () => context.go('/conta'),
          ),
        ),
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 480),
                child: Form(
                  key: _form,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      TextFormField(
                        key: const Key('profile-nickname'),
                        initialValue: account.nickname,
                        readOnly: auth.busy,
                        textCapitalization: TextCapitalization.words,
                        textInputAction: TextInputAction.done,
                        decoration: const InputDecoration(
                          labelText: 'Apelido',
                          helperText: 'Deixe em branco para remover.',
                        ),
                        validator: (value) => (value?.trim().length ?? 0) > 40
                            ? 'Use até 40 caracteres.'
                            : null,
                        onSaved: (value) {
                          final nickname = value?.trim() ?? '';
                          _nickname = nickname.isEmpty ? null : nickname;
                        },
                        onFieldSubmitted: auth.busy ? null : (_) => _save(),
                      ),
                      const SizedBox(height: 24),
                      if (auth.message != null)
                        Semantics(liveRegion: true, child: Text(auth.message!)),
                      if (auth.busy) const LinearProgressIndicator(),
                      FilledButton(
                        onPressed: auth.busy ? null : _save,
                        child: const Text('Salvar apelido'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

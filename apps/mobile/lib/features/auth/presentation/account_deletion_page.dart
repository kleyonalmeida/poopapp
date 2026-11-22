import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'auth_providers.dart';

class AccountDeletionPage extends ConsumerStatefulWidget {
  const AccountDeletionPage({super.key});
  @override
  ConsumerState<AccountDeletionPage> createState() =>
      _AccountDeletionPageState();
}

class _AccountDeletionPageState extends ConsumerState<AccountDeletionPage> {
  final _form = GlobalKey<FormState>();
  final _password = TextEditingController();
  bool _consent = false;
  @override
  void initState() {
    super.initState();
    _password.addListener(_changed);
  }

  void _changed() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_consent || !_form.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        scrollable: true,
        title: const Text('Excluir definitivamente?'),
        content: const Text(
          'Sua conta será apagada e todas as sessões serão encerradas. Esta ação não pode ser desfeita.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Cancelar'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Excluir definitivamente'),
          ),
        ],
      ),
    );
    if (!mounted || confirmed != true) return;
    await ref
        .read(authControllerProvider.notifier)
        .deleteAccount(_password.text);
    if (mounted) _password.clear();
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    if (auth.account == null) return const Scaffold(body: SizedBox.shrink());
    final colors = Theme.of(context).colorScheme;
    return PopScope(
      canPop: !auth.busy,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Excluir conta'),
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
                      Text(
                        'Esta ação é definitiva.',
                        style: Theme.of(context).textTheme.headlineMedium,
                      ),
                      const SizedBox(height: 16),
                      const Text(
                        'Ao excluir sua conta, seus dados de acesso serão apagados e todas as sessões serão encerradas. Para continuar, confirme sua senha.',
                      ),
                      const SizedBox(height: 24),
                      if (auth.message != null) ...[
                        Semantics(
                          liveRegion: true,
                          child: Text(
                            auth.message!,
                            style: TextStyle(color: colors.error),
                          ),
                        ),
                        const SizedBox(height: 16),
                      ],
                      TextFormField(
                        key: const Key('delete-password'),
                        controller: _password,
                        enabled: !auth.busy,
                        obscureText: true,
                        autocorrect: false,
                        enableSuggestions: false,
                        autofillHints: const [AutofillHints.password],
                        decoration: const InputDecoration(
                          labelText: 'Senha atual',
                          border: OutlineInputBorder(),
                        ),
                        validator: (value) =>
                            value == null || value.isEmpty || value.length > 128
                            ? 'Confirme sua senha, com até 128 caracteres.'
                            : null,
                      ),
                      const SizedBox(height: 16),
                      CheckboxListTile(
                        key: const Key('delete-consent'),
                        contentPadding: EdgeInsets.zero,
                        controlAffinity: ListTileControlAffinity.leading,
                        value: _consent,
                        onChanged: auth.busy
                            ? null
                            : (value) =>
                                  setState(() => _consent = value ?? false),
                        title: const Text(
                          'Entendo que a exclusão é definitiva.',
                        ),
                      ),
                      const SizedBox(height: 24),
                      FilledButton(
                        key: const Key('delete-submit'),
                        style: FilledButton.styleFrom(
                          backgroundColor: colors.error,
                          foregroundColor: colors.onError,
                        ),
                        onPressed:
                            auth.busy || !_consent || _password.text.isEmpty
                            ? null
                            : _submit,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 12),
                          child: auth.busy
                              ? const SizedBox(
                                  width: 24,
                                  height: 24,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Text('Excluir conta'),
                        ),
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

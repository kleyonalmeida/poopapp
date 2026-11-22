import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'auth_providers.dart';
import 'package:go_router/go_router.dart';

class AccountPage extends ConsumerWidget {
  const AccountPage({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final account = auth.account;
    if (account == null) return const Scaffold(body: SizedBox.shrink());
    return Scaffold(
      appBar: AppBar(title: const Text('Minha conta')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Seu espaço está pronto.',
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                  const SizedBox(height: 24),
                  Text(
                    account.email,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  if (account.nickname case final nickname?) ...[
                    const SizedBox(height: 8),
                    Text(nickname),
                  ],
                  const SizedBox(height: 8),
                  Text(
                    account.emailConfirmed
                        ? 'E-mail confirmado.'
                        : 'E-mail ainda não confirmado.',
                  ),
                  const SizedBox(height: 24),
                  const Text(
                    'Seus registros são privados por padrão. Você decide o que compartilhar e com quem.',
                  ),
                  const SizedBox(height: 24),
                  if (auth.message != null)
                    Semantics(liveRegion: true, child: Text(auth.message!)),
                  if (auth.busy) const LinearProgressIndicator(),
                  const SizedBox(height: 16),
                  OutlinedButton(
                    onPressed: auth.busy ? null : () => context.go('/perfil'),
                    child: const Text('Editar apelido'),
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton(
                    onPressed: auth.busy
                        ? null
                        : () => context.go('/preferencias'),
                    child: const Text('Preferências'),
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton(
                    onPressed: auth.busy
                        ? null
                        : () => ref
                              .read(authControllerProvider.notifier)
                              .reload(),
                    child: const Text('Atualizar conta'),
                  ),
                  const SizedBox(height: 12),
                  TextButton(
                    onPressed: () =>
                        ref.read(authControllerProvider.notifier).logout(),
                    child: const Text('Sair'),
                  ),
                  const SizedBox(height: 12),
                  TextButton(
                    onPressed: auth.busy
                        ? null
                        : () => context.go('/excluir-conta'),
                    child: Text(
                      'Excluir minha conta',
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

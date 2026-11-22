import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'auth_providers.dart';

class OwnSettingsPage extends ConsumerWidget {
  const OwnSettingsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final account = auth.account;
    if (account == null) return const Scaffold(body: SizedBox.shrink());

    return PopScope(
      canPop: !auth.busy,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Preferências'),
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
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    SwitchListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Permitir notificações push'),
                      subtitle: const Text(
                        'Sua escolha será usada quando o envio estiver disponível.',
                      ),
                      value: account.pushNotificationsEnabled,
                      onChanged: auth.busy
                          ? null
                          : ref
                                .read(authControllerProvider.notifier)
                                .savePushNotifications,
                    ),
                    if (auth.busy) const LinearProgressIndicator(),
                    if (auth.message != null)
                      Semantics(liveRegion: true, child: Text(auth.message!)),
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

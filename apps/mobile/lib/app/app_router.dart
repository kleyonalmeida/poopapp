import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../features/auth/presentation/auth_providers.dart';
import '../features/auth/presentation/auth_form_page.dart';
import '../features/auth/presentation/account_page.dart';
import '../features/auth/presentation/own_profile_page.dart';
import '../features/auth/presentation/own_settings_page.dart';
import '../features/auth/presentation/account_deletion_page.dart';
import '../features/home/presentation/home_page.dart';
import '../features/records/presentation/records_page.dart';
import '../features/records/presentation/record_editor_page.dart';
import '../features/records/domain/records.dart';

final appRouterProvider = Provider<GoRouter>((ref) {
  final router = GoRouter(
    initialLocation: '/',
    redirect: (context, state) {
      final account = ref.read(authControllerProvider).account;
      if ({
            '/conta',
            '/perfil',
            '/preferencias',
            '/excluir-conta',
          }.contains(state.uri.path) &&
          account == null) {
        return '/entrar';
      }
      if (state.uri.path.startsWith('/registros') && account == null) {
        return '/entrar';
      }
      if (account != null &&
          {'/', '/entrar', '/cadastro'}.contains(state.uri.path)) {
        return '/conta';
      }
      return null;
    },
    routes: [
      GoRoute(
        path: '/registros',
        builder: (context, state) => const RecordsPage(),
      ),
      GoRoute(
        path: '/registros/novo',
        builder: (context, state) => const RecordEditorPage(),
      ),
      GoRoute(
        path: '/registros/editar',
        builder: (context, state) => state.extra is RecordEntry
            ? RecordEditorPage(entry: state.extra! as RecordEntry)
            : const Scaffold(
                body: Center(
                  child: Text('Registro indisponível. Abra seu histórico.'),
                ),
              ),
      ),
      GoRoute(path: '/', builder: (context, state) => const HomePage()),
      GoRoute(
        path: '/entrar',
        builder: (context, state) => const AuthFormPage(register: false),
      ),
      GoRoute(
        path: '/cadastro',
        builder: (context, state) => const AuthFormPage(register: true),
      ),
      GoRoute(path: '/conta', builder: (context, state) => const AccountPage()),
      GoRoute(
        path: '/perfil',
        builder: (context, state) => const OwnProfilePage(),
      ),
      GoRoute(
        path: '/preferencias',
        builder: (context, state) => const OwnSettingsPage(),
      ),
      GoRoute(
        path: '/excluir-conta',
        builder: (context, state) => const AccountDeletionPage(),
      ),
    ],
    errorBuilder: (context, state) => Scaffold(
      body: SafeArea(
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Página não encontrada.'),
              TextButton(
                onPressed: () => context.go('/'),
                child: const Text('Voltar ao início'),
              ),
            ],
          ),
        ),
      ),
    ),
  );
  ref.listen(authControllerProvider, (_, next) => router.refresh());
  ref.onDispose(router.dispose);
  return router;
});

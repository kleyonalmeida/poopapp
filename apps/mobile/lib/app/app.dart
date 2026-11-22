import 'package:flutter/material.dart';
import '../features/auth/domain/auth_session.dart';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../features/auth/presentation/auth_providers.dart';
import 'app_router.dart';

class MyApp extends StatelessWidget {
  const MyApp({super.key, this.session});
  final AuthSession? session;

  @override
  Widget build(BuildContext context) {
    return ProviderScope(
      overrides: [
        if (session != null) authSessionProvider.overrideWithValue(session!),
      ],
      child: const TronoApp(),
    );
  }
}

class TronoApp extends ConsumerWidget {
  const TronoApp({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return MaterialApp.router(
      title: 'TRONO',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF456747)),
      ),
      darkTheme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF456747),
          brightness: Brightness.dark,
        ),
      ),
      routerConfig: ref.watch(appRouterProvider),
    );
  }
}

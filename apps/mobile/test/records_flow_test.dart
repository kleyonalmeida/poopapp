import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trono/app/app.dart';
import 'package:trono/app/app_router.dart';
import 'package:trono/features/auth/domain/auth_models.dart';
import 'package:trono/features/auth/domain/auth_session.dart';
import 'package:trono/features/auth/presentation/auth_providers.dart';
import 'package:trono/features/records/domain/records.dart';
import 'package:trono/features/records/presentation/records_provider.dart';
import 'support/auth_fakes.dart';

class FakeRecords implements RecordsRepository {
  final items = <RecordEntry>[];
  int creates = 0;
  Completer<void>? saveGate;
  bool fail = false;
  Completer<RecordPage>? listGate;
  @override
  Future<RecordPage> list(int offset) async => listGate == null
      ? RecordPage(items.skip(offset).toList(), false)
      : listGate!.future;
  @override
  Future<RecordEntry> create(RecordDraft draft, String key) async {
    creates++;
    await saveGate?.future;
    if (fail) throw const AuthFailure(AuthFailureKind.network);
    final entry = RecordEntry('11111111-1111-4111-8111-111111111111', draft);
    items.add(entry);
    return entry;
  }

  @override
  Future<RecordEntry> update(String id, RecordDraft draft) async {
    final index = items.indexWhere((item) => item.id == id);
    return items[index] = RecordEntry(id, draft);
  }

  @override
  Future<void> delete(String id) async =>
      items.removeWhere((item) => item.id == id);
}

void main() {
  late FakeRecords records;
  late ProviderContainer container;
  Future<void> open(WidgetTester tester, {bool loggedIn = true}) async {
    records = FakeRecords();
    final api = FakeAuthApi(DateTime.now);
    final session = AuthSession(api, MemoryVault());
    if (loggedIn) {
      await session.login('pessoa@example.com', 'senha longa privada');
    }
    container = ProviderContainer(
      overrides: [
        authSessionProvider.overrideWithValue(session),
        recordsRepositoryProvider.overrideWithValue(records),
      ],
    );
    addTearDown(container.dispose);
    await tester.pumpWidget(
      UncontrolledProviderScope(container: container, child: const TronoApp()),
    );
    await tester.pumpAndSettle();
  }

  Future<void> history(WidgetTester tester) async {
    await tester.ensureVisible(find.text('Meus registros'));
    await tester.tap(find.text('Meus registros'));
    await tester.pumpAndSettle();
  }

  testWidgets('Cria, edita e exclui um registro privado com confirmação', (
    tester,
  ) async {
    await open(tester);
    await history(tester);
    expect(find.text('Seu trono ainda não tem histórias.'), findsOneWidget);
    await tester.tap(find.text('Registrar minha cagada'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Só você'), findsOneWidget);
    await tester.enterText(
      find.byKey(const Key('record-notes')),
      'A lenda da porcelana',
    );
    await tester.ensureVisible(find.text('Salvar registro privado'));
    await tester.tap(find.text('Salvar registro privado'));
    await tester.pumpAndSettle();
    expect(records.creates, 1);
    expect(find.text('A lenda da porcelana'), findsOneWidget);
    await tester.tap(find.byTooltip('Editar registro'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('record-notes')),
      'História revisada',
    );
    await tester.ensureVisible(find.text('Salvar registro privado'));
    await tester.tap(find.text('Salvar registro privado'));
    await tester.pumpAndSettle();
    expect(find.text('História revisada'), findsOneWidget);
    await tester.tap(find.byTooltip('Excluir registro'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancelar'));
    await tester.pumpAndSettle();
    expect(records.items, hasLength(1));
    await tester.tap(find.byTooltip('Excluir registro'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Excluir agora'));
    await tester.pumpAndSettle();
    expect(records.items, isEmpty);
    expect(find.text('Seu trono ainda não tem histórias.'), findsOneWidget);
  });

  testWidgets(
    'Não confirma salvamento sem resposta nem duplica envio durante espera',
    (tester) async {
      await open(tester);
      await history(tester);
      await tester.tap(find.text('Registrar minha cagada'));
      await tester.pumpAndSettle();
      records.saveGate = Completer<void>();
      records.fail = true;
      await tester.enterText(
        find.byKey(const Key('record-notes')),
        'Meu rascunho',
      );
      await tester.ensureVisible(find.text('Salvar registro privado'));
      await tester.tap(find.text('Salvar registro privado'));
      await tester.pump();
      await tester.tap(find.text('Salvar registro privado'));
      await tester.pump();
      expect(records.creates, 1);
      records.saveGate!.complete();
      await tester.pumpAndSettle();
      expect(find.textContaining('Não foi possível confirmar'), findsOneWidget);
      expect(find.text('Meu rascunho'), findsOneWidget);
      expect(records.items, isEmpty);
      expect(records.creates, 1);
    },
  );

  testWidgets('Rotas de registros são privadas', (tester) async {
    await open(tester, loggedIn: false);
    container.read(appRouterProvider).go('/registros');
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('auth-submit')), findsOneWidget);
    expect(find.text('Registrar minha cagada'), findsNothing);
  });

  testWidgets('Resposta de histórico pendente não reaparece depois do logout', (
    tester,
  ) async {
    await open(tester);
    await history(tester);
    records.listGate = Completer<RecordPage>();
    await tester.tap(find.text('Atualizar histórico'));
    await tester.pump();
    await container.read(authControllerProvider.notifier).logout();
    await tester.pumpAndSettle();
    records.listGate!.complete(
      RecordPage([
        RecordEntry(
          '11111111-1111-4111-8111-111111111111',
          RecordDraft(
            occurredAt: DateTime.now(),
            notes: 'História que deve ficar oculta',
          ),
        ),
      ], false),
    );
    await tester.pumpAndSettle();
    expect(find.text('História que deve ficar oculta'), findsNothing);
    expect(find.text('Meu trono'), findsNothing);
    expect(find.byKey(const Key('auth-submit')), findsOneWidget);
  });

  testWidgets('Formulário privado permanece utilizável com texto ampliado', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 568);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    tester.platformDispatcher.textScaleFactorTestValue = 2;
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    await open(tester);
    await history(tester);
    await tester.scrollUntilVisible(find.text('Registrar minha cagada'), 100);
    await tester.tap(find.text('Registrar minha cagada'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('record-duration')), '0');
    await tester.ensureVisible(find.text('Salvar registro privado'));
    await tester.tap(find.text('Salvar registro privado'));
    await tester.pumpAndSettle();
    expect(records.creates, 0);
    expect(tester.takeException(), isNull);
  });
}

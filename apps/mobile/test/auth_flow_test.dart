import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trono/main.dart';
import 'package:trono/features/auth/domain/auth_models.dart';
import 'package:trono/features/auth/domain/auth_session.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:trono/app/app_router.dart';
import 'support/auth_fakes.dart';

void main() {
  late FakeAuthApi api;
  late MemoryVault vault;
  late AuthSession session;
  setUp(() {
    api = FakeAuthApi(() => DateTime.now().toUtc());
    vault = MemoryVault();
    session = AuthSession(api, vault);
  });
  Future<void> openLogin(WidgetTester tester) async {
    await tester.pumpWidget(MyApp(session: session));
    await tester.pumpAndSettle();
    expect(find.text('Entrar'), findsOneWidget);
    await tester.ensureVisible(find.text('Entrar'));
    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();
  }

  Future<void> fill(
    WidgetTester tester, {
    String password = '  uma senha privada  ',
  }) async {
    await tester.enterText(
      find.byKey(const Key('email')),
      'pessoa@example.com',
    );
    await tester.enterText(find.byKey(const Key('password')), password);
    await tester.ensureVisible(find.byKey(const Key('auth-submit')));
  }

  Future<void> signIn(WidgetTester tester) async {
    await openLogin(tester);
    await fill(tester);
    await tester.tap(find.byKey(const Key('auth-submit')));
    await tester.pumpAndSettle();
  }

  testWidgets('Preferências de push permitem ativar e desativar', (
    tester,
  ) async {
    await signIn(tester);
    await tester.ensureVisible(find.text('Preferências'));
    await tester.tap(find.text('Preferências'));
    await tester.pumpAndSettle();

    final toggle = find.byType(SwitchListTile);
    expect(tester.widget<SwitchListTile>(toggle).value, isFalse);
    await tester.tap(find.text('Permitir notificações push'));
    await tester.pumpAndSettle();
    expect(tester.widget<SwitchListTile>(toggle).value, isTrue);

    await tester.tap(find.text('Permitir notificações push'));
    await tester.pumpAndSettle();
    expect(tester.widget<SwitchListTile>(toggle).value, isFalse);
  });

  testWidgets('Falha ao salvar preferências mantém a escolha confirmada', (
    tester,
  ) async {
    await signIn(tester);
    await tester.ensureVisible(find.text('Preferências'));
    await tester.tap(find.text('Preferências'));
    await tester.pumpAndSettle();
    api.pushUpdateFailure = const AuthFailure(AuthFailureKind.network);

    await tester.tap(find.text('Permitir notificações push'));
    await tester.pumpAndSettle();
    expect(
      tester.widget<SwitchListTile>(find.byType(SwitchListTile)).value,
      isFalse,
    );
    expect(find.textContaining('Não foi possível conectar.'), findsOneWidget);
    expect(api.pushUpdates, 1);
  });

  Future<void> openProfile(WidgetTester tester) async {
    await signIn(tester);
    await tester.ensureVisible(find.text('Editar apelido'));
    await tester.tap(find.text('Editar apelido'));
    await tester.pumpAndSettle();
  }

  testWidgets('Salva o apelido e permite removê-lo', (tester) async {
    await openProfile(tester);
    final nickname = find.byKey(const Key('profile-nickname'));
    await tester.enterText(nickname, '  Guardiã do Trono 👑  ');
    await tester.tap(find.text('Salvar apelido'));
    await tester.pumpAndSettle();
    expect(find.text('Guardiã do Trono 👑'), findsOneWidget);

    await tester.ensureVisible(find.text('Editar apelido'));
    await tester.tap(find.text('Editar apelido'));
    await tester.pumpAndSettle();
    expect(
      tester.widget<TextFormField>(nickname).initialValue,
      'Guardiã do Trono 👑',
    );
    await tester.enterText(nickname, '');
    await tester.tap(find.text('Salvar apelido'));
    await tester.pumpAndSettle();
    expect(find.text('Minha conta'), findsOneWidget);
    expect(find.text('Guardiã do Trono 👑'), findsNothing);
  });

  testWidgets('Valida o comprimento na edição do apelido', (tester) async {
    await openProfile(tester);
    await tester.enterText(find.byKey(const Key('profile-nickname')), 'a' * 41);
    await tester.tap(find.text('Salvar apelido'));
    await tester.pumpAndSettle();
    expect(find.text('Use até 40 caracteres.'), findsOneWidget);
    expect(find.text('Editar apelido'), findsOneWidget);
  });

  Future<void> openDeletion(WidgetTester tester) async {
    await signIn(tester);
    await tester.ensureVisible(find.text('Excluir minha conta'));
    await tester.tap(find.text('Excluir minha conta'));
    await tester.pumpAndSettle();
  }

  Future<void> prepareDeletion(WidgetTester tester) async {
    await tester.enterText(
      find.byKey(const Key('delete-password')),
      '  minha senha  ',
    );
    await tester.ensureVisible(find.byKey(const Key('delete-consent')));
    await tester.tap(find.byKey(const Key('delete-consent')));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byKey(const Key('delete-submit')));
    await tester.tap(find.byKey(const Key('delete-submit')));
    await tester.pumpAndSettle();
  }

  testWidgets(
    'Falha de rede e limpeza não declara exclusão nem saída concluídas',
    (tester) async {
      await openDeletion(tester);
      api.deleteFailure = const AuthFailure(AuthFailureKind.network);
      vault.failClear = true;
      await prepareDeletion(tester);
      await tester.tap(find.text('Excluir definitivamente'));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Não foi possível confirmar a exclusão nem limpar o acesso salvo neste aparelho. Tente novamente.',
        ),
        findsOneWidget,
      );
      expect(find.text('Conta excluída.'), findsNothing);
      expect(vault.value, isNotNull);
      expect(api.deletions, 1);
    },
  );
  testWidgets('Exclusão em andamento bloqueia outro envio', (tester) async {
    await openDeletion(tester);
    api.deleteGate = Completer<void>();
    await prepareDeletion(tester);
    await tester.tap(find.text('Excluir definitivamente'));
    await tester.pump();
    expect(api.deletions, 1);
    expect(
      tester
          .widget<FilledButton>(find.byKey(const Key('delete-submit')))
          .onPressed,
      isNull,
    );
    api.deleteGate!.complete();
    await tester.pumpAndSettle();
    expect(api.deletions, 1);
    expect(find.text('Conta excluída.'), findsOneWidget);
  });
  testWidgets(
    'Confirmação de exclusão funciona em tela pequena com texto ampliado',
    (tester) async {
      tester.view.physicalSize = const Size(320, 568);
      tester.view.devicePixelRatio = 1;
      tester.platformDispatcher.textScaleFactorTestValue = 2;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
      await openDeletion(tester);
      await prepareDeletion(tester);
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('Cancelar'));
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(api.deletions, 0);
    },
  );

  testWidgets(
    'Exclusão exige consentimento e confirmação; cancelar não envia requisição',
    (tester) async {
      await openDeletion(tester);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('delete-submit')))
            .onPressed,
        isNull,
      );
      await prepareDeletion(tester);
      expect(api.deletions, 0);
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(api.deletions, 0);
      expect(vault.value, isNotNull);
      await tester.ensureVisible(find.byKey(const Key('delete-submit')));
      await tester.tap(find.byKey(const Key('delete-submit')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Excluir definitivamente'));
      await tester.pumpAndSettle();
      expect(api.deletions, 1);
      expect(vault.value, isNull);
      expect(find.text('Conta excluída.'), findsOneWidget);
      expect(find.text('Minha conta'), findsNothing);
    },
  );
  testWidgets('Recusa de senha permite corrigir sem nova exclusão automática', (
    tester,
  ) async {
    api.deleteFailure = const AuthFailure(AuthFailureKind.forbidden);
    await openDeletion(tester);
    await prepareDeletion(tester);
    await tester.tap(find.text('Excluir definitivamente'));
    await tester.pumpAndSettle();
    expect(
      find.text('Não foi possível confirmar a exclusão. Verifique sua senha.'),
      findsOneWidget,
    );
    expect(api.deletions, 1);
    expect(api.refreshes, 0);
    expect(vault.value, isNotNull);
  });
  testWidgets(
    'Falha incerta não afirma exclusão e volta ao login sem dados privados',
    (tester) async {
      api.deleteFailure = const AuthFailure(AuthFailureKind.network);
      await openDeletion(tester);
      await prepareDeletion(tester);
      await tester.tap(find.text('Excluir definitivamente'));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Não foi possível confirmar a exclusão. Você saiu deste aparelho; entre novamente para verificar.',
        ),
        findsOneWidget,
      );
      expect(find.text('Conta excluída.'), findsNothing);
      expect(vault.value, isNull);
      expect(api.deletions, 1);
    },
  );

  testWidgets(
    'Rota privada não aceita navegação direta antes ou depois da sessão',
    (tester) async {
      await tester.pumpWidget(MyApp(session: session));
      await tester.pumpAndSettle();
      final container = ProviderScope.containerOf(
        tester.element(find.byType(TronoApp)),
      );
      final router = container.read(appRouterProvider);
      for (final path in ['/conta', '/perfil', '/preferencias']) {
        router.go(path);
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('profile-nickname')), findsNothing);
        expect(find.byKey(const Key('email')), findsOneWidget);
      }
      expect(find.text('Minha conta'), findsNothing);
      await fill(tester);
      await tester.tap(find.byKey(const Key('auth-submit')));
      await tester.pumpAndSettle();
      expect(find.text('Minha conta'), findsOneWidget);
      await tester.ensureVisible(find.text('Sair'));
      await tester.tap(find.text('Sair'));
      await tester.pumpAndSettle();
      for (final path in ['/conta', '/perfil', '/preferencias']) {
        router.go(path);
        await tester.pumpAndSettle();
        expect(find.text('Minha conta'), findsNothing);
        expect(find.byKey(const Key('profile-nickname')), findsNothing);
        expect(find.byKey(const Key('email')), findsOneWidget);
      }
    },
  );

  testWidgets(
    'Login mostra a própria conta e logout limpa credenciais e tela',
    (tester) async {
      await signIn(tester);
      expect(find.text('Minha conta'), findsOneWidget);
      expect(find.text('pessoa@example.com'), findsOneWidget);
      expect(api.receivedPassword, '  uma senha privada  ');
      await tester.ensureVisible(find.text('Sair'));
      await tester.tap(find.text('Sair'));
      await tester.pumpAndSettle();
      expect(find.text('Minha conta'), findsNothing);
      expect(vault.value, isNull);
      expect(api.logouts, 1);
    },
  );
  testWidgets('Validação impede envio vazio e mostra mensagens dos campos', (
    tester,
  ) async {
    await openLogin(tester);
    await tester.ensureVisible(find.byKey(const Key('auth-submit')));
    await tester.tap(find.byKey(const Key('auth-submit')));
    await tester.pumpAndSettle();
    expect(find.text('Informe um e-mail válido.'), findsOneWidget);
    expect(find.text('Informe sua senha.'), findsOneWidget);
    expect(api.logins, 0);
  });
  testWidgets('Cadastro exige confirmação e volta ao login sem autenticar', (
    tester,
  ) async {
    await openLogin(tester);
    await tester.ensureVisible(find.text('Criar conta'));
    await tester.tap(find.text('Criar conta'));
    await tester.pumpAndSettle();
    await fill(tester, password: 'uma senha privada');
    await tester.enterText(
      find.byKey(const Key('password-confirmation')),
      'diferente',
    );
    await tester.ensureVisible(find.byKey(const Key('auth-submit')));
    await tester.tap(find.byKey(const Key('auth-submit')));
    await tester.pumpAndSettle();
    expect(find.text('As senhas precisam ser iguais.'), findsOneWidget);
    expect(api.registrations, 0);
    await tester.enterText(
      find.byKey(const Key('password-confirmation')),
      'uma senha privada',
    );
    await tester.ensureVisible(find.byKey(const Key('auth-submit')));
    await tester.tap(find.byKey(const Key('auth-submit')));
    await tester.pumpAndSettle();
    expect(find.text('Conta criada. Entre para continuar.'), findsOneWidget);
    expect(api.registrations, 1);
    expect(api.logins, 0);
    expect(find.text('Minha conta'), findsNothing);
  });
  testWidgets('Envio em andamento bloqueia toques duplicados', (tester) async {
    api.loginGate = Completer<void>();
    await openLogin(tester);
    await fill(tester);
    await tester.tap(find.byKey(const Key('auth-submit')));
    await tester.pump();
    expect(
      tester
          .widget<FilledButton>(find.byKey(const Key('auth-submit')))
          .onPressed,
      isNull,
    );
    expect(api.logins, 1);
    api.loginGate!.complete();
    await tester.pumpAndSettle();
    expect(api.logins, 1);
    expect(find.text('Minha conta'), findsOneWidget);
  });
  testWidgets(
    'Falha de login mostra mensagem segura e permite nova tentativa',
    (tester) async {
      api.loginFailure = const AuthFailure(AuthFailureKind.unauthorized);
      await signIn(tester);
      expect(find.text('E-mail ou senha inválidos.'), findsOneWidget);
      expect(find.text('Minha conta'), findsNothing);
      api.loginFailure = null;
      await tester.ensureVisible(find.byKey(const Key('auth-submit')));
      await tester.tap(find.byKey(const Key('auth-submit')));
      await tester.pumpAndSettle();
      expect(find.text('Minha conta'), findsOneWidget);
    },
  );
  testWidgets('Falha no storage não apresenta conta autenticada', (
    tester,
  ) async {
    vault.failWrite = true;
    await signIn(tester);
    expect(
      find.text(
        'Não foi possível acessar o armazenamento seguro. Tente novamente.',
      ),
      findsOneWidget,
    );
    expect(find.text('Minha conta'), findsNothing);
    expect(api.logouts, 1);
  });
  testWidgets('Logout durante leitura pendente impede ressurgimento da conta', (
    tester,
  ) async {
    await signIn(tester);
    api.meGate = Completer<void>();
    await tester.tap(find.text('Atualizar conta'));
    await tester.pump();
    await tester.ensureVisible(find.text('Sair'));
    await tester.tap(find.text('Sair'));
    await tester.pump();
    expect(find.text('pessoa@example.com'), findsNothing);
    api.meGate!.complete();
    await tester.pumpAndSettle();
    expect(find.text('Minha conta'), findsNothing);
    expect(vault.value, isNull);
  });
  testWidgets(
    'Formulário continua utilizável em tela pequena e texto ampliado',
    (tester) async {
      tester.view.physicalSize = const Size(320, 568);
      tester.view.devicePixelRatio = 1;
      tester.platformDispatcher.textScaleFactorTestValue = 2;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
      await openLogin(tester);
      await fill(tester);
      expect(tester.takeException(), isNull);
      expect(
        find.byKey(const Key('auth-submit')).hitTestable(),
        findsOneWidget,
      );
    },
  );
}

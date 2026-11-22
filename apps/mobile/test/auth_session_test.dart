import 'dart:async';
import 'package:flutter_test/flutter_test.dart';
import 'package:trono/features/auth/domain/auth_models.dart';
import 'package:trono/features/auth/domain/auth_session.dart';

import 'support/auth_fakes.dart';

void main() {
  late DateTime now;
  late MemoryVault vault;
  late FakeAuthApi api;
  late AuthSession session;
  setUp(() {
    now = DateTime.utc(2026, 11, 12);
    vault = MemoryVault();
    api = FakeAuthApi(() => now);
    session = AuthSession(api, vault, now: () => now);
  });

  test(
    'Exclusão confirmada apaga a sessão e preserva a senha exata da confirmação',
    () async {
      await session.login('pessoa@example.com', 'senha de teste');
      final result = await session.deleteAccount('  senha exata  ');
      expect(result.confirmed, isTrue);
      expect(result.localCleared, isTrue);
      expect(api.deletions, 1);
      expect(api.deletePassword, '  senha exata  ');
      expect(vault.value, isNull);
      await expectLater(session.account(), throwsA(isA<AuthFailure>()));
    },
  );
  test(
    'Senha recusada não repete DELETE nem refresh nem encerra conta local',
    () async {
      await session.login('pessoa@example.com', 'senha de teste');
      api.deleteFailure = const AuthFailure(AuthFailureKind.forbidden);
      await expectLater(
        session.deleteAccount('errada'),
        throwsA(
          isA<AuthFailure>().having(
            (error) => error.kind,
            'kind',
            AuthFailureKind.forbidden,
          ),
        ),
      );
      expect(api.deletions, 1);
      expect(api.refreshes, 0);
      expect(vault.value, isNotNull);
      expect((await session.account()).email, 'pessoa@example.com');
    },
  );
  test(
    'Resposta incerta de exclusão limpa sessão sem declarar conta excluída e sem retry',
    () async {
      await session.login('pessoa@example.com', 'senha de teste');
      api.deleteFailure = const AuthFailure(AuthFailureKind.network);
      final result = await session.deleteAccount('senha de teste');
      expect(result.confirmed, isFalse);
      expect(result.localCleared, isTrue);
      expect(api.deletions, 1);
      expect(vault.value, isNull);
      await expectLater(session.account(), throwsA(isA<AuthFailure>()));
    },
  );
  test(
    'Falha na limpeza local não oculta a confirmação de exclusão do servidor',
    () async {
      await session.login('pessoa@example.com', 'senha de teste');
      vault.failClear = true;
      final result = await session.deleteAccount('senha de teste');
      expect(result.confirmed, isTrue);
      expect(result.localCleared, isFalse);
      expect(api.deletions, 1);
      await expectLater(session.account(), throwsA(isA<AuthFailure>()));
    },
  );

  test('Cadastro não cria sessão nem faz login automaticamente', () async {
    await session.register('pessoa@example.com', 'senha de teste');
    expect(api.registrations, 1);
    expect(api.logins, 0);
    expect(vault.value, isNull);
  });
  test(
    'Login preserva senha, salva somente refresh e carrega a própria conta',
    () async {
      final account = await session.login(
        'pessoa@example.com',
        '  senha privada  ',
      );
      expect(account.email, 'pessoa@example.com');
      expect(api.receivedPassword, '  senha privada  ');
      expect(vault.value!.token, 'refresh-login');
      expect(vault.value!.origin, api.origin);
      expect(vault.value.toString(), isNot(contains('refresh-login')));
    },
  );
  test(
    'Restauração consome a credencial local antes de enviá-la e salva a nova',
    () async {
      vault.value = SavedRefresh(
        api.origin,
        'old-refresh',
        now.add(const Duration(days: 1)),
      );
      api.beforeRefresh = () async => expect(vault.value, isNull);
      expect((await session.restore())!.email, 'pessoa@example.com');
      expect(api.refreshes, 1);
      expect(vault.value!.token, 'refresh-renewed-1');
    },
  );
  test(
    'Credenciais de outro servidor ou expiradas são descartadas sem envio',
    () async {
      vault.value = SavedRefresh(
        'https://other.example',
        'secret',
        now.add(const Duration(days: 1)),
      );
      expect(await session.restore(), isNull);
      expect(vault.value, isNull);
      vault.value = SavedRefresh(api.origin, 'expired', now);
      expect(await session.restore(), isNull);
      expect(api.refreshes, 0);
    },
  );
  test(
    'Duas operações com acesso expirado fazem uma única renovação',
    () async {
      await session.login('pessoa@example.com', 'senha de teste');
      now = now.add(const Duration(minutes: 16));
      final results = await Future.wait([session.account(), session.account()]);
      expect(results.length, 2);
      expect(api.refreshes, 1);
    },
  );
  test('401 do acesso provoca só uma renovação e nova leitura', () async {
    await session.login('pessoa@example.com', 'senha de teste');
    api.rejectAccessOnce = true;
    expect((await session.account()).email, 'pessoa@example.com');
    expect(api.refreshes, 1);
    expect(api.reads, 3);
  });
  test('Falha incerta de refresh não repete credencial consumida', () async {
    vault.value = SavedRefresh(
      api.origin,
      'old-refresh',
      now.add(const Duration(days: 1)),
    );
    api.failRefresh = true;
    await expectLater(
      session.restore(),
      throwsA(
        isA<AuthFailure>().having(
          (error) => error.kind,
          'kind',
          AuthFailureKind.expired,
        ),
      ),
    );
    expect(vault.value, isNull);
    expect(await session.restore(), isNull);
    expect(api.refreshes, 1);
  });
  test(
    'Falha ao salvar refresh impede login e tenta revogar acesso emitido',
    () async {
      vault.failWrite = true;
      await expectLater(
        session.login('pessoa@example.com', 'senha de teste'),
        throwsA(isA<AuthFailure>()),
      );
      expect(vault.value, isNull);
      expect(api.logouts, 1);
      await expectLater(session.account(), throwsA(isA<AuthFailure>()));
    },
  );
  test(
    'Falha ao consumir o armazenamento bloqueia refresh antes da rede',
    () async {
      vault.value = SavedRefresh(
        api.origin,
        'old-refresh',
        now.add(const Duration(days: 1)),
      );
      vault.failClear = true;
      await expectLater(session.restore(), throwsA(isA<AuthFailure>()));
      expect(api.refreshes, 0);
    },
  );
  test(
    'Logout offline remove sessão local e informa revogação não confirmada',
    () async {
      await session.login('pessoa@example.com', 'senha de teste');
      api.failLogout = true;
      expect(await session.logout(), isFalse);
      expect(vault.value, isNull);
      await expectLater(session.account(), throwsA(isA<AuthFailure>()));
    },
  );
  test(
    'Logout enfileirado durante renovação descarta o novo refresh ao concluir',
    () async {
      await session.login('pessoa@example.com', 'senha de teste');
      now = now.add(const Duration(minutes: 16));
      final started = Completer<void>();
      api.beforeRefresh = () async => started.complete();
      api.refreshGate = Completer<void>();
      final reading = session.account();
      await started.future;
      final leaving = session.logout();
      api.refreshGate!.complete();
      await reading;
      expect(await leaving, isTrue);
      expect(vault.value, isNull);
      await expectLater(session.account(), throwsA(isA<AuthFailure>()));
    },
  );
}

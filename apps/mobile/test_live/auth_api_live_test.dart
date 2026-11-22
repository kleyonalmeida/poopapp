import 'dart:io';
import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trono/features/auth/data/dio_auth_api.dart';
import 'package:trono/features/auth/domain/auth_models.dart';
import 'package:trono/features/auth/domain/auth_session.dart';
import '../test/support/auth_fakes.dart';

class LocalTls extends HttpOverrides {
  LocalTls(this.trust);
  final SecurityContext trust;
  @override
  HttpClient createHttpClient(SecurityContext? context) =>
      super.createHttpClient(trust);
}

void main() {
  test(
    'Cliente real: cadastro, login, perfil, restauração rotativa e logout por TLS',
    () async {
      const url = String.fromEnvironment('TRONO_TEST_API_URL');
      const caPath = String.fromEnvironment('TRONO_TEST_CA');
      expect(
        {'localhost', '127.0.0.1'}.contains(Uri.parse(url).host),
        isTrue,
        reason:
            'Execute scripts/test_mobile_api.py com seu backend descartável.',
      );
      final trust = SecurityContext(withTrustedRoots: false)
        ..setTrustedCertificates(caPath);
      await HttpOverrides.runWithHttpOverrides(() async {
        final dio = Dio()..httpClientAdapter = IOHttpClientAdapter();
        final api = DioAuthApi(url, dio: dio);
        addTearDown(api.close);
        final vault = MemoryVault();
        final session = AuthSession(api, vault);
        final email =
            'mobile-${DateTime.now().microsecondsSinceEpoch}@example.com';
        await session.register(email, 'uma senha longa de fixture');
        expect(vault.value, isNull);
        final account = await session.login(
          email,
          'uma senha longa de fixture',
        );
        expect(account.email, email);
        expect(account.emailConfirmed, isFalse);
        expect(account.nickname, isNull);
        expect(account.pushNotificationsEnabled, isFalse);
        expect(await session.updatePushNotifications(true), isTrue);
        expect(await session.updateNickname('  Guardiã 👑  '), 'Guardiã 👑');
        expect((await session.account()).nickname, 'Guardiã 👑');
        final oldRefresh = vault.value!.token;
        final restarted = AuthSession(api, vault);
        final restored = await restarted.restore();
        expect(restored!.id, account.id);
        expect(restored.nickname, 'Guardiã 👑');
        expect(restored.pushNotificationsEnabled, isTrue);
        expect(await restarted.updatePushNotifications(false), isFalse);
        expect(await restarted.updateNickname(null), isNull);
        expect((await restarted.account()).nickname, isNull);
        expect(vault.value!.token, isNot(oldRefresh));
        final currentRefresh = vault.value!.token;
        expect(await restarted.logout(), isTrue);
        expect(vault.value, isNull);
        await expectLater(
          api.refresh(currentRefresh),
          throwsA(
            isA<AuthFailure>().having(
              (error) => error.kind,
              'kind',
              AuthFailureKind.unauthorized,
            ),
          ),
        );
        final removalVault = MemoryVault();
        final removal = AuthSession(api, removalVault);
        final removalEmail =
            'remove-mobile-${DateTime.now().microsecondsSinceEpoch}@example.com';
        const password = '  senha de exclusao de fixture  ';
        await removal.register(removalEmail, password);
        await removal.login(removalEmail, password);
        final beforeDenial = removalVault.value!.token;
        await expectLater(
          removal.deleteAccount(password.trim()),
          throwsA(
            isA<AuthFailure>().having(
              (error) => error.kind,
              'kind',
              AuthFailureKind.forbidden,
            ),
          ),
        );
        expect(removalVault.value!.token, beforeDenial);
        expect((await removal.account()).email, removalEmail);
        final removed = await removal.deleteAccount(password);
        expect(removed.confirmed, isTrue);
        expect(removed.localCleared, isTrue);
        expect(removalVault.value, isNull);
        await expectLater(
          api.login(removalEmail, password),
          throwsA(
            isA<AuthFailure>().having(
              (error) => error.kind,
              'kind',
              AuthFailureKind.unauthorized,
            ),
          ),
        );
      }, LocalTls(trust));
    },
  );
}

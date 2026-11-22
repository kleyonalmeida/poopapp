import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:trono/features/auth/data/dio_auth_api.dart';
import 'package:trono/features/auth/data/preview_refresh_vault.dart';
import 'package:trono/features/auth/domain/auth_models.dart';
import 'package:trono/features/auth/domain/auth_session.dart';
import 'package:trono/features/auth/presentation/auth_providers.dart';
import 'package:trono/features/records/presentation/records_provider.dart';

void main() {
  test(
    'Registros usam a origem da sessão também na prévia sem dart-define',
    () {
      final api = DioAuthApi('https://localhost:8443');
      addTearDown(api.close);
      final container = ProviderContainer(
        overrides: [
          authSessionProvider.overrideWithValue(
            AuthSession(api, PreviewRefreshVault()),
          ),
        ],
      );
      addTearDown(container.dispose);
      expect(() => container.read(recordsRepositoryProvider), returnsNormally);
    },
  );
  test(
    'A prévia não recupera credenciais de outra instância ou recarga',
    () async {
      final first = PreviewRefreshVault();
      final saved = SavedRefresh(
        'https://localhost:8443',
        'refresh-secret',
        DateTime.utc(2027),
      );
      await first.write(saved);
      expect(await first.read(), same(saved));
      expect(await PreviewRefreshVault().read(), isNull);
      await first.clear();
      expect(await first.read(), isNull);
    },
  );
}

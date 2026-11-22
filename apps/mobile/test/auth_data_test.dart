import 'dart:convert';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trono/features/auth/data/dio_auth_api.dart';
import 'package:trono/features/auth/data/secure_refresh_vault.dart';
import 'package:trono/features/auth/domain/auth_models.dart';

class ContractAdapter implements HttpClientAdapter {
  final List<RequestOptions> requests = [];
  int status = 200;
  dynamic response;
  bool offline = false;
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    if (offline) {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.connectionError,
        error: 'private access-token',
      );
    }
    return ResponseBody.fromString(
      jsonEncode(response),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late ContractAdapter adapter;
  late DioAuthApi api;
  final now = DateTime.utc(2026, 11, 12);
  Map<String, dynamic> tokens() => {
    'tokenType': 'Bearer',
    'accessToken': 'at_${'A' * 64}',
    'refreshToken': 'rt_${'B' * 64}',
    'expiresIn': 900,
    'refreshExpiresAt': now.add(const Duration(days: 7)).toIso8601String(),
  };
  setUp(() {
    adapter = ContractAdapter();
    final dio = Dio()..httpClientAdapter = adapter;
    api = DioAuthApi('https://trono.example', dio: dio, now: () => now);
    FlutterSecureStorage.setMockInitialValues({});
  });
  test(
    'PATCH de preferências usa um booleano e valida a confirmação',
    () async {
      adapter.response = {'pushNotificationsEnabled': true};
      expect(await api.updatePushNotifications('access-secret', true), isTrue);
      final request = adapter.requests.single;
      expect(request.method, 'PATCH');
      expect(request.path, '/api/v1/me/settings');
      expect(request.data, {'pushNotificationsEnabled': true});
      expect(request.headers['Authorization'], 'Bearer access-secret');

      adapter.response = {'pushNotificationsEnabled': 'false'};
      await expectLater(
        api.updatePushNotifications('access-secret', false),
        throwsA(
          isA<AuthFailure>().having(
            (error) => error.kind,
            'kind',
            AuthFailureKind.invalidResponse,
          ),
        ),
      );
    },
  );

  test('PATCH de perfil envia remoção explícita e valida o DTO', () async {
    adapter.response = {'nickname': null};
    expect(await api.updateNickname('access-secret', null), isNull);
    final request = adapter.requests.single;
    expect(request.method, 'PATCH');
    expect(request.path, '/api/v1/me/profile');
    expect(request.data, {'nickname': null});
    expect(request.headers['Authorization'], 'Bearer access-secret');
    expect(request.queryParameters, isEmpty);
    expect(request.followRedirects, isFalse);

    adapter.response = {'nickname': 'Guardiã 👑'};
    expect(
      await api.updateNickname('access-secret', 'Guardiã 👑'),
      'Guardiã 👑',
    );
    adapter.response = {};
    await expectLater(
      api.updateNickname('access-secret', null),
      throwsA(
        isA<AuthFailure>().having(
          (error) => error.kind,
          'kind',
          AuthFailureKind.invalidResponse,
        ),
      ),
    );
  });

  test(
    'DELETE de conta envia senha somente no corpo HTTPS e não segue redirect',
    () async {
      adapter.status = 204;
      adapter.response = null;
      await api.deleteAccount('access-secret', '  senha exata  ');
      final request = adapter.requests.single;
      expect(request.method, 'DELETE');
      expect(request.path, '/api/v1/me/account');
      expect(request.data, {'password': '  senha exata  '});
      expect(request.headers['Authorization'], 'Bearer access-secret');
      expect(request.queryParameters, isEmpty);
      expect(request.followRedirects, isFalse);
    },
  );
  test(
    '403 é recusa de permissão e não pedido para renovar a sessão',
    () async {
      adapter.status = 403;
      adapter.response = {'title': 'private details'};
      await expectLater(
        api.me('access-secret'),
        throwsA(
          isA<AuthFailure>().having(
            (error) => error.kind,
            'kind',
            AuthFailureKind.forbidden,
          ),
        ),
      );
    },
  );
  test(
    'Configuração aceita somente origem HTTPS sem credenciais, query ou path',
    () {
      for (final url in [
        '',
        'http://trono.example',
        'https://user:secret@trono.example',
        'https://trono.example?token=x',
        'https://trono.example/api',
      ]) {
        expect(() => DioAuthApi(url), throwsA(isA<AuthFailure>()));
      }
    },
  );
  test(
    'Login respeita DTO, preserva senha e desabilita redirecionamentos',
    () async {
      adapter.response = tokens();
      final result = await api.login('pessoa@example.com', '  senha privada  ');
      expect(result.accessExpiresAt, now.add(const Duration(minutes: 15)));
      expect(result.toString(), isNot(contains(result.accessToken)));
      final request = adapter.requests.single;
      expect(request.path, '/api/v1/auth/login');
      expect(request.data['password'], '  senha privada  ');
      expect(request.followRedirects, isFalse);
      expect(request.queryParameters, isEmpty);
    },
  );
  test(
    'Cadastro, renovação, me e logout seguem contratos sem token na URL',
    () async {
      adapter.status = 201;
      adapter.response = {'id': 'fixture'};
      await api.register('pessoa@example.com', 'senha privada');
      adapter.status = 200;
      adapter.response = tokens();
      await api.refresh('refresh-secret');
      expect(adapter.requests.last.data['refreshToken'], 'refresh-secret');
      adapter.response = {
        'id': 'b622cb94-4350-4f9e-9557-be9aadfd7c15',
        'email': 'pessoa@example.com',
        'emailConfirmed': false,
      };
      expect((await api.me('access-secret')).email, 'pessoa@example.com');
      expect(
        adapter.requests.last.headers['Authorization'],
        'Bearer access-secret',
      );
      adapter.status = 204;
      adapter.response = null;
      await api.logout('access-secret');
      expect(adapter.requests.last.path, '/api/v1/auth/logout');
      expect(
        adapter.requests.every((value) => value.queryParameters.isEmpty),
        isTrue,
      );
    },
  );
  test(
    'Respostas inválidas e erros nunca expõem corpo ou detalhes de Dio',
    () async {
      adapter.response = {'accessToken': 'private'};
      await expectLater(
        api.login('a@example.com', 'private'),
        throwsA(isA<AuthFailure>()),
      );
      adapter.status = 401;
      adapter.response = {'title': 'private password'};
      try {
        await api.login('a@example.com', 'private');
        fail('Era esperado erro');
      } on AuthFailure catch (error) {
        expect(error.kind, AuthFailureKind.unauthorized);
        expect(error.toString(), isNot(contains('private')));
      }
      adapter.offline = true;
      await expectLater(
        api.me('private'),
        throwsA(
          isA<AuthFailure>().having(
            (error) => error.kind,
            'kind',
            AuthFailureKind.network,
          ),
        ),
      );
    },
  );
  test(
    'Refresh é persistido em uma única entrada sem senha, conta ou acesso',
    () async {
      final vault = SecureRefreshVault();
      await vault.write(
        SavedRefresh(api.origin, 'rt_secret', now.add(const Duration(days: 1))),
      );
      final raw = await const FlutterSecureStorage().read(
        key: SecureRefreshVault.key,
      );
      expect(jsonDecode(raw!)['refreshToken'], 'rt_secret');
      expect(raw, isNot(contains('accessToken')));
      expect(raw, isNot(contains('password')));
      expect(raw, isNot(contains('email')));
      expect((await vault.read())!.token, 'rt_secret');
      await const FlutterSecureStorage().write(key: 'unrelated', value: 'keep');
      await vault.clear();
      expect(await vault.read(), isNull);
      expect(await const FlutterSecureStorage().read(key: 'unrelated'), 'keep');
    },
  );
  test('Conteúdo local corrompido é eliminado sem restaurar sessão', () async {
    FlutterSecureStorage.setMockInitialValues({
      SecureRefreshVault.key: 'not-json',
    });
    expect(await SecureRefreshVault().read(), isNull);
    expect(
      await const FlutterSecureStorage().read(key: SecureRefreshVault.key),
      isNull,
    );
  });
}

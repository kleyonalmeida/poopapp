import 'package:dio/dio.dart';
import '../domain/auth_models.dart';

class DioAuthApi implements AuthApi {
  @override
  Future<void> deleteAccount(String accessToken, String password) async {
    await _request(
      'DELETE',
      '/api/v1/me/account',
      204,
      data: {'password': password},
      access: accessToken,
    );
  }

  DioAuthApi(String url, {Dio? dio, DateTime Function()? now})
    : _dio = dio ?? Dio(),
      _now = now ?? DateTime.now {
    try {
      final uri = Uri.parse(url);
      if (uri.scheme != 'https' ||
          uri.host.isEmpty ||
          uri.userInfo.isNotEmpty ||
          uri.hasQuery ||
          uri.hasFragment ||
          (uri.path.isNotEmpty && uri.path != '/') ||
          uri.port < 1 ||
          uri.port > 65535) {
        throw const AuthFailure(AuthFailureKind.configuration);
      }
      origin = uri.origin;
    } catch (_) {
      throw const AuthFailure(AuthFailureKind.configuration);
    }
    _dio.options = BaseOptions(
      baseUrl: origin,
      connectTimeout: const Duration(seconds: 15),
      sendTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      followRedirects: false,
      maxRedirects: 0,
      responseType: ResponseType.json,
      headers: {'Accept': 'application/json'},
    );
  }
  final Dio _dio;
  final DateTime Function() _now;
  @override
  late final String origin;

  Future<dynamic> _request(
    String method,
    String path,
    int expected, {
    Object? data,
    String? access,
  }) async {
    try {
      final response = await _dio.request<dynamic>(
        path,
        data: data,
        options: Options(
          method: method,
          headers: {if (access != null) 'Authorization': 'Bearer $access'},
        ),
      );
      if (response.statusCode != expected) {
        throw const AuthFailure(AuthFailureKind.invalidResponse);
      }
      return response.data;
    } on DioException catch (error) {
      final status = error.response?.statusCode;
      if (status == 400 || status == 422) {
        throw const AuthFailure(AuthFailureKind.invalidInput);
      }
      if (status == 401) {
        throw const AuthFailure(AuthFailureKind.unauthorized);
      }
      if (status == 403) throw const AuthFailure(AuthFailureKind.forbidden);
      if (status == 409) throw const AuthFailure(AuthFailureKind.conflict);
      if (status == 429) throw const AuthFailure(AuthFailureKind.throttled);
      if (status == null) throw const AuthFailure(AuthFailureKind.network);
      throw const AuthFailure(AuthFailureKind.service);
    } on AuthFailure {
      rethrow;
    } catch (_) {
      throw const AuthFailure(AuthFailureKind.invalidResponse);
    }
  }

  AuthTokens _tokens(dynamic value) {
    try {
      if (value is! Map || value['tokenType'] != 'Bearer') {
        throw const FormatException();
      }
      final access = value['accessToken'];
      final refresh = value['refreshToken'];
      final seconds = value['expiresIn'];
      final expiry = value['refreshExpiresAt'];
      if (access is! String ||
          !RegExp(r'^at_[a-fA-F0-9]{64}$').hasMatch(access) ||
          refresh is! String ||
          !RegExp(r'^rt_[a-fA-F0-9]{64}$').hasMatch(refresh) ||
          seconds is! int ||
          seconds < 1 ||
          seconds > 900 ||
          expiry is! String ||
          !RegExp(r'(Z|[+-]\d{2}:\d{2})$').hasMatch(expiry)) {
        throw const FormatException();
      }
      final end = DateTime.parse(expiry).toUtc();
      final now = _now().toUtc();
      if (!end.isAfter(now)) throw const FormatException();
      final accessEnd = now.add(Duration(seconds: seconds));
      return AuthTokens(
        access,
        refresh,
        accessEnd.isBefore(end) ? accessEnd : end,
        end,
      );
    } catch (_) {
      throw const AuthFailure(AuthFailureKind.invalidResponse);
    }
  }

  @override
  Future<void> register(String email, String password) async {
    await _request(
      'POST',
      '/api/v1/auth/register',
      201,
      data: {'email': email, 'password': password},
    );
  }

  @override
  Future<AuthTokens> login(String email, String password) async => _tokens(
    await _request(
      'POST',
      '/api/v1/auth/login',
      200,
      data: {'email': email, 'password': password},
    ),
  );
  @override
  Future<AuthTokens> refresh(String token) async => _tokens(
    await _request(
      'POST',
      '/api/v1/auth/refresh',
      200,
      data: {'refreshToken': token},
    ),
  );
  @override
  Future<AuthAccount> me(String accessToken) async {
    final value = await _request('GET', '/api/v1/me', 200, access: accessToken);
    if (value is! Map ||
        value['id'] is! String ||
        value['email'] is! String ||
        (value['email'] as String).isEmpty ||
        value['emailConfirmed'] is! bool ||
        (value['nickname'] != null && value['nickname'] is! String) ||
        (value.containsKey('pushNotificationsEnabled') &&
            value['pushNotificationsEnabled'] is! bool)) {
      throw const AuthFailure(AuthFailureKind.invalidResponse);
    }
    return AuthAccount(
      value['id'] as String,
      value['email'] as String,
      value['emailConfirmed'] as bool,
      nickname: value['nickname'] as String?,
      pushNotificationsEnabled:
          value['pushNotificationsEnabled'] as bool? ?? false,
    );
  }

  @override
  Future<String?> updateNickname(String accessToken, String? nickname) async {
    final value = await _request(
      'PATCH',
      '/api/v1/me/profile',
      200,
      data: {'nickname': nickname},
      access: accessToken,
    );
    if (value is! Map ||
        !value.containsKey('nickname') ||
        (value['nickname'] != null && value['nickname'] is! String)) {
      throw const AuthFailure(AuthFailureKind.invalidResponse);
    }
    return value['nickname'] as String?;
  }

  @override
  Future<bool> updatePushNotifications(String accessToken, bool enabled) async {
    final value = await _request(
      'PATCH',
      '/api/v1/me/settings',
      200,
      data: {'pushNotificationsEnabled': enabled},
      access: accessToken,
    );
    if (value is! Map || value['pushNotificationsEnabled'] is! bool) {
      throw const AuthFailure(AuthFailureKind.invalidResponse);
    }
    return value['pushNotificationsEnabled'] as bool;
  }

  @override
  Future<void> logout(String accessToken) async {
    await _request('POST', '/api/v1/auth/logout', 204, access: accessToken);
  }

  void close() => _dio.close(force: true);
}

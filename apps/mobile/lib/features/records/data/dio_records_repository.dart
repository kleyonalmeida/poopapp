import 'dart:math';
import 'package:dio/dio.dart';
import '../../auth/domain/auth_models.dart';
import '../../auth/domain/auth_session.dart';
import '../domain/records.dart';

String newRecordKey() {
  final random = Random.secure();
  final bytes = List<int>.generate(16, (_) => random.nextInt(256));
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;
  final hex = bytes
      .map((byte) => byte.toRadixString(16).padLeft(2, '0'))
      .join();
  return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
}

class DioRecordsRepository implements RecordsRepository {
  DioRecordsRepository(String url, this.session, {Dio? dio})
    : _dio = dio ?? Dio() {
    final uri = Uri.tryParse(url);
    if (uri == null ||
        uri.scheme != 'https' ||
        uri.host.isEmpty ||
        uri.userInfo.isNotEmpty ||
        uri.hasQuery ||
        uri.hasFragment ||
        (uri.path.isNotEmpty && uri.path != '/') ||
        uri.port < 1 ||
        uri.port > 65535) {
      throw const AuthFailure(AuthFailureKind.configuration);
    }
    _dio.options = BaseOptions(
      baseUrl: uri.origin,
      connectTimeout: const Duration(seconds: 15),
      sendTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      followRedirects: false,
      maxRedirects: 0,
      responseType: ResponseType.json,
    );
  }
  final Dio _dio;
  final AuthSession session;
  void close() => _dio.close(force: true);

  Future<dynamic> _request(
    String method,
    String path,
    String access, {
    Object? data,
    String? key,
    Map<String, dynamic>? query,
  }) async {
    try {
      final response = await _dio.request<dynamic>(
        path,
        data: data,
        queryParameters: query,
        options: Options(
          method: method,
          headers: {'Authorization': 'Bearer $access', 'Idempotency-Key': ?key},
        ),
      );
      final valid = method == 'POST'
          ? {200, 201}
          : method == 'DELETE'
          ? {204}
          : {200};
      if (!valid.contains(response.statusCode)) {
        throw const AuthFailure(AuthFailureKind.invalidResponse);
      }
      return response.data;
    } on DioException catch (error) {
      throw AuthFailure(switch (error.response?.statusCode) {
        401 => AuthFailureKind.unauthorized,
        400 || 404 || 410 || 422 => AuthFailureKind.invalidInput,
        403 => AuthFailureKind.forbidden,
        409 => AuthFailureKind.conflict,
        429 => AuthFailureKind.throttled,
        null => AuthFailureKind.network,
        _ => AuthFailureKind.service,
      });
    }
  }

  RecordEntry _entry(dynamic value) {
    try {
      if (value is! Map ||
          value['id'] is! String ||
          !RegExp(
            r'^[a-fA-F0-9]{8}(-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}$',
          ).hasMatch(value['id'] as String) ||
          value['occurredAt'] is! String ||
          !RegExp(
            r'(Z|[+-]\d{2}:\d{2})$',
          ).hasMatch(value['occurredAt'] as String) ||
          !value.containsKey('durationSeconds') ||
          (value['durationSeconds'] != null &&
              (value['durationSeconds'] is! int ||
                  value['durationSeconds'] < 1 ||
                  value['durationSeconds'] > 86400)) ||
          !value.containsKey('type') ||
          (value['type'] != null && !recordTypes.containsKey(value['type'])) ||
          !value.containsKey('notes') ||
          (value['notes'] != null &&
              (value['notes'] is! String ||
                  (value['notes'] as String).length > 280))) {
        throw const FormatException();
      }
      return RecordEntry(
        value['id'] as String,
        RecordDraft(
          occurredAt: DateTime.parse(value['occurredAt'] as String).toUtc(),
          durationSeconds: value['durationSeconds'] as int?,
          type: value['type'] as String?,
          notes: value['notes'] as String?,
        ),
      );
    } catch (_) {
      throw const AuthFailure(AuthFailureKind.invalidResponse);
    }
  }

  @override
  Future<RecordPage> list(int offset) => session.authenticated((access) async {
    final value = await _request(
      'GET',
      '/api/v1/records',
      access,
      query: {'offset': offset, 'limit': 20},
    );
    if (value is! Map ||
        value['items'] is! List ||
        value['hasMore'] is! bool ||
        (value['items'] as List).length > 20) {
      throw const AuthFailure(AuthFailureKind.invalidResponse);
    }
    return RecordPage(
      (value['items'] as List).map(_entry).toList(),
      value['hasMore'] as bool,
    );
  }, readOnly: true);
  @override
  Future<RecordEntry> create(RecordDraft draft, String key) =>
      session.authenticated(
        (access) async => _entry(
          await _request(
            'POST',
            '/api/v1/records',
            access,
            data: draft.toJson(),
            key: key,
          ),
        ),
      );
  @override
  Future<RecordEntry> update(String id, RecordDraft draft) =>
      session.authenticated(
        (access) async => _entry(
          await _request(
            'PATCH',
            '/api/v1/records/${Uri.encodeComponent(id)}',
            access,
            data: draft.toJson(),
          ),
        ),
      );
  @override
  Future<void> delete(String id) => session.authenticated((access) async {
    await _request(
      'DELETE',
      '/api/v1/records/${Uri.encodeComponent(id)}',
      access,
    );
  });
}

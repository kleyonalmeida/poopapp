import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../domain/auth_models.dart';

class SecureRefreshVault implements RefreshVault {
  SecureRefreshVault({FlutterSecureStorage? storage})
    : _storage =
          storage ??
          const FlutterSecureStorage(
            iOptions: IOSOptions(
              accessibility: KeychainAccessibility.unlocked_this_device,
              synchronizable: false,
            ),
          );
  final FlutterSecureStorage _storage;
  static const key = 'trono.identity.session.v1';
  @override
  Future<SavedRefresh?> read() async {
    final raw = await _storage.read(key: key);
    if (raw == null) return null;
    try {
      final data = jsonDecode(raw);
      if (data is! Map ||
          data['version'] != 1 ||
          data['origin'] is! String ||
          data['refreshToken'] is! String ||
          (data['refreshToken'] as String).isEmpty ||
          data['expiresAt'] is! String) {
        throw const FormatException();
      }
      return SavedRefresh(
        data['origin'] as String,
        data['refreshToken'] as String,
        DateTime.parse(data['expiresAt'] as String).toUtc(),
      );
    } catch (_) {
      await clear();
      return null;
    }
  }

  @override
  Future<void> write(SavedRefresh value) => _storage.write(
    key: key,
    value: jsonEncode({
      'version': 1,
      'origin': value.origin,
      'refreshToken': value.token,
      'expiresAt': value.expiresAt.toUtc().toIso8601String(),
    }),
  );
  @override
  Future<void> clear() => _storage.delete(key: key);
}

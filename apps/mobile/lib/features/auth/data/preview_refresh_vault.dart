import '../domain/auth_models.dart';

/// Preview-only: a new page load starts without any saved credentials.
class PreviewRefreshVault implements RefreshVault {
  SavedRefresh? _value;
  @override
  Future<SavedRefresh?> read() async => _value;
  @override
  Future<void> write(SavedRefresh value) async {
    _value = value;
  }

  @override
  Future<void> clear() async {
    _value = null;
  }
}

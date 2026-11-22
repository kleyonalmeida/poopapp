import 'dart:async';
import 'package:trono/features/auth/domain/auth_models.dart';

class MemoryVault implements RefreshVault {
  SavedRefresh? value;
  bool failWrite = false;
  bool failClear = false;
  int clears = 0;
  @override
  Future<SavedRefresh?> read() async => value;
  @override
  Future<void> write(SavedRefresh value) async {
    if (failWrite) throw StateError('storage unavailable');
    this.value = value;
  }

  @override
  Future<void> clear() async {
    if (failClear) throw StateError('storage unavailable');
    clears++;
    value = null;
  }
}

class FakeAuthApi implements AuthApi {
  FakeAuthApi(this.now);
  final DateTime Function() now;
  @override
  String origin = 'https://trono.example';
  int refreshes = 0;
  int logins = 0;
  int registrations = 0;
  int reads = 0;
  int logouts = 0;
  int deletions = 0;
  String? nickname;
  bool pushNotificationsEnabled = false;
  int pushUpdates = 0;
  AuthFailure? pushUpdateFailure;
  AuthFailure? deleteFailure;
  Completer<void>? deleteGate;
  String? deletePassword;
  String? receivedPassword;
  bool rejectAccessOnce = false;
  bool failRefresh = false;
  bool failLogout = false;
  Future<void> Function()? beforeRefresh;
  Completer<void>? refreshGate;
  Completer<void>? loginGate;
  Completer<void>? meGate;
  AuthFailure? loginFailure;
  AuthTokens tokens(String suffix) => AuthTokens(
    'access-$suffix',
    'refresh-$suffix',
    now().add(const Duration(minutes: 15)),
    now().add(const Duration(days: 7)),
  );
  @override
  Future<void> register(String email, String password) async {
    registrations++;
  }

  @override
  Future<AuthTokens> login(String email, String password) async {
    logins++;
    receivedPassword = password;
    await loginGate?.future;
    if (loginFailure != null) throw loginFailure!;
    return tokens('login');
  }

  @override
  Future<AuthTokens> refresh(String token) async {
    refreshes++;
    await beforeRefresh?.call();
    await refreshGate?.future;
    if (failRefresh) throw const AuthFailure(AuthFailureKind.network);
    return tokens('renewed-$refreshes');
  }

  @override
  Future<AuthAccount> me(String accessToken) async {
    reads++;
    await meGate?.future;
    if (rejectAccessOnce) {
      rejectAccessOnce = false;
      throw const AuthFailure(AuthFailureKind.unauthorized);
    }
    return AuthAccount(
      'account-a',
      'pessoa@example.com',
      false,
      nickname: nickname,
      pushNotificationsEnabled: pushNotificationsEnabled,
    );
  }

  @override
  Future<String?> updateNickname(String accessToken, String? nickname) async {
    return this.nickname = nickname;
  }

  @override
  Future<bool> updatePushNotifications(String accessToken, bool enabled) async {
    pushUpdates++;
    if (pushUpdateFailure != null) throw pushUpdateFailure!;
    return pushNotificationsEnabled = enabled;
  }

  @override
  Future<void> logout(String accessToken) async {
    logouts++;
    if (failLogout) throw const AuthFailure(AuthFailureKind.network);
  }

  @override
  Future<void> deleteAccount(String accessToken, String password) async {
    deletions++;
    deletePassword = password;
    await deleteGate?.future;
    if (deleteFailure != null) throw deleteFailure!;
  }
}

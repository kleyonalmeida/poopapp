enum AuthFailureKind {
  unauthorized,
  forbidden,
  expired,
  network,
  service,
  invalidInput,
  conflict,
  throttled,
  storage,
  configuration,
  invalidResponse,
}

class AuthFailure implements Exception {
  const AuthFailure(this.kind);
  final AuthFailureKind kind;
  @override
  String toString() => 'AuthFailure(${kind.name})';
}

class AuthAccount {
  const AuthAccount(
    this.id,
    this.email,
    this.emailConfirmed, {
    this.nickname,
    this.pushNotificationsEnabled = false,
  });
  final String id;
  final String email;
  final bool emailConfirmed;
  final String? nickname;
  final bool pushNotificationsEnabled;
}

class AuthTokens {
  const AuthTokens(
    this.accessToken,
    this.refreshToken,
    this.accessExpiresAt,
    this.refreshExpiresAt,
  );
  final String accessToken;
  final String refreshToken;
  final DateTime accessExpiresAt;
  final DateTime refreshExpiresAt;
  @override
  String toString() => 'AuthTokens([redacted])';
}

class SavedRefresh {
  const SavedRefresh(this.origin, this.token, this.expiresAt);
  final String origin;
  final String token;
  final DateTime expiresAt;
  @override
  String toString() => 'SavedRefresh([redacted])';
}

abstract interface class AuthApi {
  String get origin;
  Future<void> register(String email, String password);
  Future<AuthTokens> login(String email, String password);
  Future<AuthTokens> refresh(String token);
  Future<AuthAccount> me(String accessToken);
  Future<String?> updateNickname(String accessToken, String? nickname);
  Future<bool> updatePushNotifications(String accessToken, bool enabled);
  Future<void> logout(String accessToken);
  Future<void> deleteAccount(String accessToken, String password);
}

class AccountDeletionOutcome {
  const AccountDeletionOutcome({
    required this.confirmed,
    required this.localCleared,
  });
  final bool confirmed;
  final bool localCleared;
}

abstract interface class RefreshVault {
  Future<SavedRefresh?> read();
  Future<void> write(SavedRefresh value);
  Future<void> clear();
}

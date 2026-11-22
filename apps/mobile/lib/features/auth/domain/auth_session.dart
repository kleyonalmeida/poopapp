import 'auth_models.dart';

class AuthSession {
  Future<AccountDeletionOutcome> deleteAccount(
    String password,
  ) => _exclusive(() async {
    // Only the safe read may renew/retry. Never replay the destructive request.
    await _account();
    var confirmed = false;
    try {
      await api.deleteAccount(_tokens!.accessToken, password);
      confirmed = true;
    } on AuthFailure catch (error) {
      if ({
        AuthFailureKind.forbidden,
        AuthFailureKind.invalidInput,
        AuthFailureKind.throttled,
        AuthFailureKind.conflict,
      }.contains(error.kind)) {
        rethrow;
      }
    }
    _tokens = null;
    var localCleared = true;
    try {
      await vault.clear();
    } catch (_) {
      localCleared = false;
    }
    return AccountDeletionOutcome(
      confirmed: confirmed,
      localCleared: localCleared,
    );
  });
  AuthSession(this.api, this.vault, {DateTime Function()? now})
    : _now = now ?? DateTime.now;
  final AuthApi api;
  final RefreshVault vault;
  final DateTime Function() _now;
  AuthTokens? _tokens;
  Future<void>? _tail;

  Future<T> _exclusive<T>(Future<T> Function() action) {
    final result = (_tail ?? Future<void>.value()).then((_) => action());
    _tail = result.then<void>((_) {}, onError: (Object _, StackTrace _) {});
    return result;
  }

  Future<AuthAccount?> restore() => _exclusive(() async {
    final origin = api.origin;
    if (_tokens != null) return _account();
    final saved = await _read();
    if (saved == null) return null;
    if (saved.origin != origin || !saved.expiresAt.isAfter(_now())) {
      await _forget();
      return null;
    }
    await _rotate(saved);
    return _account(canRefresh: false);
  });

  Future<AuthAccount> login(String email, String password) =>
      _exclusive(() async {
        if (api.origin.isEmpty) {
          throw const AuthFailure(AuthFailureKind.configuration);
        }
        if (_tokens != null) await _logout();
        await _forget();
        await _persist(await api.login(email.trim(), password));
        return _account(canRefresh: false);
      });

  Future<void> register(String email, String password) =>
      _exclusive(() => api.register(email.trim(), password));
  Future<AuthAccount> account() => _exclusive(() => _account());
  Future<String?> updateNickname(String? nickname) => _exclusive(
    () => _withAccess((access) => api.updateNickname(access, nickname)),
  );

  Future<bool> updatePushNotifications(bool enabled) => _exclusive(
    () => _withAccess((access) => api.updatePushNotifications(access, enabled)),
  );
  Future<bool> logout() => _exclusive(_logout);

  Future<SavedRefresh?> _read() async {
    try {
      return await vault.read();
    } catch (_) {
      throw const AuthFailure(AuthFailureKind.storage);
    }
  }

  Future<void> _forget() async {
    _tokens = null;
    try {
      await vault.clear();
    } catch (_) {
      throw const AuthFailure(AuthFailureKind.storage);
    }
  }

  Future<void> _persist(AuthTokens tokens) async {
    try {
      await vault.write(
        SavedRefresh(api.origin, tokens.refreshToken, tokens.refreshExpiresAt),
      );
    } catch (_) {
      _tokens = null;
      try {
        await api.logout(tokens.accessToken);
      } catch (_) {}
      try {
        await vault.clear();
      } catch (_) {}
      throw const AuthFailure(AuthFailureKind.storage);
    }
    _tokens = tokens;
  }

  Future<AuthTokens> _rotate(SavedRefresh saved) async {
    await _forget(); // Consume locally before sending: never replay after a crash or uncertain response.
    if (saved.origin != api.origin || !saved.expiresAt.isAfter(_now())) {
      throw const AuthFailure(AuthFailureKind.expired);
    }
    try {
      final tokens = await api.refresh(saved.token);
      await _persist(tokens);
      return tokens;
    } on AuthFailure catch (error) {
      if (error.kind == AuthFailureKind.storage) rethrow;
      throw const AuthFailure(AuthFailureKind.expired);
    }
  }

  SavedRefresh _saved(AuthTokens value) =>
      SavedRefresh(api.origin, value.refreshToken, value.refreshExpiresAt);

  Future<T> _withAccess<T>(
    Future<T> Function(String) action, {
    bool canRefresh = true,
  }) async {
    var tokens = _tokens;
    if (tokens == null) throw const AuthFailure(AuthFailureKind.expired);
    var rotated = false;
    if (!tokens.refreshExpiresAt.isAfter(_now())) {
      await _forget();
      throw const AuthFailure(AuthFailureKind.expired);
    }
    if (canRefresh &&
        !tokens.accessExpiresAt.isAfter(
          _now().add(const Duration(seconds: 30)),
        )) {
      tokens = await _rotate(_saved(tokens));
      rotated = true;
    }
    try {
      return await action(tokens.accessToken);
    } on AuthFailure catch (error) {
      if (error.kind != AuthFailureKind.unauthorized) rethrow;
      if (canRefresh && !rotated) {
        final fresh = await _rotate(_saved(tokens));
        try {
          return await action(fresh.accessToken);
        } on AuthFailure catch (retryError) {
          if (retryError.kind != AuthFailureKind.unauthorized) rethrow;
        }
      }
      await _forget();
      throw const AuthFailure(AuthFailureKind.expired);
    }
  }

  Future<AuthAccount> _account({bool canRefresh = true}) =>
      _withAccess(api.me, canRefresh: canRefresh);

  Future<bool> _logout() async {
    try {
      if (_tokens == null) {
        final saved = await _read();
        if (saved == null) return true;
        await _rotate(saved);
      }
      await _withAccess(api.logout);
      return true;
    } on AuthFailure {
      return false;
    } finally {
      await _forget();
    }
  }
}

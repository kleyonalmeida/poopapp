import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/dio_auth_api.dart';
import '../data/secure_refresh_vault.dart';
import '../domain/auth_models.dart';
import '../domain/auth_session.dart';

final authSessionProvider = Provider<AuthSession>((ref) {
  try {
    final api = DioAuthApi(const String.fromEnvironment('TRONO_API_URL'));
    ref.onDispose(api.close);
    return AuthSession(api, SecureRefreshVault());
  } on AuthFailure {
    return AuthSession(_UnavailableApi(), SecureRefreshVault());
  }
});

class _UnavailableApi implements AuthApi {
  @override
  Future<void> deleteAccount(String accessToken, String password) async =>
      throw const AuthFailure(AuthFailureKind.configuration);
  @override
  String get origin => throw const AuthFailure(AuthFailureKind.configuration);
  @override
  Future<void> register(String email, String password) async =>
      throw const AuthFailure(AuthFailureKind.configuration);
  @override
  Future<AuthTokens> login(String email, String password) async =>
      throw const AuthFailure(AuthFailureKind.configuration);
  @override
  Future<AuthTokens> refresh(String token) async =>
      throw const AuthFailure(AuthFailureKind.configuration);
  @override
  Future<AuthAccount> me(String accessToken) async =>
      throw const AuthFailure(AuthFailureKind.configuration);
  @override
  Future<void> logout(String accessToken) async =>
      throw const AuthFailure(AuthFailureKind.configuration);
  @override
  Future<String?> updateNickname(String accessToken, String? nickname) async =>
      throw const AuthFailure(AuthFailureKind.configuration);
  @override
  Future<bool> updatePushNotifications(
    String accessToken,
    bool enabled,
  ) async => throw const AuthFailure(AuthFailureKind.configuration);
}

class AuthView {
  const AuthView({this.account, this.busy = false, this.message, this.failure});
  final AuthAccount? account;
  final bool busy;
  final String? message;
  final AuthFailureKind? failure;
}

final authControllerProvider = NotifierProvider<AuthController, AuthView>(
  AuthController.new,
);

class AuthController extends Notifier<AuthView> {
  int _revision = 0;
  @override
  AuthView build() {
    unawaited(Future.microtask(() => _restore(initial: true)));
    return const AuthView(busy: true);
  }

  bool _current(int revision) => ref.mounted && revision == _revision;
  AuthSession get _session => ref.read(authSessionProvider);

  Future<void> restore() => _restore();
  Future<void> _restore({bool initial = false}) async {
    if (!ref.mounted || (state.busy && !initial)) return;
    final revision = ++_revision;
    state = const AuthView(busy: true);
    try {
      final account = await _session.restore();
      if (_current(revision)) state = AuthView(account: account);
    } catch (error) {
      if (_current(revision)) state = _failure(error);
    }
  }

  Future<bool> submit(
    String email,
    String password, {
    required bool register,
  }) async {
    if (state.busy) return false;
    final revision = ++_revision;
    state = const AuthView(busy: true);
    try {
      if (register) {
        await _session.register(email, password);
        if (!_current(revision)) return false;
        state = const AuthView(message: 'Conta criada. Entre para continuar.');
      } else {
        final account = await _session.login(email, password);
        if (!_current(revision)) return false;
        state = AuthView(account: account);
      }
      return true;
    } catch (error) {
      if (_current(revision)) state = _failure(error);
      return false;
    }
  }

  Future<void> reload() async {
    if (state.busy) return;
    final previous = state.account;
    final revision = ++_revision;
    state = AuthView(account: previous, busy: true);
    try {
      final account = await _session.account();
      if (_current(revision)) state = AuthView(account: account);
    } catch (error) {
      if (_current(revision)) {
        final failure = _failure(error);
        final mayKeep =
            failure.failure == AuthFailureKind.network ||
            failure.failure == AuthFailureKind.service;
        state = AuthView(
          account: mayKeep ? previous : null,
          message: failure.message,
          failure: failure.failure,
        );
      }
    }
  }

  Future<bool> saveNickname(String? nickname) => _saveAccountChange(
    (account) async => AuthAccount(
      account.id,
      account.email,
      account.emailConfirmed,
      nickname: await _session.updateNickname(nickname),
      pushNotificationsEnabled: account.pushNotificationsEnabled,
    ),
  );

  Future<void> savePushNotifications(bool enabled) async {
    await _saveAccountChange(
      (account) async => AuthAccount(
        account.id,
        account.email,
        account.emailConfirmed,
        nickname: account.nickname,
        pushNotificationsEnabled: await _session.updatePushNotifications(
          enabled,
        ),
      ),
    );
  }

  Future<bool> _saveAccountChange(
    Future<AuthAccount> Function(AuthAccount) save,
  ) async {
    final account = state.account;
    if (state.busy || account == null) return false;
    final revision = ++_revision;
    state = AuthView(account: account, busy: true);
    try {
      final saved = await save(account);
      if (!_current(revision)) return false;
      state = AuthView(account: saved);
      return true;
    } catch (error) {
      if (_current(revision)) {
        final failure = _failure(error);
        final signedOut =
            failure.failure == AuthFailureKind.expired ||
            failure.failure == AuthFailureKind.storage;
        state = AuthView(
          account: signedOut ? null : account,
          message: failure.message,
          failure: failure.failure,
        );
      }
      return false;
    }
  }

  Future<void> logout() async {
    final revision = ++_revision;
    state = const AuthView(
      busy: true,
    ); // Hide private data before waiting for in-flight work.
    try {
      final confirmed = await _session.logout();
      if (_current(revision)) {
        state = AuthView(
          message: confirmed
              ? 'Você saiu da conta.'
              : 'Você saiu deste aparelho. Não foi possível confirmar a saída no servidor.',
        );
      }
    } catch (error) {
      if (_current(revision)) state = _failure(error);
    }
  }

  Future<void> deleteAccount(String password) async {
    if (state.busy) return;
    final previous = state.account;
    final revision = ++_revision;
    state = AuthView(account: previous, busy: true);
    try {
      final result = await _session.deleteAccount(password);
      if (!_current(revision)) return;
      state = AuthView(
        message: result.confirmed
            ? result.localCleared
                  ? 'Conta excluída.'
                  : 'Conta excluída. Não foi possível limpar o acesso salvo neste aparelho.'
            : result.localCleared
            ? 'Não foi possível confirmar a exclusão. Você saiu deste aparelho; entre novamente para verificar.'
            : 'Não foi possível confirmar a exclusão nem limpar o acesso salvo neste aparelho. Tente novamente.',
        failure: !result.localCleared
            ? AuthFailureKind.storage
            : result.confirmed
            ? null
            : AuthFailureKind.expired,
      );
    } catch (error) {
      if (!_current(revision)) return;
      final failure = _failure(error);
      final keep = {
        AuthFailureKind.forbidden,
        AuthFailureKind.invalidInput,
        AuthFailureKind.conflict,
        AuthFailureKind.throttled,
        AuthFailureKind.network,
        AuthFailureKind.service,
      }.contains(failure.failure);
      state = AuthView(
        account: keep ? previous : null,
        failure: failure.failure,
        message: failure.failure == AuthFailureKind.forbidden
            ? 'Não foi possível confirmar a exclusão. Verifique sua senha.'
            : failure.failure == AuthFailureKind.conflict
            ? 'Não foi possível concluir a exclusão. Atualize sua conta e tente novamente.'
            : failure.message,
      );
    }
  }

  AuthView _failure(Object error) {
    final kind = error is AuthFailure ? error.kind : AuthFailureKind.service;
    final message = switch (kind) {
      AuthFailureKind.unauthorized => 'E-mail ou senha inválidos.',
      AuthFailureKind.forbidden => 'Não foi possível confirmar a solicitação.',
      AuthFailureKind.expired => 'Entre novamente para continuar.',
      AuthFailureKind.network =>
        'Não foi possível conectar. Confira sua conexão e tente novamente.',
      AuthFailureKind.storage =>
        'Não foi possível acessar o armazenamento seguro. Tente novamente.',
      AuthFailureKind.configuration =>
        'Acesso à conta indisponível no momento.',
      AuthFailureKind.throttled =>
        'Muitas tentativas. Aguarde e tente novamente.',
      AuthFailureKind.conflict =>
        'Não foi possível criar a conta. Tente entrar ou use outro e-mail.',
      AuthFailureKind.invalidInput => 'Confira os dados informados.',
      AuthFailureKind.invalidResponse =>
        'Não foi possível validar a resposta do servidor.',
      AuthFailureKind.service =>
        'Serviço indisponível. Tente novamente mais tarde.',
    };
    return AuthView(message: message, failure: kind);
  }
}

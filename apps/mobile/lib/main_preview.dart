import 'package:flutter/material.dart';
import 'app/app.dart';
import 'features/auth/data/dio_auth_api.dart';
import 'features/auth/data/preview_refresh_vault.dart';
import 'features/auth/domain/auth_session.dart';

/// Browser preview of the mobile UI, served beside the API through local TLS.
void main() {
  WidgetsFlutterBinding.ensureInitialized();
  final api = DioAuthApi(Uri.base.origin);
  runApp(MyApp(session: AuthSession(api, PreviewRefreshVault())));
}

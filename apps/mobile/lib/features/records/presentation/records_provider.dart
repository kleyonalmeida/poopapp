import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../domain/records.dart';
import '../../auth/domain/auth_models.dart';
import '../../auth/presentation/auth_providers.dart';
import '../data/dio_records_repository.dart';

final recordsRepositoryProvider = Provider<RecordsRepository>((ref) {
  ref.watch(authControllerProvider.select((state) => state.account?.id));
  final session = ref.read(authSessionProvider);
  final repository = DioRecordsRepository(session.api.origin, session);
  ref.onDispose(repository.close);
  return repository;
});

bool recordSessionExpired(Object error) =>
    error is AuthFailure &&
    {AuthFailureKind.expired, AuthFailureKind.storage}.contains(error.kind);

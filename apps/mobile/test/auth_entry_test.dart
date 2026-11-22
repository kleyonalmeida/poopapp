import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trono/main.dart';

void main() {
  testWidgets(
    'A tela inicial abre login e cadastro sem exibir dados privados',
    (tester) async {
      await tester.pumpWidget(const MyApp());
      await tester.pumpAndSettle();
      expect(find.text('Entrar'), findsOneWidget);
      await tester.ensureVisible(find.text('Entrar'));
      await tester.tap(find.text('Entrar'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('email')), findsOneWidget);
      expect(find.byKey(const Key('password')), findsOneWidget);
      expect(find.text('Minha conta'), findsNothing);
      await tester.tap(find.text('Criar conta'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('password-confirmation')), findsOneWidget);
    },
  );
}

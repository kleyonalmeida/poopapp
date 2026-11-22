import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:trono/main.dart';

void main() {
  testWidgets('Apresenta TRONO e explica privacidade por padrão', (
    tester,
  ) async {
    await tester.pumpWidget(const MyApp());
    expect(find.text('TRONO'), findsOneWidget);
    expect(find.text('Seu diário começa aqui.'), findsOneWidget);
    expect(
      find.text('Seus registros são privados por padrão.'),
      findsOneWidget,
    );
    expect(
      find.text('Você decide o que compartilhar e com quem.'),
      findsOneWidget,
    );
  });

  testWidgets('Permite leitura em tela pequena com texto ampliado', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 568);
    tester.view.devicePixelRatio = 1;
    tester.platformDispatcher.textScaleFactorTestValue = 2;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);

    await tester.pumpWidget(const MyApp());
    await tester.ensureVisible(
      find.text('Você decide o que compartilhar e com quem.'),
    );
    expect(tester.takeException(), isNull);
    expect(
      find.text('Você decide o que compartilhar e com quem.').hitTestable(),
      findsOneWidget,
    );
  });
}

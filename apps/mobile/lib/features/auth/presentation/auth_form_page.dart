import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../domain/auth_models.dart';
import 'auth_providers.dart';

class AuthFormPage extends ConsumerStatefulWidget {
  const AuthFormPage({super.key, required this.register});
  final bool register;
  @override
  ConsumerState<AuthFormPage> createState() => _AuthFormPageState();
}

class _AuthFormPageState extends ConsumerState<AuthFormPage> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _confirmation = TextEditingController();
  bool _obscure = true;
  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _confirmation.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    final success = await ref
        .read(authControllerProvider.notifier)
        .submit(_email.text, _password.text, register: widget.register);
    if (!mounted || !success) return;
    _password.clear();
    _confirmation.clear();
    if (widget.register) context.go('/entrar');
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    final disabled = auth.busy || auth.failure == AuthFailureKind.configuration;
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.register ? 'Criar conta' : 'Entrar'),
        leading: IconButton(
          tooltip: 'Voltar',
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/'),
        ),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: AutofillGroup(
                child: Form(
                  key: _form,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        widget.register
                            ? 'Um espaço só seu.'
                            : 'Bom ter você por aqui.',
                        style: Theme.of(context).textTheme.headlineMedium,
                      ),
                      const SizedBox(height: 16),
                      const Text('Seus registros são privados por padrão.'),
                      const SizedBox(height: 24),
                      if (auth.message != null) ...[
                        Semantics(
                          liveRegion: true,
                          child: Text(
                            auth.message!,
                            style: TextStyle(
                              color: auth.failure != null
                                  ? Theme.of(context).colorScheme.error
                                  : null,
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                      ],
                      TextFormField(
                        key: const Key('email'),
                        controller: _email,
                        enabled: !auth.busy,
                        decoration: const InputDecoration(
                          labelText: 'E-mail',
                          border: OutlineInputBorder(),
                        ),
                        keyboardType: TextInputType.emailAddress,
                        autofillHints: const [AutofillHints.username],
                        autocorrect: false,
                        textInputAction: TextInputAction.next,
                        validator: (value) =>
                            value != null &&
                                value.trim().length <= 254 &&
                                RegExp(
                                  r'^[^@\s<>,;]+@[^@\s<>,;]+$',
                                ).hasMatch(value.trim())
                            ? null
                            : 'Informe um e-mail válido.',
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        key: const Key('password'),
                        controller: _password,
                        enabled: !auth.busy,
                        obscureText: _obscure,
                        autocorrect: false,
                        enableSuggestions: false,
                        autofillHints: [
                          widget.register
                              ? AutofillHints.newPassword
                              : AutofillHints.password,
                        ],
                        decoration: InputDecoration(
                          labelText: 'Senha',
                          border: const OutlineInputBorder(),
                          suffixIcon: IconButton(
                            tooltip: _obscure
                                ? 'Mostrar senha'
                                : 'Ocultar senha',
                            icon: Icon(
                              _obscure
                                  ? Icons.visibility
                                  : Icons.visibility_off,
                            ),
                            onPressed: () =>
                                setState(() => _obscure = !_obscure),
                          ),
                        ),
                        validator: (value) {
                          if (value == null || value.isEmpty) {
                            return 'Informe sua senha.';
                          }
                          if (value.length > 128 ||
                              (widget.register &&
                                  (value.length < 12 ||
                                      value.trim().isEmpty))) {
                            return 'Use de 12 a 128 caracteres na senha.';
                          }
                          return null;
                        },
                      ),
                      if (widget.register) ...[
                        const SizedBox(height: 16),
                        TextFormField(
                          key: const Key('password-confirmation'),
                          controller: _confirmation,
                          enabled: !auth.busy,
                          obscureText: _obscure,
                          autocorrect: false,
                          enableSuggestions: false,
                          autofillHints: const [AutofillHints.newPassword],
                          decoration: const InputDecoration(
                            labelText: 'Confirmar senha',
                            border: OutlineInputBorder(),
                          ),
                          validator: (value) => value == _password.text
                              ? null
                              : 'As senhas precisam ser iguais.',
                        ),
                      ],
                      const SizedBox(height: 24),
                      FilledButton(
                        key: const Key('auth-submit'),
                        onPressed: disabled ? null : _submit,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 12),
                          child: auth.busy
                              ? const SizedBox(
                                  width: 24,
                                  height: 24,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : Text(
                                  widget.register
                                      ? 'Criar minha conta'
                                      : 'Entrar',
                                ),
                        ),
                      ),
                      const SizedBox(height: 8),
                      TextButton(
                        onPressed: auth.busy
                            ? null
                            : () => context.go(
                                widget.register ? '/entrar' : '/cadastro',
                              ),
                        child: Text(
                          widget.register ? 'Já tenho conta' : 'Criar conta',
                        ),
                      ),
                      if (!auth.busy &&
                          {
                            AuthFailureKind.network,
                            AuthFailureKind.service,
                            AuthFailureKind.storage,
                            AuthFailureKind.invalidResponse,
                          }.contains(auth.failure))
                        TextButton(
                          onPressed: () => ref
                              .read(authControllerProvider.notifier)
                              .restore(),
                          child: const Text('Tentar carregar minha conta'),
                        ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

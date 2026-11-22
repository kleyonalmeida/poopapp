#!/usr/bin/env python3
"""Smoke the local Flutter preview through its real HTTPS gateway (test accounts only)."""
import argparse
import json
import secrets
import ssl
import urllib.error
import urllib.parse
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', required=True)
    parser.add_argument('--ca')
    args = parser.parse_args()
    origin = args.url.rstrip('/')
    parsed = urllib.parse.urlparse(origin)
    assert parsed.hostname in {'localhost', '127.0.0.1'}, 'Use somente uma fixture local.'
    context = ssl.create_default_context(cafile=args.ca) if args.ca else ssl.create_default_context()

    def request(path, method='GET', data=None, access=None, extra=None):
        headers = {'Content-Type': 'application/json'}
        if access:
            headers['Authorization'] = 'Bearer ' + access
        if extra:
            headers.update(extra)
        req = urllib.request.Request(origin + path, method=method, headers=headers,
                                     data=None if data is None else json.dumps(data).encode())
        try:
            with urllib.request.urlopen(req, context=context, timeout=20) as response:
                return response.status, response.headers, response.read()
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read()

    status, _, html = request('/')
    assert status == 200, f'Interface ausente: GET / retornou {status}, esperado 200.'
    assert b'flutter_bootstrap.js' in html and b'name="viewport"' in html, 'Bootstrap mobile ausente.'
    assert parsed.scheme == 'https', 'O fluxo autenticado exige HTTPS.'
    status, _, bootstrap = request('/flutter_bootstrap.js')
    assert status == 200 and bootstrap, 'Bootstrap Flutter indisponível.'
    assert request('/health/ready')[2] == b'Healthy'
    assert request('/api/v1/me')[0] == 401
    # Client-supplied forwarding headers must not influence authorization.
    assert request('/api/v1/records', extra={'X-Forwarded-Proto': 'https', 'X-Forwarded-For': '203.0.113.5'})[0] == 401
    credentials = {'email': 'preview-' + secrets.token_hex(8) + '@example.test', 'password': secrets.token_hex(24)}
    assert request('/api/v1/auth/register', 'POST', credentials)[0] == 201
    status, headers, raw = request('/api/v1/auth/login', 'POST', credentials)
    assert status == 200 and 'no-store' in headers.get('Cache-Control', '')
    access = json.loads(raw)['accessToken']
    assert request('/api/v1/me', access=access)[0] == 200
    assert request('/api/v1/me/account', 'DELETE', {'password': credentials['password']}, access)[0] == 204
    assert request('/api/v1/me', access=access)[0] == 401
    print('Interface mobile, TLS, prontidão, cadastro/login, autorização e exclusão: OK')


if __name__ == '__main__':
    main()

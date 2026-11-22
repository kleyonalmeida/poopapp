#!/usr/bin/env python3
"""Build and test the preview in an isolated Compose project, then delete only its fixtures."""
import argparse
import json
import os
from pathlib import Path
import secrets
import ssl
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--browser', action='store_true', help='Also execute the Playwright test using PLAYWRIGHT_MODULE and CHROME_BIN.')
    args = parser.parse_args()
    environment = dict(os.environ)
    project = 'trono-preview-check-' + secrets.token_hex(6)
    password = secrets.token_hex(32)
    environment.update({'COMPOSE_PROJECT_NAME': project, 'POSTGRES_PASSWORD': password,
                        'TRONO_WEB_PORT': '0', 'TRONO_API_PORT': '0', 'TRONO_DB_PORT': '0',
                        'IDENTITY_SESSION_CLEANUP_ENABLED': 'false'})

    def run(command, *, env=environment):
        result = subprocess.run(command, cwd=ROOT, env=env, text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        if result.returncode:
            raise RuntimeError(result.stdout.replace(password, '[redacted]')[-8000:])
        return result.stdout.strip()

    try:
        print('Preparando fixture Docker isolada...', flush=True)
        run(['docker', 'compose', 'up', '-d'])
        migrated = run(['docker', 'compose', 'exec', '-T', 'postgres', 'psql', '-U', 'trono', '-d', 'trono', '-Atc',
                        '''SELECT to_regclass('identity."__EFMigrationsHistory"') IS NOT NULL
                        AND to_regclass('records."__EFMigrationsHistory"') IS NOT NULL;'''])
        assert migrated == 't', 'docker compose up -d não aplicou as migrations do banco novo.'
        assert run(['docker', 'compose', 'exec', '-T', 'postgres', 'psql', '-U', 'trono', '-d', 'trono', '-Atc', 'SELECT PostGIS_Version();']).startswith('3.5')
        with tempfile.TemporaryDirectory(prefix='trono-preview-test-') as folder:
            certificate = Path(folder) / 'localhost.crt'
            run(['docker', 'compose', 'cp', 'web:/tls/localhost.crt', str(certificate)])
            port = run(['docker', 'compose', 'port', 'web', '8443']).rsplit(':', 1)[-1]
            url = 'https://localhost:' + port
            trust = ssl.create_default_context(cafile=str(certificate))
            for _ in range(120):
                try:
                    with urllib.request.urlopen(url + '/health/ready', context=trust, timeout=3) as response:
                        if response.status == 200 and response.read() == b'Healthy':
                            break
                except (OSError, urllib.error.URLError):
                    pass
                time.sleep(0.25)
            else:
                raise AssertionError('A prévia não ficou pronta após docker compose up -d.')
            # A second plain up must preserve the persistent volume and certificate.
            certificate_before = certificate.read_bytes()
            run(['docker', 'compose', 'exec', '-T', 'postgres', 'psql', '-U', 'trono', '-d', 'trono', '-v', 'ON_ERROR_STOP=1', '-c',
                 'CREATE TABLE public.compose_preview_probe (id integer PRIMARY KEY); INSERT INTO public.compose_preview_probe VALUES (1);'])
            run(['docker', 'compose', 'up', '-d'])
            assert run(['docker', 'compose', 'exec', '-T', 'postgres', 'psql', '-U', 'trono', '-d', 'trono', '-Atc',
                        'SELECT count(*) FROM public.compose_preview_probe;']) == '1'
            run(['docker', 'compose', 'cp', 'web:/tls/localhost.crt', str(certificate)])
            assert certificate.read_bytes() == certificate_before
            print('docker compose up -d: banco novo migrado; repetição preserva dados e certificado: OK', flush=True)
            print(run(['python3', 'scripts/test_preview_http.py', '--url', url, '--ca', str(certificate)]), flush=True)
            api_port = run(['docker', 'compose', 'port', 'api', '8080']).rsplit(':', 1)[-1]
            # The direct HTTP probe remains available; authentication over HTTP must stay forbidden.
            try:
                urllib.request.urlopen('http://127.0.0.1:' + api_port + '/api/v1/me', timeout=10)
            except urllib.error.HTTPError as error:
                assert error.code == 400
            else:
                raise AssertionError('HTTP de identidade não foi rejeitado.')
            if args.browser:
                browser_env = dict(environment, TRONO_PREVIEW_URL=url, TRONO_PREVIEW_CA=str(certificate))
                print(run(['node', 'scripts/test_preview_browser.cjs'], env=browser_env), flush=True)
            blocked_env = dict(environment, COMPOSE_PROJECT_NAME=project + '-blocked')
            override = Path(folder) / 'blocked-migration.json'
            override.write_text(json.dumps({'services': {'migrate': {'environment': {'ASPNETCORE_ENVIRONMENT': 'Production'}}}}))
            blocked = ['docker', 'compose', '-f', str(ROOT / 'docker-compose.yml'), '-f', str(override)]
            try:
                failed = subprocess.run([*blocked, 'up', '-d'], cwd=ROOT, env=blocked_env, text=True,
                                        stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                assert failed.returncode != 0, 'Falha do migrador não bloqueou a subida.'
                running = run([*blocked, 'ps', '--services', '--status', 'running'], env=blocked_env).splitlines()
                assert 'api' not in running and 'web' not in running, 'API/web iniciaram apesar da falha da migração.'
                print('Falha da migração impede iniciar API/web: OK', flush=True)
            finally:
                run([*blocked, 'down', '--volumes', '--remove-orphans', '--rmi', 'local'], env=blocked_env)
            # Rejected query credentials must not leak through nginx error logs when the upstream fails.
            run(['docker', 'compose', 'stop', 'api'])
            marker = 'trono-query-not-for-logs'
            try:
                urllib.request.urlopen(url + '/api/v1/me?access_token=' + marker,
                                       context=ssl.create_default_context(cafile=str(certificate)), timeout=10)
            except urllib.error.HTTPError as error:
                assert error.code in {502, 504}
            else:
                raise AssertionError('A fixture deveria estar sem upstream.')
            logs = run(['docker', 'compose', 'logs', '--no-color', 'web', 'api'])
            assert marker not in logs, 'Query sensível apareceu nos logs do gateway.'
            print('Falha do upstream sem vazamento de query nos logs: OK', flush=True)
    finally:
        run(['docker', 'compose', 'down', '--volumes', '--remove-orphans', '--rmi', 'local'])
        print('Somente o projeto de teste, seus volumes e imagens locais foram removidos.', flush=True)


if __name__ == '__main__':
    main()

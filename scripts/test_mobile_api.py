#!/usr/bin/env python3
"""Run the Dart HTTP client against a disposable PostgreSQL + HTTPS API on Linux/WSL."""
import argparse
import json
import os
from pathlib import Path
import secrets
import socket
import ssl
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--flutter', default='flutter')
    args = parser.parse_args()
    redactions = []

    def run(command, *, env=None, cwd=ROOT):
        result = subprocess.run(command, cwd=cwd, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        if result.returncode:
            output = result.stdout
            for value in redactions:
                output = output.replace(value, '[redacted]')
            raise RuntimeError(f'{command[0]} failed:\n{output[-6000:]}')
        return result.stdout

    with tempfile.TemporaryDirectory(prefix='trono-mobile-api-') as temp:
        folder = Path(temp)
        password, cert_password = secrets.token_hex(24), secrets.token_hex(24)
        redactions.extend([password, cert_password])
        postgres_env = folder / 'postgres.env'
        postgres_env.write_text(f'POSTGRES_USER=trono\nPOSTGRES_DB=trono_mobile_test\nPOSTGRES_PASSWORD={password}\n')
        postgres_env.chmod(0o600)
        cert_pass = folder / 'cert-password'
        cert_pass.write_text(cert_password + '\n')
        cert_pass.chmod(0o600)
        name = 'trono-mobile-e2e-' + uuid.uuid4().hex[:12]
        api = None
        container_created = False
        api_log = folder / 'api.log'
        try:
            print('Preparando PostgreSQL descartável e certificado TLS local...', flush=True)
            run(['docker', 'run', '--detach', '--name', name, '--env-file', str(postgres_env),
                 '-p', '127.0.0.1::5432', 'postgis/postgis:17-3.5'])
            container_created = True
            for _ in range(100):
                ready = subprocess.run(['docker', 'exec', name, 'pg_isready', '-h', '127.0.0.1', '-U', 'trono', '-d', 'trono_mobile_test'],
                                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                if ready.returncode == 0:
                    break
                time.sleep(0.2)
            else:
                raise RuntimeError('PostgreSQL não ficou pronto.')
            port = json.loads(run(['docker', 'inspect', name]))[0]['NetworkSettings']['Ports']['5432/tcp'][0]['HostPort']
            run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '1', '-subj', '/CN=localhost',
                 '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-keyout', str(folder / 'key.pem'), '-out', str(folder / 'cert.pem')])
            (folder / 'key.pem').chmod(0o600)
            run(['openssl', 'pkcs12', '-export', '-out', str(folder / 'server.pfx'), '-inkey', str(folder / 'key.pem'),
                 '-in', str(folder / 'cert.pem'), '-passout', 'file:' + str(cert_pass)])
            (folder / 'server.pfx').chmod(0o600)
            env = dict(os.environ)
            env['ConnectionStrings__Identity'] = f'Host=127.0.0.1;Port={port};Database=trono_mobile_test;Username=trono;Password={password}'
            print('Compilando API e aplicando migrations somente no banco descartável...', flush=True)
            run(['dotnet', 'restore', 'apps/api/src/Trono.Api/Trono.Api.csproj', '--disable-parallel'], env=env)
            run(['dotnet', 'tool', 'restore'], env=env)
            run(['dotnet', 'build', 'apps/api/src/Trono.Api/Trono.Api.csproj', '-c', 'Release', '--no-restore', '-m:1', '-p:UseSharedCompilation=false'], env=env)
            run(['dotnet', 'ef', 'database', 'update', '--project', 'apps/api/src/Trono.Modules.Identity', '--startup-project',
                 'apps/api/src/Trono.Api', '--context', 'IdentityDbContext', '--configuration', 'Release', '--no-build'], env=env)
            run(['dotnet', 'ef', 'database', 'update', '--project', 'apps/api/src/Trono.Modules.Records', '--startup-project',
                 'apps/api/src/Trono.Api', '--context', 'RecordsDbContext', '--configuration', 'Release', '--no-build'], env=env)
            with socket.socket() as listener:
                listener.bind(('127.0.0.1', 0))
                api_port = listener.getsockname()[1]
            url = f'https://localhost:{api_port}'
            env.update({'ASPNETCORE_URLS': url, 'ASPNETCORE_Kestrel__Certificates__Default__Path': str(folder / 'server.pfx'),
                        'ASPNETCORE_Kestrel__Certificates__Default__Password': cert_password})
            with api_log.open('w') as log:
                api = subprocess.Popen(['dotnet', str(ROOT / 'apps/api/src/Trono.Api/bin/Release/net10.0/Trono.Api.dll')],
                                       cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
            trust = ssl.create_default_context(cafile=str(folder / 'cert.pem'))
            for _ in range(100):
                if api.poll() is not None:
                    raise RuntimeError('A API encerrou durante a inicialização.')
                try:
                    with urllib.request.urlopen(url + '/health/live', context=trust, timeout=1) as response:
                        if response.read() == b'Healthy':
                            break
                except (OSError, urllib.error.URLError):
                    time.sleep(0.2)
            else:
                raise RuntimeError('A API HTTPS não ficou pronta.')
            print('Executando cliente Dio por HTTPS com certificado verificado...', flush=True)
            output = run([args.flutter, 'test', 'test_live/auth_api_live_test.dart',
                          '--dart-define=TRONO_TEST_API_URL=' + url, '--dart-define=TRONO_TEST_CA=' + str(folder / 'cert.pem')],
                         cwd=ROOT / 'apps/mobile', env=env)
            print(output, flush=True)
        finally:
            if api is not None:
                api.terminate()
                try:
                    api.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    api.kill()
                    api.wait()
            if container_created:
                # This uniquely named container and its anonymous volume were created above for this fixture only.
                subprocess.run(['docker', 'rm', '-f', '-v', name], stdout=subprocess.DEVNULL, check=True)
            print('Recursos descartáveis encerrados.', flush=True)


if __name__ == '__main__':
    main()

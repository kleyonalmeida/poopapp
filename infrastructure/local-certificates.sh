#!/bin/sh
set -eu
if [ -f /tls/localhost.crt ] && [ -f /tls/localhost.key ]; then
  if ! openssl x509 -checkend 0 -noout -in /tls/localhost.crt > /dev/null; then
    echo 'Certificado local inválido ou expirado; revise o volume TLS antes de iniciar.' >&2
    exit 1
  fi
  echo 'Certificado local existente preservado.'
  exit 0
fi
if [ -e /tls/localhost.crt ] || [ -e /tls/localhost.key ]; then
  echo 'Configuração TLS incompleta; revise o volume de certificados.' >&2
  exit 1
fi
umask 077
openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 365 \
  -subj '/CN=TRONO local development' \
  -addext 'subjectAltName=DNS:localhost,DNS:api,IP:127.0.0.1' \
  -addext 'basicConstraints=critical,CA:FALSE' \
  -addext 'extendedKeyUsage=serverAuth' \
  -keyout /tls/localhost.key -out /tls/localhost.crt 2>/dev/null
# The ASP.NET runtime image runs as its built-in app UID; nginx master reads as root.
chown 1654:1654 /tls/localhost.key
chmod 600 /tls/localhost.key
chmod 644 /tls/localhost.crt
echo 'Certificado de desenvolvimento criado para localhost, 127.0.0.1 e api.'

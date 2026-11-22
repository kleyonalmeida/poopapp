FROM nginx:1.30.7-alpine AS web-base
RUN apk add --no-cache openssl curl
COPY infrastructure/local-certificates.sh /usr/local/bin/trono-local-certificates

FROM web-base AS certificates
ENTRYPOINT ["/bin/sh", "/usr/local/bin/trono-local-certificates"]

FROM debian:bookworm-slim AS flutter-build
RUN apt-get update && apt-get install --yes --no-install-recommends ca-certificates curl git unzip xz-utils \
    && rm -rf /var/lib/apt/lists/*
ARG FLUTTER_VERSION=3.44.2
RUN git clone --depth 1 --branch "$FLUTTER_VERSION" https://github.com/flutter/flutter.git /opt/flutter
ENV PATH="/opt/flutter/bin:${PATH}"
RUN flutter config --no-analytics --enable-web && flutter precache --web
WORKDIR /app
COPY apps/mobile/pubspec.yaml apps/mobile/pubspec.lock ./
RUN flutter pub get --enforce-lockfile
COPY apps/mobile/lib/ lib/
COPY apps/mobile/web/ web/
RUN flutter build web --release --no-pub --target lib/main_preview.dart --no-web-resources-cdn --pwa-strategy=none

FROM web-base AS web
COPY infrastructure/nginx-preview.conf /etc/nginx/conf.d/default.conf
COPY --from=flutter-build /app/build/web/ /usr/share/nginx/html/
EXPOSE 8443
HEALTHCHECK --interval=5s --timeout=5s --start-period=15s --retries=20 \
  CMD curl --fail --silent --cacert /tls/localhost.crt https://localhost:8443/health/ready > /dev/null || exit 1

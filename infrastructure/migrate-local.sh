#!/bin/sh
set -eu
if [ "${ASPNETCORE_ENVIRONMENT:-}" != Development ]; then
  echo 'Este comando de migrations é exclusivo do desenvolvimento local.' >&2
  exit 1
fi
: "${ConnectionStrings__Identity:?Configure a conexão do banco local.}"
dotnet ef database update --context IdentityDbContext \
  --project apps/api/src/Trono.Modules.Identity --startup-project apps/api/src/Trono.Api \
  --configuration Release --no-build
dotnet ef database update --context RecordsDbContext \
  --project apps/api/src/Trono.Modules.Records --startup-project apps/api/src/Trono.Api \
  --configuration Release --no-build

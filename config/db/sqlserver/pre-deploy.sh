#!/usr/bin/env bash
set -euo pipefail
: "${SERVICE:?SERVICE must be set}"
[[ "$SERVICE" =~ ^[a-z0-9][a-z0-9-]*$ ]] || { echo 'Invalid SERVICE' >&2; exit 1; }
data_dir="/opt/docker/${SERVICE}/mssql-2022"
kamal server exec --no-interactive -d sqlserver "mkdir -p '$data_dir' && chown 10001:0 '$data_dir'"
if kamal server exec --no-interactive -d sqlserver "docker inspect '${SERVICE}-sqlserver'" >/dev/null 2>&1; then
    kamal accessory start sqlserver -d sqlserver
else
    kamal accessory boot sqlserver -d sqlserver
fi
# Credentials are read inside the accessory, never expanded into the SSH command.
kamal server exec --no-interactive -d sqlserver "docker exec '${SERVICE}-sqlserver' bash /tmp/next-license-init.sh"

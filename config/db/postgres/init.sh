#!/bin/sh
set -eu

psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set=app_password="$DB_PASSWORD" <<'SQL'
CREATE ROLE next_license WITH LOGIN PASSWORD :'app_password';
CREATE DATABASE next_license OWNER next_license;
SQL

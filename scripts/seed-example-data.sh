#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd -- "$script_dir/../MyApp" && pwd)"

DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project "$project_dir" --no-launch-profile --AppTasks=seed-example-data

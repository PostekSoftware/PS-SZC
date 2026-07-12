#!/usr/bin/env bash
set -euo pipefail

source_path="$1"
output_path="$2"

if [[ ! -f "$source_path" ]]; then
  echo "macOS print helper source not found: $source_path" >&2
  exit 1
fi

swiftc -O -o "$output_path" "$source_path"
chmod +x "$output_path"

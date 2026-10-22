#!/bin/bash
set -e
if [ ! -f /app/data/app.config ]; then
  cp /app/default.app.config /app/data/app.config
fi
exec dotnet SphereSSLv2.dll
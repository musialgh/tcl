#!/usr/bin/env bash
# Start the Azimutt stack. DB patching is handled by the patch service in docker-compose.
set -e

docker-compose down --remove-orphans || true
docker-compose up -d

echo "Done. Azimutt: https://azimutt.badweb.de  Mail: https://mailpit.badweb.de"

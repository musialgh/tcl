#!/usr/bin/env bash
# Start the Azimutt stack and patch plan_seats after migrations run.
set -e

COMPOSE="$HOME/.local/bin/docker-compose"
DB="database-db-1"

$COMPOSE up -d

echo "Waiting for Azimutt to finish migrations..."
until docker logs $DB 2>&1 | grep -q "database system is ready to accept connections"; do
  sleep 1
done
sleep 3

docker exec $DB psql -U azimutt -d azimutt -c \
  "UPDATE organizations SET plan = 'enterprise', plan_seats = 99 WHERE plan_seats IS NULL OR plan_seats < 99;"

echo "Done. Azimutt: http://localhost:4000  Mail: http://localhost:8025"

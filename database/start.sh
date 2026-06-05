#!/usr/bin/env bash
# Start the Azimutt stack and patch plan_seats after migrations run.
set -e

docker-compose down --remove-orphans || true
docker-compose up -d

DB=$(docker-compose ps -q db)

echo "Waiting for Postgres..."
until docker logs $DB 2>&1 | grep -q "database system is ready to accept connections"; do
  sleep 1
done

echo "Waiting for Azimutt migrations..."
until curl -s http://localhost:4000 > /dev/null 2>&1; do
  sleep 2
done

docker exec $DB psql -U azimutt -d azimutt -c \
  "UPDATE organizations SET plan = 'enterprise', plan_seats = 99, plan_status = 'active', plan_freq = 'yearly', plan_validated = now();"

echo "Done. Azimutt: http://localhost:4000  Mail: http://localhost:8025"

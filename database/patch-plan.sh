#!/usr/bin/env bash
# Re-run the enterprise plan patch manually.
set -e

docker-compose restart patch

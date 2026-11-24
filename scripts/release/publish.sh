#!/usr/bin/env bash
set -euo pipefail
umask 077
revision=$(git rev-parse HEAD)
[[ "$revision" =~ ^[0-9a-f]{40}$ ]]
registry=ghcr.io/trunghieu220504
# Ephemeral auth prevents credentials persisting on a shared build agent.
export DOCKER_CONFIG
DOCKER_CONFIG=$(mktemp -d)
trap 'rm -f "$DOCKER_CONFIG/config.json"; rmdir "$DOCKER_CONFIG" 2>/dev/null || true' EXIT
printf '%s' "${REGISTRY_PASSWORD:?}" | docker login ghcr.io -u "${REGISTRY_USER:?}" --password-stdin
for component in api web migrations; do
    case "$component" in
        api) file=infra/docker/Dockerfile.api; context=. ;;
        web) file=web/Dockerfile; context=web ;;
        migrations) file=infra/docker/Dockerfile.migrations; context=artifacts/release/migrations ;;
    esac
    image="$registry/restaurant-$component:$revision"
    docker build --label "org.opencontainers.image.revision=$revision" \
        --label 'org.opencontainers.image.source=https://github.com/Trunghieu220504/restaurant-management-system' \
        -f "$file" -t "$image" "$context"
    docker push "$image"
done

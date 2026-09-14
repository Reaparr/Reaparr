#!/usr/bin/env bash
set -Eeuo pipefail

readonly registry="${REAPARR_REGISTRY:?Set REAPARR_REGISTRY to the private registry host, for example registry.example.com}"
readonly image="${registry}/reaparr/reaparr:dev-unraid"
readonly version="${VERSION:-9.9.9}"
readonly informational_version="${INFORMATIONAL_VERSION:-${version}-dev}"

cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."

echo "Building and pushing ${image}"
echo "Docker context: $(docker context show)"
echo "Docker daemon: $(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')"

docker buildx build \
    --progress=plain \
    --platform linux/amd64 \
    --file docker/Dockerfile \
    --tag "${image}" \
    --build-arg BUILDPLATFORM=linux/amd64 \
    --build-arg TARGETPLATFORM=linux/amd64 \
    --build-arg "VERSION=${version}" \
    --build-arg "INFORMATIONAL_VERSION=${informational_version}" \
    --provenance=false \
    --push \
    .

echo "Successfully pushed ${image}"

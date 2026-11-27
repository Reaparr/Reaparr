#!/usr/bin/env bash
set -Eeuo pipefail

readonly registry="${REAPARR_REGISTRY:?Set REAPARR_REGISTRY to the private registry host, for example registry.example.com}"
readonly image="${registry}/reaparr/reaparr:dev-unraid"
readonly version="${VERSION:-9.9.9}"
readonly informational_version="${INFORMATIONAL_VERSION:-${version}-dev}"

cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
report_graphql_errors() {
    local response="$1"
    local operation="$2"
    local endpoint="$3"

    if ! jq -e '(.errors // []) | length > 0' >/dev/null <<<"${response}"; then
        return 0
    fi

    echo "Unraid GraphQL ${operation} failed at ${endpoint}" >&2
    jq -r --arg operation "${operation}" '
        .errors[]? |
        (.extensions.code // "UNKNOWN") as $code |
        (if ((.path // []) | length) == 0 then "<root>" else (.path | map(tostring) | join(".")) end) as $path |
        (.message // "Unraid GraphQL request failed") as $message |
        "Unraid GraphQL \($operation) error [\($code)] at \($path): \($message)"
    ' <<<"${response}" >&2

    if jq -e 'any(.errors[]?.extensions?.code?; . == "FORBIDDEN")' >/dev/null <<<"${response}"; then
        echo "Verify REAPARR_UNRAID_URL points to the intended server and REAPARR_UNRAID_API_KEY is the key loaded in this shell with Docker UPDATE_ANY permission." >&2
    fi

    return 1
}

resolve_image_config_digest() {
    local ref="$1"
    local manifest
    local child_digest
    manifest="$(docker buildx imagetools inspect --raw "${ref}")" || return 1

    if jq -e '.manifests | type == "array"' >/dev/null <<<"${manifest}"; then
        # Buildx can publish an index even for one platform when provenance is enabled.
        child_digest="$(jq -er '
            [.manifests[] | select(
                .platform.os == "linux" and .platform.architecture == "amd64"
                and .annotations["vnd.docker.reference.type"] != "attestation-manifest"
            )] | select(length == 1) | .[0].digest | strings | select(startswith("sha256:"))
        ' <<<"${manifest}")" || {
            echo "Expected exactly one runnable linux/amd64 manifest for ${ref}" >&2
            return 1
        }
        ref="${ref%%@*}"
        if [[ "${ref##*/}" == *:* ]]; then ref="${ref%:*}"; fi
        manifest="$(docker buildx imagetools inspect --raw "${ref}@${child_digest}")" || return 1
    fi

    jq -er '.config.digest | strings | select(startswith("sha256:"))' <<<"${manifest}"
}

update_unraid_container() {
    local graphql_url="${REAPARR_UNRAID_URL%/}/graphql"
    local container_name="${REAPARR_UNRAID_CONTAINER:-Reaparr}"
    local expected_image="${REAPARR_UNRAID_IMAGE:-${image}}"
    local api_key="${REAPARR_UNRAID_API_KEY:?Set REAPARR_UNRAID_API_KEY when REAPARR_UNRAID_AUTO_UPDATE=true}"
    local containers_query='query { docker { containers { id names image imageId state } } }'
    local readiness_url="${REAPARR_UNRAID_READINESS_URL:?Set REAPARR_UNRAID_READINESS_URL to the Reaparr login or readiness URL}"
    local expected_image_id
    local response
    local container
    local container_id
    local actual_image
    local display_name
    local update_mutation="mutation UpdateContainer(\$id: PrefixedID!) { docker { updateContainer(id: \$id) { id names image state status } } }"
    local update_response
    local attempt
    local curl_resolve_args=()
    if [[ -n "${REAPARR_UNRAID_RESOLVE:-}" ]]; then
        # Keep TLS verification enabled while mapping the certificate hostname to a fixed IP.
        curl_resolve_args=(--resolve "${REAPARR_UNRAID_RESOLVE}")
    fi

    command -v curl >/dev/null 2>&1 || {
        echo "curl is required for the Unraid auto-update hook" >&2
        return 1
    }
    command -v jq >/dev/null 2>&1 || {
        echo "jq is required for the Unraid auto-update hook" >&2
        return 1
    }

    expected_image_id="$(resolve_image_config_digest "${expected_image}")"

    response="$(
        jq -cn --arg query "${containers_query}" '{query: $query}' |
            curl --fail --silent --show-error --max-time 15 \
                --request POST "${graphql_url}" \
                "${curl_resolve_args[@]}" \
                --header 'Content-Type: application/json' \
                --header "x-api-key: ${api_key}" \
                --data-binary @-
    )"

    report_graphql_errors "${response}" "container lookup" "${graphql_url}"

    container="$(
        jq -c --arg name "${container_name}" '
            first(
                .data.docker.containers[]?
                | select(any(.names[]?; ltrimstr("/") == $name))
            ) // empty
        ' <<<"${response}"
    )"

    if [[ -z "${container}" ]]; then
        echo "Unraid container '${container_name}' was not found" >&2
        return 1
    fi

    actual_image="$(jq -r '.image // empty' <<<"${container}")"
    if [[ "${actual_image}" != "${expected_image}" ]]; then
        echo "Refusing to update '${container_name}': expected image '${expected_image}', found '${actual_image}'" >&2
        return 1
    fi

    container_id="$(jq -r '.id // empty' <<<"${container}")"
    display_name="$(jq -r '(.names // []) | join(", ")' <<<"${container}")"

    if update_response="$(
        jq -cn \
            --arg query "${update_mutation}" \
            --arg id "${container_id}" \
            '{query: $query, variables: {id: $id}}' |
            curl --fail --silent --show-error --max-time 60 \
                --request POST "${graphql_url}" \
                "${curl_resolve_args[@]}" \
                --header 'Content-Type: application/json' \
                --header "x-api-key: ${api_key}" \
                --data-binary @-
    )"; then
        report_graphql_errors "${update_response}" "container update" "${graphql_url}"
    else
        echo "Unraid update response was unavailable; checking container state without repeating the update" >&2
    fi

    for ((attempt = 1; attempt <= 12; attempt++)); do
        if response="$(
            jq -cn --arg query "${containers_query}" '{query: $query}' |
                curl --fail --silent --show-error --max-time 5 \
                    --request POST "${graphql_url}" "${curl_resolve_args[@]}" \
                    --header 'Content-Type: application/json' --header "x-api-key: ${api_key}" --data-binary @-
        )" && jq -e --arg name "${container_name}" --arg image "${expected_image}" --arg id "${expected_image_id}" '
            [.data.docker.containers[]? | select(any(.names[]?; ltrimstr("/") == $name))]
            | length == 1 and .[0].image == $image and .[0].imageId == $id and .[0].state == "RUNNING"
        ' >/dev/null <<<"${response}" &&
            [[ "$(curl --silent --show-error --max-time 5 --output /dev/null --write-out '%{http_code}' "${readiness_url}")" == 2?? ]]; then
            echo "Verified ${display_name} is running ${expected_image_id} and Reaparr is ready"
            return 0
        fi
        if ((attempt < 12)); then sleep 3; fi
    done

    echo "Could not verify the new image and readiness for ${display_name}" >&2
    return 1
}



echo "Building and pushing ${image}"
echo "Docker context: $(docker context show)"
echo "Docker daemon: $(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')"

if [[ "${REAPARR_UNRAID_AUTO_UPDATE:-false}" == "true" ]]; then
    : "${REAPARR_UNRAID_URL:?Set REAPARR_UNRAID_URL when REAPARR_UNRAID_AUTO_UPDATE=true}"
    : "${REAPARR_UNRAID_READINESS_URL:?Set REAPARR_UNRAID_READINESS_URL to the Reaparr login or readiness URL}"
fi

docker buildx build \
    --progress=plain \
    --platform linux/amd64 \
    --file docker/Dockerfile \
    --tag "${image}" \
    --build-arg BUILDPLATFORM=linux/amd64 \
    --build-arg TARGETPLATFORM=linux/amd64 \
    --build-arg "VERSION=${version}" \
    --build-arg "INFORMATIONAL_VERSION=${informational_version}" \
    --push \
    .

echo "Successfully pushed ${image}"
if [[ "${REAPARR_UNRAID_AUTO_UPDATE:-false}" == "true" ]]; then
    update_unraid_container
fi

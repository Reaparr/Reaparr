# syntax=docker/dockerfile:1.20

ARG VERSION=0.0.0
ARG INFORMATIONAL_VERSION=0.0.0
ARG PORT=7000
ARG TARGETPLATFORM=linux/amd64
ARG BUILDPLATFORM=linux/amd64

# Stage 1 - Build the Nuxt front-end
FROM --platform=$BUILDPLATFORM oven/bun:1.4.0-alpine AS client-build

ARG PORT
ARG VERSION

ENV NUXT_HOST=0.0.0.0
ENV NUXT_PORT=${PORT}
ENV NUXT_PUBLIC_API_PORT=${PORT}
ENV NUXT_PUBLIC_VERSION=${VERSION}
# Required during static Nuxt generate so runtimeConfig.public.platform is baked as 'docker' in the client bundle.
ENV NUXT_PUBLIC_PLATFORM=docker

WORKDIR /tmp/ClientApp

## Copy the project files and install the dependencies
COPY ./src/AppHost/ClientApp/ ./
RUN bun install --frozen-lockfile && bun run generate --fail-on-error

# Stage 2 - Build the .NET back-end
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS back-end

ARG VERSION
ARG INFORMATIONAL_VERSION
ARG TARGETPLATFORM

WORKDIR /src

## Speed up and stabilize dotnet restore/publish
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    NUGET_XMLDOC_MODE=skip

COPY ["Directory.Build.props", "."]
COPY --parents src/*/*.csproj src/*/packages.lock.json /src/

## Restore Projects
RUN dotnet restore "src/AppHost/AppHost.csproj" --locked-mode

COPY . .
WORKDIR "/src/src/AppHost"

## Build and Publish
RUN case "${TARGETPLATFORM}" in \
      "linux/amd64") REAPARR_RUNTIME_IDENTIFIER="linux-x64" ;; \
      "linux/arm64") REAPARR_RUNTIME_IDENTIFIER="linux-arm64" ;; \
      *) echo "Unsupported TARGETPLATFORM: ${TARGETPLATFORM}"; exit 1 ;; \
    esac && \
    dotnet publish "AppHost.csproj" -c Release -o /app/publish \
    --no-restore \
    /p:Version=${VERSION} \
    /p:InformationalVersion=${INFORMATIONAL_VERSION} \
    /p:ReaparrRuntimeMode=docker \
    /p:ReaparrRuntimeIdentifier=${REAPARR_RUNTIME_IDENTIFIER} \
    -p:UseAppHost=false \
    -p:CSharpier_Bypass=true

# Stage 3a - .NET runtime (Alpine/musl-based; compatible with the Alpine final image)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine3.22 AS dotnet-runtime

# Stage 3b - glibc donor: Ubuntu noble ships glibc 2.39, satisfying dash-mpd-cli's >= 2.38 requirement.
# This stage is never included in the final image; only individual .so files are bind-mounted from it.
FROM ubuntu:24.04 AS glibc-source

# Stage 3c - build a static MP4Box binary from GPAC (gpac is not packaged for Alpine 3.22)
FROM alpine:3.22 AS gpac-builder

WORKDIR /src
RUN apk add --no-cache musl-dev pkgconfig git g++ binutils make zlib-dev zlib-static && \
    git clone --depth 1 https://github.com/gpac/gpac.git && \
    cd gpac && \
    ./configure --static-bin && \
    make -j 3

# Stage 4 - Final Image with s6-overlay
FROM ghcr.io/linuxserver/baseimage-alpine:3.22 AS final

ARG VERSION
ARG INFORMATIONAL_VERSION
ARG PORT
ARG TARGETPLATFORM
ARG S6_OVERLAY_VERSION="3.2.1.0"

## Set environment variables
ENV VERSION=${VERSION}
ENV INFORMATIONAL_VERSION=${INFORMATIONAL_VERSION}
ENV PORT=${PORT}
ENV DOTNET_ENVIRONMENT=Production
ENV DOTNET_URLS=http://+:${PORT}
ENV S6_SERVICES_GRACETIME=15000
ENV REAPARR_PLATFORM=docker
ENV UMASK=002

# Install Alpine dependencies (.NET runtime deps + dash-mpd-cli helpers)
RUN apk add --no-cache bash icu-libs krb5-libs libgcc libintl libssl3 libstdc++ zlib \
    ffmpeg mkvtoolnix

## Copy .NET runtime from official image
COPY --from=dotnet-runtime /usr/share/dotnet /usr/share/dotnet

## Copy glibc from the Ubuntu noble stage so that dash-mpd-cli (glibc-linked, requires >= 2.38)
## can execute on musl Alpine. Only the three libs the binary actually needs are copied.
## /lib64/ld-linux-*.so.2 is the ELF interpreter the kernel invokes for glibc-linked executables.
## dotnet itself uses the Alpine/musl runtime copied above, so there is no loader conflict.
RUN --mount=type=bind,from=glibc-source,source=/lib,target=/mnt/glibc-src \
    ARCH=$(uname -m) && \
    case "${ARCH}" in \
      x86_64) \
        TRIPLE=x86_64-linux-gnu; \
        LOADER=ld-linux-x86-64.so.2; \
        LOADER_DEST=/lib64/${LOADER}; \
        ;; \
      aarch64) \
        TRIPLE=aarch64-linux-gnu; \
        LOADER=ld-linux-aarch64.so.1; \
        LOADER_DEST=/lib/${LOADER}; \
        ;; \
      *) \
        echo "Unsupported architecture: ${ARCH}"; \
        exit 1; \
        ;; \
    esac && \
    mkdir -p /lib/${TRIPLE} /lib64 && \
    cp /mnt/glibc-src/${TRIPLE}/libc.so.6 /lib/${TRIPLE}/libc.so.6 && \
    cp /mnt/glibc-src/${TRIPLE}/libm.so.6 /lib/${TRIPLE}/libm.so.6 && \
    if [ -f "/mnt/glibc-src/${TRIPLE}/${LOADER}" ]; then \
      cp "/mnt/glibc-src/${TRIPLE}/${LOADER}" "${LOADER_DEST}"; \
    elif [ -f "/mnt/glibc-src/${LOADER}" ]; then \
      cp "/mnt/glibc-src/${LOADER}" "${LOADER_DEST}"; \
    else \
      echo "Could not locate ${LOADER} in glibc donor stage"; \
      exit 1; \
    fi

## Make dotnet command available
ENV PATH="$PATH:/usr/share/dotnet"

## Copy static MP4Box binary built from GPAC source
COPY --from=gpac-builder /src/gpac/bin/gcc/MP4Box /usr/local/bin/MP4Box

## Copy to final image
WORKDIR /app
COPY --from=back-end /app/publish .
COPY --from=client-build /tmp/ClientApp/.output/public /app/wwwroot
## Static assets are served by the non-root abc user; normalize restrictive source modes.
RUN chmod -R u=rwX,go=rX /app/wwwroot

## Copy the s6-overlay config files
COPY docker/etc/ /etc/

### This fixes the permissions on the config directory and Reaparr crashes if it can't write to it
COPY docker/10-config /etc/fix-attrs.d/10-config

## Disable LSIO cron service at image build time
## For s6-rc, disabling is done by removing service from the 'user' bundle contents.
RUN if [ -f /etc/s6-overlay/s6-rc.d/user/contents.d/svc-cron ]; then \
      rm -f /etc/s6-overlay/s6-rc.d/user/contents.d/svc-cron; \
    fi

EXPOSE ${PORT}
VOLUME /Config /Downloads /Movies /TvShows

# Note: Container starts as root to allow s6-overlay initialization.
# The s6 service script drops privileges and runs the app as 'abc' user (see s6-setuidgid abc in run script).

# Set the entrypoint to use s6-overlay
ENTRYPOINT ["/init"]

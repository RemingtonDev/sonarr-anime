FROM mcr.microsoft.com/dotnet/runtime:6.0-jammy@sha256:16b5401a53acf33cda5136bf09ae1653f338e7f9c60dc3eee6ca9f320b0f0b14 AS trust

# apt verifies Ubuntu's signed metadata and package hashes. Refuse an old mirror.
ARG CA_CERTIFICATES_MIN_VERSION=20260816ubuntu0.22.04.1
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates libsqlite3-0 libicu70 \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' ca-certificates)" ge "$CA_CERTIFICATES_MIN_VERSION" \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

FROM trust

WORKDIR /opt/sonarr

COPY Sonarr/ .

VOLUME /config /tv
EXPOSE 8989

ENTRYPOINT ["./Sonarr", "-nobrowser", "-data=/config"]

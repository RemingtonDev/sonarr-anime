FROM mcr.microsoft.com/dotnet/runtime:6.0-jammy

RUN apt-get update && apt-get install -y libsqlite3-0 libicu70 && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/sonarr

COPY Sonarr/ .

VOLUME /config /tv
EXPOSE 8989

ENTRYPOINT ["./Sonarr", "-nobrowser", "-data=/config"]

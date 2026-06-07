FROM node:22-bullseye-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:8.0

COPY --from=node /usr/local/ /usr/local/

WORKDIR /src/nihomebackend

RUN groupadd --system --gid 1001 nihome \
    && useradd --system --uid 1001 --gid nihome --create-home --shell /bin/bash nihome \
    && chown -R nihome:nihome /src
USER nihome

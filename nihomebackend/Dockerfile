FROM node:22-bullseye-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:8.0

COPY --from=node /usr/local/ /usr/local/

WORKDIR /src/nihomebackend

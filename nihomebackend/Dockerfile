FROM node:22-bullseye-slim AS web-build

WORKDIR /src/nihomeweb
COPY nihomeweb/package.json nihomeweb/package-lock.json ./
RUN npm ci
COPY nihomeweb/ ./
COPY nihomebackend/appsettings*.json /src/nihomebackend/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build

ARG BUILD_SHA=local
COPY --from=web-build /usr/local/ /usr/local/
WORKDIR /src
COPY . .
RUN dotnet restore nihomebackend/nihomebackend.csproj
RUN dotnet publish nihomebackend/nihomebackend.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:SkipNihomeWebBuild=true \
    -p:InformationalVersion=${BUILD_SHA}

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-noto-core fonts-noto-cjk \
    && test -r /usr/share/fonts/truetype/noto/NotoSans-Regular.ttf \
    && test -r /usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=web-build /src/nihomeweb/dist ./wwwroot

EXPOSE 5043
ENTRYPOINT ["dotnet", "nihomebackend.dll"]

FROM node:22-alpine AS frontend-build

WORKDIR /src/nihomeweb

COPY nihomeweb/package.json nihomeweb/package-lock.json ./
RUN npm ci

COPY nihomeweb/. ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build

WORKDIR /src

COPY nihomebackend/nihomebackend.csproj nihomebackend/
RUN dotnet restore nihomebackend/nihomebackend.csproj

COPY nihomebackend/. ./nihomebackend/
COPY --from=frontend-build /src/nihomeweb/out/ ./nihomebackend/wwwroot/

RUN dotnet publish nihomebackend/nihomebackend.csproj -c Release -o /app/publish /p:SkipNihomeWebBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=backend-build /app/publish ./

EXPOSE 5043

ENV ASPNETCORE_URLS=http://0.0.0.0:5043

ENTRYPOINT ["dotnet", "nihomebackend.dll"]

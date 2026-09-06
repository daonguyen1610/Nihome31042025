FROM node:22-bullseye-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:8.0

RUN apt-get update \
	&& apt-get install -y --no-install-recommends fontconfig fonts-noto-core fonts-noto-cjk \
	&& test -r /usr/share/fonts/truetype/noto/NotoSans-Regular.ttf \
	&& test -r /usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc \
	&& rm -rf /var/lib/apt/lists/*

COPY --from=node /usr/local/ /usr/local/

WORKDIR /src/nihomebackend

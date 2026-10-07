# =============================================================================
# MedLink LIS Analyzer Connector — контейнер (runtime 8.0, framework-dependent)
# Збірка:  docker build -f installers/labconnector/Dockerfile -t medlink/labconnector:4.0 .
# Запуск:  docker run -d --name labconnector --restart unless-stopped \
#            -p 5088:5088 -p 5100:5100 \
#            --device /dev/ttyUSB0:/dev/ttyUSB0 \
#            -v labconnector-data:/data \
#            -e Server__BaseUrl=https://lis.clinic.ua -e Server__ApiKey=… -e Server__ConnectorId=… \
#            medlink/labconnector:4.0
# Або реєстрація всередині контейнера:
#   docker run --rm -v labconnector-data:/data medlink/labconnector:4.0 setup --server https://lis.clinic.ua --install-key КЛЮЧ
# Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/MedLink.LIS.Core/MedLink.LIS.Core.csproj src/MedLink.LIS.Core/
COPY src/MedLink.LabConnector/MedLink.LabConnector.csproj src/MedLink.LabConnector/
RUN dotnet restore src/MedLink.LabConnector/MedLink.LabConnector.csproj
COPY src/MedLink.LIS.Core/ src/MedLink.LIS.Core/
COPY src/MedLink.LabConnector/ src/MedLink.LabConnector/
RUN dotnet publish src/MedLink.LabConnector/MedLink.LabConnector.csproj -c Release -o /app --no-restore

# ASP.NET Core runtime потрібен для Kestrel-сторінки статусу (FrameworkReference Microsoft.AspNetCore.App)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
LABEL org.opencontainers.image.title="MedLink LIS Analyzer Connector" \
      org.opencontainers.image.vendor="MedLink LLC" \
      org.opencontainers.image.version="4.0.0"
# cups-client — друк етикеток через lp -o raw (необов'язково)
RUN apt-get update && apt-get install -y --no-install-recommends cups-client curl tzdata && rm -rf /var/lib/apt/lists/* \
    && groupadd -r medlink && useradd -r -g medlink -G dialout medlink \
    && mkdir -p /data/logs && chown -R medlink:medlink /data
WORKDIR /app
COPY --from=build /app ./
ENV MEDLINK_CONNECTOR_DATA=/data \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    TZ=Europe/Kyiv \
    Local__StatusPort=5088
VOLUME ["/data"]
EXPOSE 5088
USER medlink
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD curl -fsS http://localhost:5088/healthz || exit 1
ENTRYPOINT ["dotnet", "MedLink.LabConnector.dll"]
CMD ["run"]

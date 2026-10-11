# Medlink-LAB — API + SPA в одному образі
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/MedLink.LIS.Api/MedLink.LIS.Api.csproj -c Release -o /app
FROM node:22 AS web
WORKDIR /web
COPY src/MedLink.LIS.Web .
RUN npm ci --no-audit --no-fund && npm run build
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
COPY --from=web /web/dist/spa ./wwwroot
ENV ASPNETCORE_URLS=http://0.0.0.0:5055 Database__SqlitePath=/data/medlink_lis.db
VOLUME ["/data"]
EXPOSE 5055
ENTRYPOINT ["dotnet", "MedLink.LIS.Api.dll"]

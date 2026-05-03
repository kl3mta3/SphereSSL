FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 7171

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SphereSSLv2/SphereSSLv2.csproj", "SphereSSLv2/"]
RUN dotnet restore "SphereSSLv2/SphereSSLv2.csproj"
COPY . .
WORKDIR "/src/SphereSSLv2"
RUN dotnet build "SphereSSLv2.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SphereSSLv2.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY SphereSSLv2/app.config /app/default.app.config
COPY entrypoint.sh /entrypoint.sh

# Create directories for persistent data
RUN mkdir -p /app/data /app/certs /app/logs \
    && chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]

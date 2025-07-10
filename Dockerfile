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

# Create directories for persistent data
RUN mkdir -p /app/data /app/certs /app/logs

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "SphereSSLv2.dll"]
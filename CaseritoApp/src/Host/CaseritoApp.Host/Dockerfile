# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore CaseritoApp.sln
RUN dotnet publish src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# Runtime (no-root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER root
RUN mkdir -p /data/fotos-avisos && chown -R app:app /data/fotos-avisos
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "CaseritoApp.Host.dll"]

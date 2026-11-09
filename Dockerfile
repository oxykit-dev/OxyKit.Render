# syntax=docker/dockerfile:1
# Image: ghcr.io/oxykit-dev/oxykit-render (published by .github/workflows/docker.yml).
# Framework-dependent portable publish, so the build stage runs natively for every target platform.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Build.targets ./
COPY src/OxyKit.Render.Contracts/ src/OxyKit.Render.Contracts/
COPY src/OxyKit.Render.Core/ src/OxyKit.Render.Core/
COPY src/OxyKit.Render.Api/ src/OxyKit.Render.Api/
RUN dotnet publish src/OxyKit.Render.Api/OxyKit.Render.Api.csproj -c Release -o /app/publish \
    && rm -f /app/publish/appsettings.Development.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY docker/entrypoint.sh /app/entrypoint.sh
ENV ASPNETCORE_URLS=http://+:5066
ENV Rendering__TemplatesRoot=/data/templates
ENV OXYKIT_TEMPLATES_SEED=/app/templates
EXPOSE 5066
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
    CMD curl -f http://localhost:5066/health || exit 1
ENTRYPOINT ["/app/entrypoint.sh"]

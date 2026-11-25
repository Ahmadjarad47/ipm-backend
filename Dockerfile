# IPM API image. Build context = repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached until a project file or package version changes).
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Ipm.Domain/Ipm.Domain.csproj src/Ipm.Domain/
COPY src/Ipm.Application/Ipm.Application.csproj src/Ipm.Application/
COPY src/Ipm.Infrastructure/Ipm.Infrastructure.csproj src/Ipm.Infrastructure/
COPY src/Ipm.RealTime/Ipm.RealTime.csproj src/Ipm.RealTime/
COPY src/Ipm.Api/Ipm.Api.csproj src/Ipm.Api/
RUN dotnet restore src/Ipm.Api/Ipm.Api.csproj

COPY src/ src/
RUN dotnet publish src/Ipm.Api/Ipm.Api.csproj -c Release --no-restore -o /app

# Full (non-alpine) runtime image: it ships ICU, needed for Arabic culture (InvariantGlobalization=false).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ipm.Api.dll"]

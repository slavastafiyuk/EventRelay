FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .


RUN dotnet tool restore

RUN dotnet restore \
    services/ControlPlaneService/src/EventRelay.ControlPlaneService.Api/EventRelay.ControlPlaneService.Api.csproj

RUN mkdir -p /out

# Generate a framework-dependent migration bundle.
# A dummy connection string is provided solely
# because the startup project's configuration requires one.
RUN ConnectionStrings__ProjectDatabase="Host=localhost;Database=unused;Username=dummy;Password=dummy" \
    ASPNETCORE_ENVIRONMENT=Production \
    dotnet ef migrations bundle \
    --project services/ControlPlaneService/src/EventRelay.ControlPlaneService.Infrastructure \
    --startup-project services/ControlPlaneService/src/EventRelay.ControlPlaneService.Api \
    --context ProjectDbContext \
    --configuration Release \
    --output /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build --chmod=755 /out/efbundle ./efbundle

COPY --from=build \
    /src/services/ControlPlaneService/src/EventRelay.ControlPlaneService.Api/appsettings.json \
    ./appsettings.json

USER $APP_UID

ENTRYPOINT ["./efbundle"]

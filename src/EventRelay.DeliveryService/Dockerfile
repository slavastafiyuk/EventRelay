# STAGE 1: Compile and publish the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet publish \
    src/EventRelay.DeliveryService/EventRelay.DeliveryService.csproj \
    -c Release \
    -o /out \
    /p:UseAppHost=false

# STAGE 2: Execute
FROM mcr.microsoft.com/dotnet/runtime:10.0

WORKDIR /app

COPY --from=build /out .

USER $APP_UID

ENTRYPOINT ["dotnet", "EventRelay.DeliveryService.dll"]
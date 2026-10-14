FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet publish \
    services/DeliveryService/src/EventRelay.DeliveryService/EventRelay.DeliveryService.csproj \
    -c Release \
    -o /out \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0

WORKDIR /app

COPY --from=build /out .

USER $APP_UID

ENTRYPOINT ["dotnet", "EventRelay.DeliveryService.dll"]
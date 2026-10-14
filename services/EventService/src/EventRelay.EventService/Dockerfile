FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet publish \
    services/EventService/src/EventRelay.EventService/EventRelay.EventService.csproj \
    -c Release \
    -o /out \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /out .

USER $APP_UID

ENTRYPOINT ["dotnet", "EventRelay.EventService.dll"]
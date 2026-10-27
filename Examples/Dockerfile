# Built from the repo's root, as the examples reference Obvs's projects: docker compose up, from Examples/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Examples/Service.ActiveMQ -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/. .
ENTRYPOINT ["dotnet", "Obvs.Example.Service.ActiveMQ.dll"]

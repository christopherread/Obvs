# The service, on the published Obvs packages, built from Examples/: docker compose up, from Examples/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore from the project files first, so changing the code doesn't download the packages again
COPY Directory.Packages.props .
COPY Messages/*.csproj Messages/
COPY Service.ActiveMQ/*.csproj Service.ActiveMQ/
RUN dotnet restore Service.ActiveMQ
COPY Messages/ Messages/
COPY Service.ActiveMQ/ Service.ActiveMQ/
RUN dotnet publish Service.ActiveMQ -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/. .
ENTRYPOINT ["dotnet", "Obvs.Example.Service.ActiveMQ.dll"]

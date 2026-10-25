FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY Service.ActiveMQ/*.csproj ./Service.ActiveMQ/
COPY Messages/*.csproj ./Messages/
WORKDIR /app/Service.ActiveMQ/
RUN dotnet restore

WORKDIR /app/
COPY Service.ActiveMQ/. ./Service.ActiveMQ/
COPY Messages/. ./Messages/
WORKDIR /app/Service.ActiveMQ/
RUN dotnet publish -f net10.0 -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:10.0 as runtime
WORKDIR /app
COPY --from=build /app/Service.ActiveMQ/out/. .
ENTRYPOINT ["dotnet", "Obvs.Example.Service.ActiveMQ.dll"]

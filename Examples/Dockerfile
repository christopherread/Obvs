FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

COPY Service.ActiveMQ/*.csproj ./Service.ActiveMQ/
COPY Messages/*.csproj ./Messages/
WORKDIR /app/Service.ActiveMQ/
RUN dotnet restore

WORKDIR /app/
COPY Service.ActiveMQ/. ./Service.ActiveMQ/
COPY Messages/. ./Messages/
WORKDIR /app/Service.ActiveMQ/
RUN dotnet publish -f netstandard2.1 -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1 as runtime
WORKDIR /app
COPY --from=build /app/Service.ActiveMQ/out/. .
ENTRYPOINT ["dotnet", "Obvs.Example.Service.ActiveMQ.dll"]

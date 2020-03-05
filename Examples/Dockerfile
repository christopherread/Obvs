FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build
WORKDIR /app

COPY Obvs.ActiveMQ.Example.Service/*.csproj ./Obvs.ActiveMQ.Example.Service/
COPY Obvs.ActiveMQ.Example.Messages/*.csproj ./Obvs.ActiveMQ.Example.Messages/
COPY Obvs.ActiveMQ/*.csproj Obvs.ActiveMQ/
WORKDIR /app/Obvs.ActiveMQ.Example.Service/
RUN dotnet restore

WORKDIR /app/
COPY Obvs.ActiveMQ.Example.Service/. ./Obvs.ActiveMQ.Example.Service/
COPY Obvs.ActiveMQ.Example.Messages/. ./Obvs.ActiveMQ.Example.Messages/
COPY Obvs.ActiveMQ/. ./Obvs.ActiveMQ/
WORKDIR /app/Obvs.ActiveMQ.Example.Service/
RUN dotnet publish -f netstandard2.1 -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.0 as runtime
WORKDIR /app
COPY --from=build /app/Obvs.ActiveMQ.Example.Service/out ./
ENTRYPOINT ["dotnet", "Obvs.ActiveMQ.Example.Service.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
# Install dependencies as root before switching users
USER root
RUN apk add --no-cache krb5-libs
USER $APP_UID
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SimpleAIAPI/SimpleAIAPI.csproj", "SimpleAIAPI/"]
RUN dotnet restore "SimpleAIAPI/SimpleAIAPI.csproj"
COPY . .
WORKDIR "/src/SimpleAIAPI"
RUN dotnet build "./SimpleAIAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SimpleAIAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SimpleAIAPI.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Bootstrapper/Api/Api.csproj", "Bootstrapper/Api/"]
COPY ["Modules/Notification/Notification/Notification.csproj", "Modules/Notification/Notification/"]
COPY ["Shared/Shared.Contracts/Shared.Contracts.csproj", "Shared/Shared.Contracts/"]
COPY ["Shared/Shared/Shared.csproj", "Shared/Shared/"]
COPY ["Shared/Shared.Messaging/Shared.Messaging.csproj", "Shared/Shared.Messaging/"]
COPY ["Modules/Request/Request/Request.csproj", "Modules/Request/Request/"]
COPY ["Modules/Request/Request.Contracts/Request.Contracts.csproj", "Modules/Request/Request.Contracts/"]
COPY ["Modules/Document/Document.Contracts/Document.Contracts.csproj", "Modules/Document/Document.Contracts/"]
COPY ["Modules/Auth/Auth/Auth.csproj", "Modules/Auth/Auth/"]
COPY ["Modules/Auth/OAuth2OpenId/OAuth2OpenId.csproj", "Modules/Auth/OAuth2OpenId/"]
COPY ["Modules/Assignment/Assignment/Assignment.csproj", "Modules/Assignment/Assignment/"]
COPY ["Modules/Document/Document/Document.csproj", "Modules/Document/Document/"]
RUN dotnet restore "Bootstrapper/Api/Api.csproj"
COPY . .
WORKDIR "/src/Bootstrapper/Api"
RUN dotnet build "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["KidMonitor.Api/KidMonitor.Api.csproj", "KidMonitor.Api/"]
RUN dotnet restore "KidMonitor.Api/KidMonitor.Api.csproj"
# Cache bust: force rebuild on code changes
COPY KidMonitor.Api/ KidMonitor.Api/
WORKDIR "/src/KidMonitor.Api"
RUN dotnet build "KidMonitor.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KidMonitor.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KidMonitor.Api.dll"]

# Estágio 1: Build da aplicação .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["project_API.csproj", "./"]
RUN dotnet restore "project_API.csproj"

COPY . .
RUN dotnet publish "project_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Runtime leve ASP.NET Core 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 5076
ENV ASPNETCORE_URLS=http://+:5076
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "project_API.dll"]

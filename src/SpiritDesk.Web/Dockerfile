FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/SpiritDesk.Web/SpiritDesk.Web.csproj", "src/SpiritDesk.Web/"]
COPY ["src/SpiritDesk.Core/SpiritDesk.Core.csproj", "src/SpiritDesk.Core/"]
RUN dotnet restore "src/SpiritDesk.Web/SpiritDesk.Web.csproj"

COPY . .
RUN dotnet publish "src/SpiritDesk.Web/SpiritDesk.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /app/data/keys

ENTRYPOINT ["dotnet", "SpiritDesk.Web.dll"]

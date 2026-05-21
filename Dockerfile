FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

COPY ["LAVAL.Web/LAVAL.Web.csproj", "LAVAL.Web/"]
RUN dotnet restore "LAVAL.Web/LAVAL.Web.csproj"

COPY . .
WORKDIR "/src/LAVAL.Web"
RUN dotnet publish "LAVAL.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
CMD ["sh", "-c", "dotnet LAVAL.Web.dll --urls http://0.0.0.0:${PORT:-10000}"]

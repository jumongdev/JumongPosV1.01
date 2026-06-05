FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY JumongCloudAPI/JumongCloudAPI.csproj .
RUN dotnet restore
COPY JumongCloudAPI/ .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-5000}
ENTRYPOINT ["dotnet", "JumongCloudAPI.dll"]

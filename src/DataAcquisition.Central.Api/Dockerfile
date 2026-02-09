# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY src/DataAcquisition.Contracts/DataAcquisition.Contracts.csproj src/DataAcquisition.Contracts/
COPY src/DataAcquisition.Central.Api/DataAcquisition.Central.Api.csproj src/DataAcquisition.Central.Api/
RUN dotnet restore src/DataAcquisition.Central.Api/DataAcquisition.Central.Api.csproj

COPY src/DataAcquisition.Contracts/ src/DataAcquisition.Contracts/
COPY src/DataAcquisition.Central.Api/   src/DataAcquisition.Central.Api/
RUN dotnet publish src/DataAcquisition.Central.Api/DataAcquisition.Central.Api.csproj \
    -c Release -o /app/publish

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "DataAcquisition.Central.Api.dll"]

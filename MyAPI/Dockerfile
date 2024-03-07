#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

ENV APIKEY_TOMORROWIO="A74xSEtjzyFV3noo2vaI7JG7CZ7UinXx"
ENV APIKEY_STORMGLASS="50bf5ba6-dc03-11ee-91a9-0242ac130002-50bf5ce6-dc03-11ee-91a9-0242ac130002"

USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MyAPI/MyAPI.csproj", "MyAPI/"]
RUN dotnet restore "./MyAPI/./MyAPI.csproj"
COPY . .
WORKDIR "/src/MyAPI"
RUN dotnet build "./MyAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MyAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyAPI.dll"]
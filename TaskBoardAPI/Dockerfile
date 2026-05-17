#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Development
WORKDIR /src
COPY ["TaskBoardAPI/TaskBoardAPI.csproj", "TaskBoardAPI/"]
RUN dotnet restore "./TaskBoardAPI/TaskBoardAPI.csproj"
COPY . .
WORKDIR "/src/TaskBoardAPI"
RUN dotnet build "./TaskBoardAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Development
RUN dotnet publish "./TaskBoardAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TaskBoardAPI.dll"]
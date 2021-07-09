FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Kepler.GithubActions.TagBump/Kepler.GithubActions.TagBump.csproj", "Kepler.GithubActions.TagBump/"]
RUN dotnet restore "Kepler.GithubActions.TagBump/Kepler.GithubActions.TagBump.csproj"
COPY . .
WORKDIR "/src/Kepler.GithubActions.TagBump"
RUN dotnet build "Kepler.GithubActions.TagBump.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kepler.GithubActions.TagBump.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kepler.GithubActions.TagBump.dll"]

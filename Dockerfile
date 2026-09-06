FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
ARG PROJECT
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked dotnet restore ${PROJECT} --configfile NuGet.Config
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked dotnet publish ${PROJECT} -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ARG DLL
ENV APP_DLL=${DLL}
ENTRYPOINT ["sh", "-c", "exec dotnet ${APP_DLL}"]

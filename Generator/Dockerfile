FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Generator/Generator.fsproj", "Generator/"]
RUN dotnet restore "Generator/Generator.fsproj"
COPY . .
WORKDIR "/src/Generator"
RUN dotnet build "Generator.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Generator.fsproj" -c Release -o /app/publish \
    --runtime alpine-x64 \
    --self-contained true \
    /p:PublishSingleFile=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["Generator"]

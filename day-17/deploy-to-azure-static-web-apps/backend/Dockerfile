# syntax=docker/dockerfile:1
#
# The csproj also carries ContainerBaseImage/ContainerRepository, so
# `dotnet publish /t:PublishContainer` still works without this file. The Dockerfile
# exists so the image can be built by anything that speaks Docker — including
# `az containerapp up --source .`, which is what scripts/deploy.sh uses.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project file alone first, so `restore` is cached against dependency changes
# rather than against every source edit.
COPY QuotesApi.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Container Apps routes to this port; it must match --target-port on the ingress
# and the `port: 8080` already declared in azure.yaml.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# SQLite writes to the path in ConnectionStrings:Quotes (default /tmp/quotes.db) and a
# container filesystem does not survive a revision restart. Accepted here: Day 17 is
# about the deployment and the identity wiring, not durability.
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuotesApi.dll"]

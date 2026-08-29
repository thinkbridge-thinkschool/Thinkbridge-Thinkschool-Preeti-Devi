# Day 5 — Piece 2: Container Image from dotnet publish (No Dockerfile)

## What We Did
.NET 10 ships built-in container image generation — no Dockerfile, no FROM, no multi-stage builds needed.

---

## 1. csproj Container Properties

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>

  <!-- Container publish properties (no Dockerfile needed) -->
  <ContainerRepository>quotes-api</ContainerRepository>
  <ContainerImageTag>0.1.0</ContainerImageTag>
  <ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0</ContainerBaseImage>
</PropertyGroup>
```

---

## 2. Build Command

```bash
dotnet publish --os linux --arch x64 /t:PublishContainer -c Release
```

### Build Output
```
QuotesApi -> bin/Release/net10.0/linux-x64/QuotesApi.dll
QuotesApi -> bin/Release/net10.0/linux-x64/publish/
Building image 'quotes-api' with tags '0.1.0' on top of base image 'mcr.microsoft.com/dotnet/aspnet:10.0'.
Pushed image 'quotes-api:0.1.0' to local registry via 'docker'.
```

---

## 3. Docker Image Verification

```
IMAGE              ID             DISK USAGE   CONTENT SIZE
quotes-api:0.1.0   283457bd0a06        361MB          102MB
```

---

## 4. docker run Output

```bash
docker run -d --name quotes-api-test -p 8080:8080 \
  -e ASPNETCORE_URLS="http://+:8080" \
  -e Jwt__Key="super-secret-key-at-least-32-characters-long" \
  -e Jwt__Issuer="QuotesApi" \
  -e Jwt__Audience="QuotesApiUsers" \
  -e ConnectionStrings__Quotes="Data Source=/tmp/quotes.db" \
  quotes-api:0.1.0

8745f26ef748b0ce62638a16f18ac4a6fd0cb02d95b7541ccadfb779e30853a4
```

Container Status:
```
CONTAINER ID   IMAGE              STATUS          PORTS
8745f26ef748   quotes-api:0.1.0   Up 35 seconds   0.0.0.0:8080->8080/tcp
```

---

## 5. Health Endpoint curl

```bash
$ curl http://localhost:8080/health

Healthy
HTTP Code: 200
```

? Healthy — HTTP 200! The real app is running inside the container.

---

## 6. Health Endpoint Code

```csharp
builder.Services.AddHealthChecks();
app.MapHealthChecks("/health");
```

---

## Key Takeaway
.NET 10s built-in container publish eliminates the need to write or maintain a Dockerfile for standard ASP.NET Core apps.
Just set ContainerRepository, ContainerImageTag, and ContainerBaseImage in your .csproj — the SDK handles the rest.

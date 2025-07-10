# SphereSSL - Docker Setup

SphereSSL is an SSL certificate management platform built with C# and ASP.NET Core. The containerized version has been adapted from the original Windows executable to run seamlessly in Docker environments across multiple platforms.

## Quick Start

1. **Build and run with Docker Compose:**
   ```bash
   docker-compose up -d --build
   ```

2. **Access the application:**
   - Navigate to: http://localhost:7171
   - Default credentials: `admin` / `changeme123`
>  **Important:** Change the default password immediately after first login

## Manual Docker Build

```bash
# Build the image
docker build -t spheressl .

# Run the container with persistent storage
docker run -d \
  -p 7171:7171 \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/certs:/app/certs \
  -v $(pwd)/logs:/app/logs \
  --name spheressl \
  --restart unless-stopped \
  spheressl
```

## Technical Architecture

### Containerization

**Multi-Stage Docker Build**
- Utilizes .NET 8.0 base images with optimized build process
- Implements proper layer caching and minimal runtime footprint
- Follows Docker best practices for ASP.NET Core applications

**Security Implementation**
- Runs as non-root user (`appuser`) with appropriate permissions
- Uses minimal runtime base image (aspnet:8.0)
- Implements proper volume ownership and access controls

### Platform Independence

**Network Configuration**
- Server binding optimized for container networking (0.0.0.0)
- IP restriction middleware disabled for Docker compatibility
- CORS properly configured for multi-origin access

**Dependency Management**
- All Windows-specific dependencies removed
- Cross-platform .NET libraries exclusively used
- SQLite database ensures portability across operating systems

## Persistent Storage

The following directories are mounted as Docker volumes to ensure data persistence:

| Volume Mount | Purpose | Description |
|--------------|---------|-------------|
| `/app/data` | Database & Config | SQLite database and application configuration |
| `/app/certs` | SSL Certificates | Generated certificates and private keys |
| `/app/logs` | Application Logs | Runtime logs and debugging information |

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment setting |
| `ServerIP` | `0.0.0.0` | Server binding IP address |
| `ServerPort` | `7171` | HTTP port for web interface |

### Security and Other Considerations

- Please for all thing holy, change the default username and password (This is configured in [`app.config`](./SphereSSLv2/app.config))
- I've setup a cron job on my virtual host that does a regular backup of the `/app/data` volume
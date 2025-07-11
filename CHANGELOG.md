# CHANGELOG

v1.1 - Add Support for Docker Deployments

The following modifications were made to enable containerization and have been validated against the codebase:

### Platform Independence
- **Windows API Removal**: Removed `[STAThread]` attribute and Windows tray application dependencies
- **Build Target**: Changed from Windows executable (`WinExe`) to cross-platform library (`Exe`)
- **Cross-Platform Libraries**: All dependencies verified as Linux-compatible

### Network Binding
- **Interface Binding**: Changed from localhost-only (127.0.0.1) to all interfaces (0.0.0.0)
- **Kestrel Configuration**: Updated to `IPAddress.Any` for Docker compatibility
- **Container Networking**: Optimized for Docker bridge and overlay networks

### Access Control
- **IP Restrictions**: Disabled localhost-only middleware for container networking
- **CORS Policy**: Configured for multi-origin container environments
- **Security Headers**: Maintained while enabling container accessibility

### File System
- **Database Path**: Updated to containerized path (`/app/data/certificates.db`)
- **Volume Structure**: Aligned with Docker volume mount strategy
- **Permissions**: Configured for non-root user execution
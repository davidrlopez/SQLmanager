# OracleManager

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet) ![Avalonia](https://img.shields.io/badge/Avalonia-11.3-8B5CF6) ![Oracle](https://img.shields.io/badge/Oracle-DB-F80000?logo=oracle)

Cross-platform desktop application for operational and commercial management, built with .NET 9 and Avalonia. Connects to Oracle Database with native OS credential storage on both macOS and Windows.

## Screenshots

<p align="center">
  <img src="Screenshots/SQLmanager4.png" width="600" alt="Login screen">
  <br>
  <sub>Login. Credentials are stored in the macOS Keychain.</sub>
</p>

<p align="center">
  <img src="Screenshots/SQLmanager2.png" width="600" alt="Insert client form">
  <br>
  <sub>Client insertion form with live data.</sub>
</p>

<p align="center">
  <img src="Screenshots/SQLmanager3.png" width="600" alt="Main query view">
  <br>
  <sub>Main view with dynamic search, inline table results and Excel export.</sub>
</p>

## What this project does

OracleManager is an internal tool used to manage clients, orders, payments and employees against an Oracle database. It runs natively on macOS and Windows from a single C# codebase, stores credentials securely in the OS keychain (macOS) or Credential Manager (Windows), and provides a dynamic query interface with inline editing, filtering and Excel export.

## Stack

| Component          | Technology                                      |
| ------------------ | ----------------------------------------------- |
| Language           | C# / .NET 9.0                                   |
| UI                 | Avalonia 11.3.3                                 |
| Database           | Oracle (`Oracle.ManagedDataAccess.Core` 23.9.1) |
| Credential storage | macOS Keychain / Windows Credential Manager     |
| DI                 | `Microsoft.Extensions.DependencyInjection`      |
| Architecture       | MVVM + service layer + DAO                      |

## Technical decisions

### Native OS credential storage

Credentials are never stored in plain text or in the application config. On macOS they go to the system Keychain, on Windows to the Credential Manager. The same `Manager.cs` handles both platforms via compile-time directives.

### Parameterized SQL throughout

All user-supplied values go through Oracle parameters, not string interpolation. Dynamic identifiers (table/column names) are validated against an explicit whitelist before being included in queries.

### Audit logging

All connection attempts, successful or not, are logged through `IAuditLogger`, which never writes credentials or full connection strings to disk.

### Configuration through environment variables

```bash
export DB_USER="your_user"
export DB_HOST="host:1521/FREE"
export DB_PASSWORD="your_password"
```

## Running locally

Requires .NET 9 SDK and access to an Oracle instance.

```bash
dotnet restore
dotnet build
dotnet run
```

> **macOS users:** Oracle Database has no native ARM image, so running it locally on Apple Silicon requires Colima. See the [Oracle DB on macOS setup guide](https://github.com/davidrlopez/OracleDBmacOS) for the full setup.

## Security hardening

After development the repo went through a security audit, which led to these changes:

- Removed all hardcoded credentials from source and test files
- Removed full connection string from debug logs
- Removed insecure host/user defaults from DI registration
- Migrated INSERT operations from `ExecuteQuery` to `ExecuteNonQuery`
- Added identifier whitelist validation for dynamic SQL (table/column names)
- Cleaned build artifacts, IDE files and runtime logs from repo history

## Known limitations and roadmap

The main view controller is the largest file and concentrates too much logic. It works but is the main source of technical debt. Planned refactor: extract query, pagination, inline edit, export and delete flows into dedicated services.

No automated tests or CI pipeline exist in this state. Next steps would be unit tests for the service layer and a GitHub Actions pipeline for build + static analysis.

## Context

Built in 3 weeks during a professional internship, self-taught and before any formal software engineering studies, in a company with no software engineering background. The goal was to solve real operational needs while learning applied software engineering from scratch.

The company had no dedicated server, so an obsolete 2011 iMac was upgraded to macOS Sonoma with OpenCore Legacy Patcher to support modern containerization. It became the company's on-premise server, running the Oracle database in Docker, and was administered remotely over SSH and `sqlcl`.

The first version had hardcoded credentials and insecure logging, which is common in environments where security is not a primary concern. The audit and hardening came afterwards, as a learning exercise and to make the code suitable for a public portfolio.

## Contributors

**Jesús Francisco Espinosa Sánchez** ([@hir0krim](https://github.com/hir0krim))

- Implemented deep link / meta-link navigation support within the Avalonia application
- Co-designed the entity-relationship model for the database schema, including field definition and mapping to the UI layer
- Conducted exploratory and stress testing to identify functional defects and improve application robustness

**Moira Perez Rodriguez** ([@Moipr99](https://github.com/Moipr99))

- Engineered data preprocessing pipelines to clean and migrate legacy data into the Oracle database, ensuring schema integrity and optimized query performance
- Implemented compatibility with Windows Hello and Windows Credential Manager, enabling secure application access via OS-level PIN and system credentials
- Co-developed the authentication bridge for the desktop application, specifically handling the integration with Windows Security APIs to complement the macOS Keychain implementation

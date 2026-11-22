# Reaparr .NET 11 dependency inventory

**Purpose:** synchronize the implementation plans with a complete current direct-package version inventory and bounded changelog/best-practices review of the .NET 10 package upgrades and .NET 11 migration. Snapshot date: **2026-11-22**. The repository has 41 PackageReference project files and 41 lockfiles, containing 75 direct package identities and 232 distinct package IDs in the combined lock graphs. All direct IDs were checked for current version status; package-family changelog review covers the migration packages and resolved dependencies materially affecting Reaparr's runtime/test contracts. This is not a line-by-line changelog audit of all 232 resolved packages.

## Current stable-version audit (2026-11-22)

Current direct-package versions are checked against the official NuGet.org V3 flat-container version indexes on 2026-11-22; latest stable means the highest version without prerelease metadata. This table records package versions, not API, TFM, provider, native-RID, operational or application-compatibility proof. `PackageReference Version="x"` is a minimum; lockfiles record selected resolutions. Quartz remains intentionally behind latest stable per the user's direction. The legacy `docker/docker-compose.dcproj` is excluded because it is not a PackageReference project.

| Package ID | Current | Latest stable |
|---|---:|---:|
| `AppAny.Quartz.EntityFrameworkCore.Migrations.SQLite` | 0.6.2 | 0.6.2 |
| `Autofac` | 9.3.4 | 9.3.4 |
| `Autofac.Extensions.DependencyInjection` | 11.0.2 | 11.0.2 |
| `Autofac.Extras.Moq` | 8.0.1 | 8.0.1 |
| `BencodeNET` | 5.0.0 | 5.0.0 |
| `Bogus` | 35.6.5 | 35.6.5 |
| `Bogus.Hollywood` | 35.6.5 | 35.6.5 |
| `ByteSize` | 2.1.2 | 2.1.2 |
| `CSharpier.MsBuild` | 1.4.1 | 1.4.1 |
| `CliWrap` | 3.10.5 | 3.10.5 |
| `Downloader` | 7.1.0 | 7.1.0 |
| `FastEndpoints` | 9.0.0 | 9.0.0 |
| `FastEndpoints.OpenApi` | 9.0.0 | 9.0.0 |
| `FastEndpoints.Security` | 9.0.0 | 9.0.0 |
| `FastEndpoints.Testing` | 9.0.0 | 9.0.0 |
| `EFCore.BulkExtensions.SQLite` | 10.0.1 | 10.0.1 |
| `HttpClientToCurl` | 2.2.0 | 2.2.0 |
| `FlexQuery.NET.EntityFrameworkCore` | 4.0.0 | 4.0.0 |
| `FluentValidation` | 12.2.0 | 12.2.0 |
| `Flurl` | 4.0.0 | 4.0.0 |
| `JetBrains.Annotations` | 2026.2.0 | 2026.2.0 |
| `LukeHagar.PlexAPI.SDK` | 0.22.3 | 0.22.3 |
| `MessagePack` | 3.1.12 | 3.1.12 |
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | 11.0.0 | 11.0.0 |
| `Microsoft.AspNetCore.Http.Abstractions` | 2.3.14 | 2.3.14 |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 11.0.0 | 11.0.0 |
| `Microsoft.AspNetCore.Mvc.Testing` | 11.0.0 | 11.0.0 |
| `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` | 11.0.0 | 11.0.0 |
| `Microsoft.AspNetCore.SpaServices.Extensions` | 11.0.0 | 11.0.0 |
| `Microsoft.Data.Sqlite` | 11.0.0 | 11.0.0 |
| `Microsoft.EntityFrameworkCore` | 11.0.0 | 11.0.0 |
| `Microsoft.EntityFrameworkCore.Design` | 11.0.0 | 11.0.0 |
| `Microsoft.EntityFrameworkCore.InMemory` | 11.0.0 | 11.0.0 |
| `Microsoft.EntityFrameworkCore.Sqlite` | 11.0.0 | 11.0.0 |
| `Microsoft.EntityFrameworkCore.Tools` | 11.0.0 | 11.0.0 |
| `Microsoft.Extensions.DependencyInjection` | 11.0.0 | 11.0.0 |
| `Microsoft.Extensions.Http.Polly` | 11.0.0 | 11.0.0 |
| `Moq` | 4.21.0 | 4.21.0 |
| `Moq.Contrib.HttpClient` | 1.4.0 | 1.4.0 |
| `NaturalSort.Extension` | 4.4.1 | 4.4.1 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.21.0 | 1.21.0 |
| `OpenTelemetry.Extensions.Hosting` | 1.21.0 | 1.21.0 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.21.0 | 1.21.0 |
| `OpenTelemetry.Instrumentation.Http` | 1.21.0 | 1.21.0 |
| `PasswordGenerator` | 3.0.0 | 3.0.0 |
| `Photino.NET` | 4.0.16 | 4.0.16 |
| `Polly` | 8.9.1 | 8.9.1 |
| `Polly.Core` | 8.9.1 | 8.9.1 |
| `PostHog` | 2.26.5 | 2.26.5 |
| `Quartz` | **4.7.3** | **4.8.1** |
| `Semver` | 3.0.0 | 3.0.0 |
| `Serilog` | 4.4.0 | 4.4.0 |
| `Serilog.AspNetCore` | 11.0.0 | 11.0.0 |
| `Serilog.Enrichers.Sensitive` | 2.1.0 | 2.1.0 |
| `Serilog.Expressions` | 5.0.0 | 5.0.0 |
| `Serilog.Sinks.AspNetCore.App.SignalR` | 1.3.0 | 1.3.0 |
| `Serilog.Sinks.Console` | 6.1.1 | 6.1.1 |
| `Serilog.Sinks.Console.LogThemes` | 0.4.0 | 0.4.0 |
| `Serilog.Sinks.Debug` | 3.0.0 | 3.0.0 |
| `Serilog.Sinks.File` | 7.0.0 | 7.0.0 |
| `Serilog.Sinks.OpenTelemetry` | 4.2.0 | 4.2.0 |
| `Serilog.Sinks.Seq` | 9.1.0 | 9.1.0 |
| `Serilog.Sinks.TestCorrelator` | 4.0.2 | 4.0.2 |
| `Shouldly` | 4.3.0 | 4.3.0 |
| `Spectre.Console.Cli` | 0.57.2 | 0.57.2 |
| `SQLite` | 3.54.1 | 3.54.1 |
| `Swashbuckle.AspNetCore.SwaggerUI` | 11.0.1 | 11.0.1 |
| `SQLitePCLRaw.bundle_e_sqlite3` | 3.0.5 | 3.0.5 |
| `System.IO.Abstractions` | 23.0.1 | 23.0.1 |
| `System.Reactive` | 7.0.0 | 7.0.0 |
| `TUnit` | 1.87.12 | 1.87.12 |
| `TestableIO.System.IO.Abstractions.TestingHelpers` | 23.0.1 | 23.0.1 |
| `Velopack` | 1.2.161 | 1.2.161 |
| `Xdg.Directories` | 0.2.0 | 0.2.0 |
| `Yarp.ReverseProxy` | 2.3.0 | 2.3.0 |


The version inventory covers all 75 direct IDs. The lockfile union contains 232 distinct package IDs across 41 project lockfiles; each project resolves its own graph, so that union is not one deployable graph. The changelog review covers direct upgrade families and materially relevant resolved transitive dependencies only, not every package's complete release history.
Quartz current package anchors: [`src/Application/Application.csproj`](../src/Application/Application.csproj#L7) and [`src/Application/packages.lock.json`](../src/Application/packages.lock.json#L60). The manifest and lock resolve Quartz 4.7.3; NuGet's official version index shows 4.8.1 as latest stable as of 2026-11-22. The user has directed that the Quartz package step remain gated until the stable EF Core 11 BulkExtensions release is available and its targeted persistence gate passes. See the [Quartz 4.8.1 upgrade gate](dotnet11-quartz-upgrade.md#481-compatibility-changes-to-prove-before-accepting-the-upgrade).


## Historical inventory method and baseline snapshot (2026-11-20)

The following command and resulting package table/project matrix describe the package-upgrades snapshot before implementation. They are retained as historical context only and do not reflect current project references or locks; use the [current stable-version audit](#current-stable-version-audit-2026-11-22) above for current direct package versions.

The original read-only command enumerated PackageReference projects with `dotnet package list --project Reaparr.sln --outdated --format json --no-restore` against `https://api.nuget.org/v3/index.json`. It reported 189 direct reference occurrences across 81 distinct IDs and 124 outdated project occurrences across 57 IDs; it also encountered an unsupported legacy `docker/docker-compose.dcproj` `package.config` file, so that project was outside the PackageReference inventory. These counts describe the original snapshot only.

The original inventory records baseline versions and NuGet latest-stable results from that snapshot. Package version availability did not prove TFM, API, native RID or runtime compatibility. The current audit above is separately dated 2026-11-22.

| Package ID | Current version(s) | Latest stable at baseline | Direct refs | Projects |
|---|---:|---:|---:|---|
| `AppAny.Quartz.EntityFrameworkCore.Migrations.SQLite` | 0.6.0 | **0.6.2** | 1 | src/Data/Data.csproj |
| `Autofac` | 9.1.0 | **9.3.4** | 11 | src/Data/Data.csproj; src/Environment/Environment.csproj; src/External/External.csproj; src/FileSystem/FileSystem.csproj; src/Logging/Logging.csproj; src/PlexApi/PlexApi.csproj; src/PublicAPI/PublicAPI.csproj; src/Settings/Settings.csproj; src/SignalR/SignalR.csproj; tests/BaseTests/BaseTests.csproj; tools/Build/Build.csproj |
| `Autofac.Extensions.DependencyInjection` | 11.0.0 | **11.0.2** | 2 | src/AppHost/AppHost.csproj; tools/Build/Build.csproj |
| `Autofac.Extras.Moq` | 7.0.0 | **8.0.1** | 1 | tests/BaseTests/BaseTests.csproj |
| `Autofac.Extras.Quartz` | 11.0.0 | **11.0.0** | 1 | src/Application/Application.csproj |
| `BencodeNET` | 5.0.0 | **5.0.0** | 1 | src/PublicAPI/PublicAPI.csproj |
| `Bogus` | 35.6.5 | **35.6.5** | 1 | tests/BaseTests/BaseTests.csproj |
| `Bogus.Hollywood` | 35.6.5 | **35.6.5** | 1 | tests/BaseTests/BaseTests.csproj |
| `ByteSize` | 2.1.2 | **2.1.2** | 1 | src/Domain/Domain.csproj |
| `CSharpier.MsBuild` | 1.4.0 | **1.4.0** | 40 | all production/test projects that use the MSBuild formatter target; exact references are in the corresponding `*.csproj` files |
| `CliWrap` | 3.10.1 | **3.10.5** | 2 | src/External/External.csproj; tools/Build/Build.csproj |
| `Downloader` | 5.5.0 | **7.1.0** | 1 | src/Application/Application.csproj |
| `EFCore.BulkExtensions.SQLite` | 10.0.1 | **10.0.1** | 1 | src/Data.Contracts/Data.Contracts.csproj |
| `FastEndpoints` | 8.3.0 | **9.0.0** | 5 | src/Application/Application.csproj; src/Domain/Domain.csproj; src/PlexApi/PlexApi.csproj; src/PublicAPI/PublicAPI.csproj; tools/Build/Build.csproj |
| `FastEndpoints.OpenApi` | 8.3.0 | **9.0.0** | 1 | src/AppHost/AppHost.csproj |
| `FastEndpoints.Security` | 8.3.0 | **9.0.0** | 3 | src/AppHost/AppHost.csproj; src/Application/Application.csproj; src/Identity/Identity.csproj |
| `FastEndpoints.Testing` | 8.3.0 | **9.0.0** | 1 | tests/BaseTests/BaseTests.csproj |
| `FlexQuery.NET.EFCore` | 2.5.0 | **2.5.0** | 1 | src/Data.Contracts/Data.Contracts.csproj |
| `FluentValidation` | 12.1.1 | **12.2.0** | 2 | src/FluentResultExtensions/FluentResultExtensions.csproj; tools/Build/Build.csproj |
| `Flurl` | 4.0.0 | **4.0.0** | 1 | src/Domain/Domain.csproj |
| `HttpClientToCurl` | 2.1.1 | **2.2.0** | 1 | src/PlexApi/PlexApi.csproj |
| `JetBrains.Annotations` | 2025.2.4 | **2026.2.0** | 1 | src/Domain/Domain.csproj |
| `LukeHagar.PlexAPI.SDK` | 0.22.3 | **0.22.3** | 1 | src/PlexApi.Contracts/PlexApi.Contracts.csproj |
| `MessagePack` | 3.1.9 | **3.1.12** | 1 | src/SignalR/SignalR.csproj |
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | 10.0.13 | **11.0.0** | 1 | src/Identity.Contracts/Identity.Contracts.csproj |
| `Microsoft.AspNetCore.Http.Abstractions` | 2.3.13 | **2.3.14** | 1 | src/Logging/Logging.csproj |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.13 | **11.0.0** | 1 | src/Identity.Contracts/Identity.Contracts.csproj |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.13 | **11.0.0** | 1 | tests/IntegrationTests/IntegrationTests/IntegrationTests.csproj |
| `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` | 10.0.13 | **11.0.0** | 1 | src/AppHost/AppHost.csproj |
| `Microsoft.AspNetCore.SpaServices.Extensions` | 10.0.13 | **11.0.0** | 1 | src/AppHost/AppHost.csproj |
| `Microsoft.Data.Sqlite` | 10.0.13 | **11.0.0** | 1 | src/Data/Data.csproj |
| `Microsoft.EntityFrameworkCore` | 10.0.13 | **11.0.0** | 1 | src/Data.Contracts/Data.Contracts.csproj |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.13 | **11.0.0** | 2 | src/Data/Data.csproj; src/Identity/Identity.csproj |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.13 | **11.0.0** | 2 | src/Data/Data.csproj; src/Identity/Identity.csproj |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.13 | **11.0.0** | 3 | src/Data.Contracts/Data.Contracts.csproj; src/Data/Data.csproj; src/Identity/Identity.csproj |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.13 | **11.0.0** | 2 | src/Data/Data.csproj; src/Identity/Identity.csproj |
| `Microsoft.Extensions.DependencyInjection` | 10.0.13 | **11.0.0** | 1 | tools/Build/Build.csproj |
| `Microsoft.Extensions.Http.Polly` | 10.0.13 | **11.0.0** | 1 | src/AppHost/AppHost.csproj |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.13 | **11.0.0** | 3 | src/FluentResultExtensions/FluentResultExtensions.csproj; src/FluentResults/FluentResults.csproj; src/Logging/Logging.csproj |
| `Moq` | 4.20.72 | **4.21.0** | 1 | tests/BaseTests/BaseTests.csproj |
| `Moq.Contrib.HttpClient` | 1.4.0 | **1.4.0** | 1 | tests/BaseTests/BaseTests.csproj |
| `NaturalSort.Extension` | 4.4.1 | **4.4.1** | 1 | src/Domain/Domain.csproj |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.20.1 | **1.21.0** | 1 | src/AppHost/AppHost.csproj |
| `OpenTelemetry.Extensions.Hosting` | 1.20.1 | **1.21.0** | 1 | src/AppHost/AppHost.csproj |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.20.1 | **1.21.0** | 1 | src/AppHost/AppHost.csproj |
| `OpenTelemetry.Instrumentation.Http` | 1.20.1 | **1.21.0** | 1 | src/AppHost/AppHost.csproj |
| `PasswordGenerator` | 2.1.0 | **3.0.0** | 1 | src/Domain/Domain.csproj |
| `Photino.NET` | 4.0.16 | **4.0.16** | 1 | src/AppHost/AppHost.csproj |
| `Polly` | 8.6.5 | **8.9.1** | 1 | src/Application/Application.csproj |
| `Polly.Core` | 8.6.6 | **8.9.1** | 2 | src/Application/Application.csproj; src/PlexApi/PlexApi.csproj |
| `PostHog` | 2.18.1 | **2.26.4** | 1 | src/Application/Application.csproj |
| `Quartz.Extensions.Hosting` | 3.19.1 | **4.7.3** | 1 | src/AppHost/AppHost.csproj |
| `Quartz.Serialization.SystemTextJson` | 3.19.1 | **4.7.3** | 1 | src/Application/Application.csproj |
| `SQLitePCLRaw.core` | 3.0.3 | **3.0.5** | 1 | src/Data/Data.csproj |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.13, 3.53.3 | **3.53.3** | 3 | src/Data.Contracts/Data.Contracts.csproj; src/Data/Data.csproj; src/Identity/Identity.csproj |
| `SQLitePCLRaw.provider.e_sqlite3` | 3.0.3 | **3.0.5** | 1 | src/Data/Data.csproj |
| `Semver` | 3.0.0 | **3.0.0** | 1 | src/Application/Application.csproj |
| `Serilog` | 4.3.1 | **4.4.0** | 3 | src/Environment/Environment.csproj; src/FluentResults/FluentResults.csproj; src/Logging/Logging.csproj |
| `Serilog.AspNetCore` | 10.0.0 | **11.0.0** | 2 | src/AppHost/AppHost.csproj; tools/Build/Build.csproj |
| `Serilog.Enrichers.Sensitive` | 2.1.0 | **2.1.0** | 1 | src/Logging/Logging.csproj |
| `Serilog.Expressions` | 5.0.0 | **5.0.0** | 1 | src/Logging/Logging.csproj |
| `Serilog.Sinks.AspNetCore.App.SignalR` | 1.2.2 | **1.3.0** | 1 | src/AppHost/AppHost.csproj |
| `Serilog.Sinks.Console` | 6.1.1 | **6.1.1** | 1 | src/Logging/Logging.csproj |
| `Serilog.Sinks.Console.LogThemes` | 0.4.0 | **0.4.0** | 1 | src/Logging/Logging.csproj |
| `Serilog.Sinks.Debug` | 3.0.0 | **3.0.0** | 1 | src/Logging/Logging.csproj |
| `Serilog.Sinks.File` | 7.0.0 | **7.0.0** | 1 | src/Logging/Logging.csproj |
| `Serilog.Sinks.OpenTelemetry` | 4.2.0 | **4.2.0** | 1 | src/Logging/Logging.csproj |
| `Serilog.Sinks.Seq` | 9.0.0 | **9.1.0** | 1 | src/Logging/Logging.csproj |
| `Serilog.Sinks.TestCorrelator` | 4.0.0 | **4.0.2** | 3 | src/Logging/Logging.csproj; tests/UnitTests/AppHost.UnitTests/AppHost.UnitTests.csproj; tests/UnitTests/Build.UnitTests/Build.UnitTests.csproj |
| `Shouldly` | 4.3.0 | **4.3.0** | 1 | tests/BaseTests/BaseTests.csproj |
| `Spectre.Console.Cli` | 0.55.0 | **0.57.2** | 1 | tools/Build/Build.csproj |
| `Swashbuckle.AspNetCore.SwaggerUI` | 10.3.1 | **11.0.1** | 1 | src/AppHost/AppHost.csproj |
| `System.ComponentModel.Annotations` | 5.0.0 | **5.0.0** | 1 | src/Domain/Domain.csproj |
| `System.IO.Abstractions` | 22.1.1 | **23.0.1** | 4 | src/External/External.csproj; src/FileSystem.Contracts/FileSystem.Contracts.csproj; src/FileSystem/FileSystem.csproj; tools/Build/Build.csproj |
| `System.Reactive` | 6.1.0 | **7.0.0** | 3 | src/Domain/Domain.csproj; src/External.Contracts/External.Contracts.csproj; tests/UnitTests/External.UnitTests/External.UnitTests.csproj |
| `TUnit` | 1.45.29 | **1.87.12** | 16 | all TUnit test projects listed in the direct-reference matrix below |
| `TestableIO.System.IO.Abstractions.TestingHelpers` | 22.1.1 | **23.0.1** | 2 | tests/BaseTests/BaseTests.csproj; tests/UnitTests/BaseTests.UnitTests/BaseTests.UnitTests.csproj |
| `Velopack` | 0.0.1298 | **1.2.161** | 2 | src/AppHost/AppHost.csproj; src/Application/Application.csproj |
| `Xdg.Directories` | 0.1.2 | **0.2.0** | 1 | src/Environment/Environment.csproj |
| `Yarp.ReverseProxy` | 2.3.0 | **2.3.0** | 1 | src/AppHost/AppHost.csproj |
| `coverlet.collector` | 8.0.1 | **10.1.1** | 14 | integration plus 13 unit-test projects |

## Historical project/reference matrix

The next table and matrix are retained from the 2026-11-20 pre-implementation package inventory. They are not the current manifest or lockfile state; use the 2026-11-22 stable-version audit at the top of this file for current direct versions. The original reference counts and outdated-query results below describe that earlier snapshot only.
| Project | Direct package references |
|---|---|
| `src/AppHost/AppHost.csproj` | Autofac.Extensions.DependencyInjection@11.0.0, CSharpier.MsBuild@1.4.0, FastEndpoints.OpenApi@8.3.0, FastEndpoints.Security@8.3.0, Microsoft.AspNetCore.SignalR.Protocols.MessagePack@10.0.13, Microsoft.AspNetCore.SpaServices.Extensions@10.0.13, Microsoft.Extensions.Http.Polly@10.0.13, OpenTelemetry.Exporter.OpenTelemetryProtocol@1.20.1, OpenTelemetry.Extensions.Hosting@1.20.1, OpenTelemetry.Instrumentation.AspNetCore@1.20.1, OpenTelemetry.Instrumentation.Http@1.20.1, Photino.NET@4.0.16, Quartz.Extensions.Hosting@3.19.1, Serilog.AspNetCore@10.0.0, Serilog.Sinks.AspNetCore.App.SignalR@1.2.2, Swashbuckle.AspNetCore.SwaggerUI@10.3.1, Velopack@0.0.1298, Yarp.ReverseProxy@2.3.0 |
| `src/Application.Contracts/Application.Contracts.csproj` | CSharpier.MsBuild@1.4.0 |
| `src/Application/Application.csproj` | Autofac.Extras.Quartz@11.0.0, CSharpier.MsBuild@1.4.0, Downloader@5.5.0, FastEndpoints@8.3.0, FastEndpoints.Security@8.3.0, Polly@8.6.5, Polly.Core@8.6.6, PostHog@2.18.1, Quartz.Serialization.SystemTextJson@3.19.1, Semver@3.0.0, Velopack@0.0.1298 |
| `src/Data.Contracts/Data.Contracts.csproj` | CSharpier.MsBuild@1.4.0, EFCore.BulkExtensions.SQLite@10.0.1, FlexQuery.NET.EFCore@2.5.0, Microsoft.EntityFrameworkCore@10.0.13, Microsoft.EntityFrameworkCore.Sqlite@10.0.13, SQLitePCLRaw.lib.e_sqlite3@2.1.13 |
| `src/Data/Data.csproj` | AppAny.Quartz.EntityFrameworkCore.Migrations.SQLite@0.6.0, Autofac@9.1.0, CSharpier.MsBuild@1.4.0, Microsoft.Data.Sqlite@10.0.13, Microsoft.EntityFrameworkCore.Design@10.0.13, Microsoft.EntityFrameworkCore.InMemory@10.0.13, Microsoft.EntityFrameworkCore.Sqlite@10.0.13, Microsoft.EntityFrameworkCore.Tools@10.0.13, SQLitePCLRaw.core@3.0.3, SQLitePCLRaw.lib.e_sqlite3@3.53.3, SQLitePCLRaw.provider.e_sqlite3@3.0.3 |
| `src/Domain/Domain.csproj` | ByteSize@2.1.2, CSharpier.MsBuild@1.4.0, FastEndpoints@8.3.0, Flurl@4.0.0, JetBrains.Annotations@2025.2.4, NaturalSort.Extension@4.4.1, PasswordGenerator@2.1.0, System.ComponentModel.Annotations@5.0.0, System.Reactive@6.1.0 |
| `src/Environment/Environment.csproj` | Autofac@9.1.0, CSharpier.MsBuild@1.4.0, Serilog@4.3.1, Xdg.Directories@0.1.2 |
| `src/External.Contracts/External.Contracts.csproj` | CSharpier.MsBuild@1.4.0, System.Reactive@6.1.0 |
| `src/External/External.csproj` | Autofac@9.1.0, CliWrap@3.10.1, CSharpier.MsBuild@1.4.0, System.IO.Abstractions@22.1.1 |
| `src/FileSystem.Contracts/FileSystem.Contracts.csproj` | CSharpier.MsBuild@1.4.0, System.IO.Abstractions@22.1.1 |
| `src/FileSystem/FileSystem.csproj` | Autofac@9.1.0, CSharpier.MsBuild@1.4.0, System.IO.Abstractions@22.1.1 |
| `src/FluentResultExtensions/FluentResultExtensions.csproj` | CSharpier.MsBuild@1.4.0, FluentValidation@12.1.1, Microsoft.Extensions.Logging.Abstractions@10.0.13 |
| `src/FluentResults/FluentResults.csproj` | CSharpier.MsBuild@1.4.0, Microsoft.Extensions.Logging.Abstractions@10.0.13, Serilog@4.3.1 |
| `src/Identity.Contracts/Identity.Contracts.csproj` | CSharpier.MsBuild@1.4.0, Microsoft.AspNetCore.DataProtection.EntityFrameworkCore@10.0.13, Microsoft.AspNetCore.Identity.EntityFrameworkCore@10.0.13 |
| `src/Identity/Identity.csproj` | CSharpier.MsBuild@1.4.0, FastEndpoints.Security@8.3.0, Microsoft.EntityFrameworkCore.Design@10.0.13, Microsoft.EntityFrameworkCore.InMemory@10.0.13, Microsoft.EntityFrameworkCore.Sqlite@10.0.13, Microsoft.EntityFrameworkCore.Tools@10.0.13, SQLitePCLRaw.lib.e_sqlite3@3.53.3 |
| `src/Logging/Logging.csproj` | Autofac@9.1.0, CSharpier.MsBuild@1.4.0, Microsoft.AspNetCore.Http.Abstractions@2.3.13, Microsoft.Extensions.Logging.Abstractions@10.0.13, Serilog@4.3.1, Serilog.Enrichers.Sensitive@2.1.0, Serilog.Expressions@5.0.0, Serilog.Sinks.Console@6.1.1, Serilog.Sinks.Console.LogThemes@0.4.0, Serilog.Sinks.Debug@3.0.0, Serilog.Sinks.File@7.0.0, Serilog.Sinks.OpenTelemetry@4.2.0, Serilog.Sinks.Seq@9.0.0, Serilog.Sinks.TestCorrelator@4.0.0 |
| `src/PlexApi.Contracts/PlexApi.Contracts.csproj` | CSharpier.MsBuild@1.4.0, LukeHagar.PlexAPI.SDK@0.22.3 |
| `src/PlexApi/PlexApi.csproj` | Autofac@9.1.0, CSharpier.MsBuild@1.4.0, FastEndpoints@8.3.0, HttpClientToCurl@2.1.1, Polly.Core@8.6.6 |
| `src/PublicAPI.Contracts/PublicAPI.Contracts.csproj` | CSharpier.MsBuild@1.4.0 |
| `src/PublicAPI/PublicAPI.csproj` | Autofac@9.1.0, BencodeNET@5.0.0, CSharpier.MsBuild@1.4.0, FastEndpoints@8.3.0 |
| `src/Settings.Contracts/Settings.Contracts.csproj` | CSharpier.MsBuild@1.4.0 |
| `src/Settings/Settings.csproj` | Autofac@9.1.0, CSharpier.MsBuild@1.4.0 |
| `src/SignalR.Contracts/SignalR.Contracts.csproj` | CSharpier.MsBuild@1.4.0 |
| `src/SignalR/SignalR.csproj` | Autofac@9.1.0, CSharpier.MsBuild@1.4.0, MessagePack@3.1.9 |
| `tests/BaseTests/BaseTests.csproj` | Autofac@9.1.0, Autofac.Extras.Moq@7.0.0, Bogus@35.6.5, Bogus.Hollywood@35.6.5, CSharpier.MsBuild@1.4.0, FastEndpoints.Testing@8.3.0, Moq@4.20.72, Moq.Contrib.HttpClient@1.4.0, Shouldly@4.3.0, TestableIO.System.IO.Abstractions.TestingHelpers@22.1.1, TUnit@1.45.29 |
| `tests/IntegrationTests/IntegrationTests/IntegrationTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, Microsoft.AspNetCore.Mvc.Testing@10.0.13, TUnit@1.45.29 |
| `tests/UnitTests/AppHost.UnitTests/AppHost.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, Serilog.Sinks.TestCorrelator@4.0.0, TUnit@1.45.29 |
| `tests/UnitTests/Application.UnitTests/Application.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/BaseTests.UnitTests/BaseTests.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TestableIO.System.IO.Abstractions.TestingHelpers@22.1.1, TUnit@1.45.29 |
| `tests/UnitTests/Build.UnitTests/Build.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, Serilog.Sinks.TestCorrelator@4.0.0, TUnit@1.45.29 |
| `tests/UnitTests/Data.UnitTests/Data.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/Domain.UnitTests/Domain.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/Environment.UnitTests/Environment.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/External.UnitTests/External.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, System.Reactive@6.1.0, TUnit@1.45.29 |
| `tests/UnitTests/FileSystem.UnitTests/FileSystem.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/FluentResultExtension.UnitTests/FluentResultExtension.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/Logging.UnitTests/Logging.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/PlexApi.UnitTests/PlexApi.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/PublicApi.UnitTests/PublicApi.UnitTests.csproj` | coverlet.collector@8.0.1, CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tests/UnitTests/Settings.UnitTests/Settings.UnitTests.csproj` | CSharpier.MsBuild@1.4.0, TUnit@1.45.29 |
| `tools/Build/Build.csproj` | Autofac@9.1.0, Autofac.Extensions.DependencyInjection@11.0.0, CliWrap@3.10.1, FastEndpoints@8.3.0, FluentValidation@12.1.1, Microsoft.Extensions.DependencyInjection@10.0.13, Serilog.AspNetCore@10.0.0, Spectre.Console.Cli@0.55.0, System.IO.Abstractions@22.1.1 |

## Historical version-coherence flags

The following flags describe the pre-upgrade inventory above, not current package versions. The 2026-11-22 audit found Quartz 4.7.3 is the only retained direct ID behind latest stable.

## EF11 and remaining package compatibility questions

* EF Core 11.0.0's NuGet metadata declares a `net11.0` group. Microsoft documents EF11 changes at <https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/breaking-changes>, including migration/no-migrations behavior, split-query concurrency, the EF Tools/Design dependency change, primitive collection configuration and provider/native changes. These are source/runtime gates, not solved by version replacement.
- **BulkExtensions EF11 compatibility is awaiting upstream:** `src/Data.Contracts/Data.Contracts.csproj` directly references `EFCore.BulkExtensions.SQLite` 10.0.1; the `net11.0` lock graph resolves its SQLite and Core packages to 10.0.1. Original BulkExtensions methods and configured application callsites are restored. Upstream [PR #1971](https://github.com/borisdj/EFCore.BulkExtensions/pull/1971) reports that 10.0.1 fails against EF Core 11 because `GetTableColumnMappings` changed return type, and proposes an EF11-targeted build; the PR remains open and is not a release. The user directed us to retain BulkExtensions and defer this upgrade pending the package fix. In the post-restoration run of `ReaparrDbContextConcurrencyUnitTests`, 2 tests passed and 2 failed; one reproduces that `MissingMethodException` in the contending bulk-write test, and the rollback test failed its expected-exception assertion after the bulk operation failed. Do not substitute native EF persistence APIs. Update package and locks only after a stable EF11-compatible release, then rerun those Data tests and broader affected suites.
* **FlexQuery:** current direct package is `FlexQuery.NET.EntityFrameworkCore` 4.0.0 and its lock resolves EF Core 10.0.0 alongside direct EF11. Metadata describes a minimum, not EF11 behavior. Test its actual overview/search callers on EF11 and verify returned rows.
* **AppAny:** current SQLite package is 0.6.2. Its published nuspec declares an EF SQLite 10.0.8 dependency group; its README lists Quartz 4.0/4.2/4.3 mappings, with no explicit 4.8.1/EF11 guarantee. Reaparr's `QuartzSqliteSchema` supplies later Quartz columns and execution-history/job-status mappings. Verify the existing model/snapshot and fresh plus prior-Reaparr migration fixture on EF11/Quartz 4.8.1; do not remove the bridge or rewrite applied migrations without a released equivalent mapping and generated diff.

### Downloader

`Downloader` 7.1.0 has a net11 target group and no dependency in that group (<https://api.nuget.org/v3-flatcontainer/downloader/7.1.0/downloader.nuspec>). Its release notes explicitly fix storage-error completion, final flush/close/rename completion, failed setup disposal, invalid resume package validation, same-file concurrency, failed rename retention, and pooled-buffer double-return. These changes are relevant to Reaparr's range/resume/pause/cancel/cleanup contracts, but package metadata is not application behavior proof. Run focused fake-server and persisted-download tests after migration.

### Additional package-family findings from primary-source review

The following resolved transitive-package and consumer findings refine the scoped review above; they are not direct-package table entries unless explicitly identified as direct.

- **Microsoft.OpenApi 3.11.0 (transitive, current lock):** the current net11 OpenAPI stack resolves Microsoft.OpenApi 3.11.0, while upstream 3.11.1 was released on November 16 with fixes for `OpenApiDocument` copy-constructor preservation of extensions/tags and reader allocation. This is a confirmed patch-version gap in the transitive graph, but not evidence of a Reaparr vulnerability or a consumer bug. Evaluate package update with the .NET 11 OpenAPI/API-generation gate; verify the resolved lock version and contract output rather than assuming FastEndpoints v9 is the only OpenAPI dependency change. Sources: [3.11.1 release notes](https://github.com/microsoft/OpenAPI.NET/releases/tag/v3.11.1), [v3 upgrade guide](https://github.com/microsoft/OpenAPI.NET/blob/main/docs/upgrade-guide-3.md).
- **Downloader 7.1.0:** the ledger records completed-file length verification and terminal progress ordering. A specific unresolved case remains: for a resumed transfer with a single segment, if a range-advertising server replies with the complete file, the single-segment path rejects rather than appending at the saved offset. Do not add a separate retry without approval; preserve this as an explicit behavior limitation and exercise it on .NET 11 if the acceptance contract requires recovery. The full-file-space requirement when a short resume artifact forces restart and parallel real-callback timing also need runtime evidence.
- **Polly legacy adapter coexistence:** current direct Polly and Polly.Core are 8.9.1, but AppHost's direct `Microsoft.Extensions.Http.Polly` 11.0.0 still resolves Polly 7.2.4 and `Polly.Extensions.Http` 3.0.0. This is a mixed-generation graph, not proof of a conflict. Retain it unless an independently approved migration is warranted; verify the current HTTP-policy adapter is active as intended and do not claim the application has moved entirely to Polly 8.
- **FastEndpoints/OpenAPI contract:** FastEndpoints 9 is current stable for the upgraded family and supports .NET 11, but its .NET 11 OpenAPI default is 3.2 (the verified .NET 10 document was 3.1.1); API versioning integration remains .NET 10-only. No .NET 11 OpenAPI document/client regeneration, API-versioning test, or Swagger UI render is evidenced. Required gate: start the .NET 11 host, capture both internal/public documents, regenerate client, assert representative endpoint/request/response schemas, run client typecheck and contract tests, then verify the UI if acceptance includes it.
- **Telemetry/logging/updater families:** PostHog 2.26.5, OpenTelemetry 1.21.0, Serilog 4.4.0/ASP.NET Core 11, SignalR sink 1.3.0, Seq sink 9.1.0, and TestCorrelator 4.0.2 are package-aligned/current according to the recorded direct audit, but no .NET 11 end-to-end event/export/logging evidence exists. Velopack 1.2.161 has net8/net9/net10 package assets but no dedicated net11 asset; this is unknown compatibility, not by itself a failure. Its installed old-client→download→apply→restart path is unverified. Keep these runtime gaps distinct from version metadata.
- **Testing toolchain:** TUnit 1.87.12 explicitly declares net11; MTP coverage 18.12.0 and telemetry/TRX 2.5.3 come through its dependency graph after removing `coverlet.collector`. The .NET 10 suite passed, but no .NET 11 full test or coverage run proves runner discovery, hooks, per-project coverage paths or non-empty Cobertura. CSharpier.MsBuild 1.4.1 supports current TFM/C# generation but formatter execution is outside this audit. No claim that every project or coverage output has been validated on net11.
- **Tooling-family caveats:** `System.IO.Abstractions` and testing helpers are aligned at 23.0.1, and current graph resolves `Testably.Abstractions.FileSystem.Interface` 11.0.0; path-sensitive Windows/Linux/macOS behavior remains to verify under supported RIDs. `JetBrains.Annotations` 2026.2.0 is a direct Domain dependency but no JetBrains annotations were found in the scanned source, so package utility/necessity is unproven (not a runtime finding). `HttpClientToCurl` 2.2.0 has no identified application callsite; `PasswordGenerator` 3.0.0 only appears through `StringExtensions.GeneratePassword` and no caller migration was visible. No release-note-specific compatibility conclusion is claimed for those usages.
- **Unchanged SDK/client dependencies:** `LukeHagar.PlexAPI.SDK` 0.22.3 and Flurl 4.0.0 were not upgraded in this work; this audit does not establish live Plex API response compatibility. Plex canary behavior remains outside package-version evidence.

### Velopack

`Velopack` 1.2.161 declares net10/net9/net8 assets (<https://api.nuget.org/v3-flatcontainer/velopack/1.2.161/velopack.nuspec>), but no net11-specific group. This is not a .NET11 incompatibility claim: lower-TFM compatibility remains unverified. The package is used in `src/AppHost` and `src/Application`; the build tool invokes external `vpk` from PATH in `tools/Build/Workflows/DesktopPackageWorkflow.cs`, not a checked-in tool manifest. Upgrade runtime package and the separately acquired `vpk` together, then test feed selection, staging/apply/restart, packaged startup, channels and dry-run semantics on all six desktop RIDs.

### TUnit and coverage

TUnit 1.87.12 explicitly declares net10 and net11 groups and pins its own assertions/engine plus Microsoft.Testing.Extensions.CodeCoverage 18.12.0, telemetry/TRX 2.5.3 (<https://api.nuget.org/v3-flatcontainer/tunit/1.87.12/tunit.nuspec>). This supports a planned target but does not prove Reaparr's hooks and all test projects migrate. `coverlet.collector` 10.1.1 metadata has no framework dependency group (<https://api.nuget.org/v3-flatcontainer/coverlet.collector/10.1.1/coverlet.collector.nuspec>); preserve the existing CI's actual coverage path only after running it on Microsoft.Testing.Platform and confirming non-empty Cobertura line data. No coverage run occurred here.

### Reactive and filesystem

System.Reactive 7.0.0 declares net8/net8-windows plus portable/netstandard assets, not net11-specific assets (<https://api.nuget.org/v3-flatcontainer/system.reactive/7.0.0/system.reactive.nuspec>); runtime compatibility is therefore unverified. System.IO.Abstractions 23.0.1 and TestableIO testing helpers 23.0.1 both declare net11 groups and keep their production/test package versions aligned (<https://api.nuget.org/v3-flatcontainer/system.io.abstractions/23.0.1/system.io.abstractions.nuspec>, <https://api.nuget.org/v3-flatcontainer/testableio.system.io.abstractions.testinghelpers/23.0.1/testableio.system.io.abstractions.testinghelpers.nuspec>). Verify subscription disposal/callback cessation and deterministic `MockFileSystem` move/rename/collision/path/cancellation behavior; metadata alone does not cover Reaparr's seams.

## Current package and compatibility decisions

1. **Quartz:** migration 3.19.1→4.7.3/native DI is complete; the 2026-11-22 audit found 4.8.1 latest stable, while current source/lock remain 4.7.3. Per user direction, do not begin the 4.8.1 runtime gate until a stable EF11-compatible BulkExtensions release is available, the source/package graph is reconciled, and Data persistence tests pass. Quartz 4.8 has no additional upstream schema migration; retain Reaparr's applied EF schema bridge and migration.
2. **Bulk/range persistence — awaiting upstream EF11 package:** retain the `EFCore.BulkExtensions.SQLite` 10.0.1 reference, `BulkConfigPreset`, and existing `BulkInsertAsync`/`BulkUpdateAsync` contracts and application callsites. Under .NET 11, `ReaparrDbContextConcurrencyUnitTests` ran 4 tests: 2 passed, 2 failed. The contending bulk-write case fails with the upstream-reported `MissingMethodException`; the rollback case fails its expected-exception assertion after that bulk failure. Upstream [PR #1971](https://github.com/borisdj/EFCore.BulkExtensions/pull/1971) remains open and proposes an EF11 build. The user asked to keep BulkExtensions and leave the upgrade waiting for the fix. After a stable package release, update and lock it, then rerun the Data class and affected media-sync tests; the earlier native-range duplicate-key failures are obsolete and must not be presented as current BulkExtensions failures.
3. **FlexQuery:** current `FlexQuery.NET.EntityFrameworkCore` 4.0.0 has a transitive EF Core 10.0.0 lock edge. Test its actual overview/search callers under EF11 and assert returned data.
4. **AppAny:** retain SQLite 0.6.2 and the local mapping bridge until Quartz 4.8.1/EF11 compatibility is verified against the generated model/snapshot and migration fixture. Do not remove mappings or rewrite applied migrations without a released equivalent and generated diff.
5. **Framework/native graph:** Microsoft platform references are moved coherently to 11.0.0. Inspect remaining EF10/Extensions 10.0.x dependency minimums and validate resolved assets/native SQLite loading per required RID; assets and compile/restore are not behavior proof.
6. **Latest-stable audit:** the 2026-11-22 direct-package table covers all 75 retained direct IDs; Quartz 4.7.3 is the sole intentionally stale ID behind 4.8.1. Re-run the live feed audit at lockfile update; the deferred package is not latest-stable compliant until that gate passes.

## Coverage limits and verification gates

* **Direct-package coverage:** 75 direct IDs were inventoried across 41 PackageReference project files/41 lockfiles; all 75 current versions were checked against NuGet.org version indexes on 2026-11-22. The legacy `docker/docker-compose.dcproj` is unsupported by `dotnet package list` and is excluded. The installed CLI does not accept a positional solution argument: `dotnet package list Reaparr.sln ...` failed as invalid syntax. `dotnet package list --project Reaparr.sln --outdated --format json --no-restore` completed with a project-format error for the Docker compose project and emitted only a Quartz result, so it is not treated as a successful whole-solution audit.
* **Changelog/best-practice review scope:** audit covered package families changed in the accepted package migration and direct/resolved transitive packages with material Reaparr application, build, test, storage, download, scheduler, API or updater contracts. The 232 package IDs in the union of 41 lockfiles were not each individually reviewed against every release note. Family-level primary evidence, caller decisions, implemented changes and remaining unverified behavior are recorded in [Implemented package batches](#implemented-package-batches-on-net-10), the compatibility subsections above and the Quartz plan. NuGet packages with only build/test-time effects are not claimed as application runtime evidence.
* **Verification boundary:** .NET 10 locked restore, full backend unit/integration tests (4,751 passed), API generation/typecheck and focused DirectoryBrowser tests are historical verified evidence recorded below. Current .NET 11 Data concurrency tests on restored BulkExtensions 10.0.1 ran 4 cases: 2 passed and 2 failed, including the upstream-reported EF11 `MissingMethodException` and a failed rollback assertion after the bulk failure. No complete .NET 11 backend test pass is established. Docker and desktop smoke/publish checks remain explicitly excluded; downloader update-install/restart, all target RIDs and runtime platform coverage are not proven.

## Primary sources

* NuGet source used for current version inventory: <https://api.nuget.org/v3/index.json>. Current package indexes used in the 2026-11-22 direct-ID table; version facts do not imply runtime support.
* EF11 breaking changes: <https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/breaking-changes>.
* BulkExtensions SQLite 10.0.1 feed metadata (current direct dependency; EF Core 11 compatibility is disproven by the reproduced test failure): [version index](https://api.nuget.org/v3-flatcontainer/efcore.bulkextensions.sqlite/index.json), [nuspec](https://api.nuget.org/v3-flatcontainer/efcore.bulkextensions.sqlite/10.0.1/efcore.bulkextensions.sqlite.nuspec), upstream [open EF11 PR #1971](https://github.com/borisdj/EFCore.BulkExtensions/pull/1971).
* FlexQuery 4.0.0 nuspec: <https://api.nuget.org/v3-flatcontainer/flexquery.net.entityframeworkcore/4.0.0/flexquery.net.entityframeworkcore.nuspec>.
* AppAny SQLite 0.6.2 nuspec: <https://api.nuget.org/v3-flatcontainer/appany.quartz.entityframeworkcore.migrations.sqlite/0.6.2/appany.quartz.entityframeworkcore.migrations.sqlite.nuspec>.
* AppAny 0.6.2 public model README: <https://github.com/appany/AppAny.Quartz.EntityFrameworkCore.Migrations/blob/09e1b2d84f547cf67599397c6d0eb9f258b9ea59/README.md>.
* Downloader 7.1.0 nuspec: <https://api.nuget.org/v3-flatcontainer/downloader/7.1.0/downloader.nuspec>.
* Velopack 1.2.161 nuspec: <https://api.nuget.org/v3-flatcontainer/velopack/1.2.161/velopack.nuspec>.
* TUnit 1.87.12 nuspec: <https://api.nuget.org/v3-flatcontainer/tunit/1.87.12/tunit.nuspec>.
* System.IO.Abstractions 23.0.1 nuspec: <https://api.nuget.org/v3-flatcontainer/system.io.abstractions/23.0.1/system.io.abstractions.nuspec>.
* Testing helpers 23.0.1 nuspec: <https://api.nuget.org/v3-flatcontainer/testableio.system.io.abstractions.testinghelpers/23.0.1/testableio.system.io.abstractions.testinghelpers.nuspec>.
* System.Reactive 7.0.0 nuspec: <https://api.nuget.org/v3-flatcontainer/system.reactive/7.0.0/system.reactive.nuspec>.

## Implemented package batches on .NET 10

This ledger follows the completed .NET 10 dependency migration from baseline `184b07619`. The dated baseline table and project matrix above are historical; the latest direct package versions appear in the current audit at the top. Historical checks below do not prove .NET 11 runtime compatibility.

| Package range | Upstream evidence and Reaparr disposition |
| --- | --- |
| Quartz 3.19.1 → 4.7.3 | [Stable release notes](https://github.com/quartznet/quartznet/releases) and [4.7.3 migration guide](https://github.com/quartznet/quartznet/blob/v4.7.3/docs/documentation/quartz-4.x/migration-guide.md). The old Autofac adapter cannot resolve Quartz 4; native DI and System.Text.Json now come from `Quartz` itself. Reaparr migrated jobs, listeners, and paged scheduler queries and retained ownership of startup and shutdown. `Boot.StopAsync` now shares a single shutdown task across concurrent callers; a repeat stop no longer queries an already-shut-down scheduler. The existing UTC schedules are unchanged; optional fixed-delay, dashboard, and cluster features were not added. See the [Quartz plan](dotnet11-quartz-upgrade.md) for the SQLite schema and persistent-store evidence. |
| AppAny Quartz SQLite 0.6.0 → 0.6.2 | [0.6.1](https://github.com/appany/AppAny.Quartz.EntityFrameworkCore.Migrations/releases/tag/0.6.1) adds Quartz 3/4 mappings; [0.6.2](https://github.com/appany/AppAny.Quartz.EntityFrameworkCore.Migrations/releases/tag/0.6.2) aligns indexes with Quartz 4.0.1. The existing local bridge covers the remaining Quartz 4.7 schema, including history; no new migration was added in this pass. If migration fails after DB connection succeeds, startup returns the original error without resetting the database. The existing unconnectable-database reset path is unchanged. |
| FastEndpoints family 8.3.0 → 9.0.0 | [v9.0 release notes](https://github.com/FastEndpoints/FastEndpoints/releases/tag/v9.0) and [OpenAPI.NET releases](https://github.com/microsoft/OpenAPI.NET/releases) for the resolved 2.7.5 → 2.13.0 range. The upgraded host serves internal/public OpenAPI 3.1.1. Regenerated the TypeScript client from the hosted spec and adapted the shared template to keep array responses typed as arrays of their DTOs. The test URL-cache route stays off; hosted tests use the in-process fixture. The OpenAPI examples/schema changes do not change HTTP payloads. |
| FlexQuery.NET.EFCore 2.5.0 → FlexQuery.NET.EntityFrameworkCore 4.0.0 | [Stable releases](https://github.com/peterjohncasasola/FlexQuery.NET/releases) and [published 4.0.0 package](https://www.nuget.org/packages/FlexQuery.NET.EntityFrameworkCore/4.0.0). Adopted the new adapter and query contracts. Quote normalized title terms so the v4 parser treats `AND`/`OR` as search text when appropriate, including words with those suffixes. A hosted SQLite test checks returned media through both overview paths. Query telemetry maps the bound request explicitly without changing the response. |
| Downloader 5.5.0 → 7.1.0 | [Stable releases](https://github.com/bezzad/Downloader/releases), notably [6.0.0](https://github.com/bezzad/Downloader/releases/tag/v6.0.0), [7.0.0](https://github.com/bezzad/Downloader/releases/tag/v7.0.0), and [7.1.0](https://github.com/bezzad/Downloader/releases/tag/v7.1.0). Reaparr does not publish sampled progress after terminal completion and budgets for a restart when Downloader will discard a short resume artifact. Regression tests cover late progress, existing resume artifacts, and exact completed bytes plus cleanup when the server ignores a saved range. Keep platform certificate checks and system-proxy defaults; do not turn on unrelated live streaming or dynamic chunk splitting. |
| System.Reactive 6.1.0 → 7.0.0 | [Rx 7 release history](https://github.com/dotnet/reactive/blob/rxnet-v7.0.0/Rx.NET/Documentation/ReleaseHistory/Rx.v7.md). The platform-specific package split does not affect the server-side operators used here. The Downloader completion regression exercises the changed event-producer lifetime, not a new Rx API contract. |
| System.IO.Abstractions and TestingHelpers 22.1.1 → 23.0.1 | [22.2.0](https://github.com/TestableIO/System.IO.Abstractions/releases/tag/v22.2.0), [22.3.0](https://github.com/TestableIO/System.IO.Abstractions/releases/tag/v22.3.0), [23.0.0](https://github.com/TestableIO/System.IO.Abstractions/releases/tag/v23.0.0), [23.0.1](https://github.com/TestableIO/System.IO.Abstractions/releases/tag/v23.0.1). Shared mock file handles, lazy mock filesystem metadata, and Unix root-drive naming are fixed upstream. Runtime wrappers and test helpers were upgraded together; the filesystem and Build suites passed without replacing the existing filesystem abstraction. |
| Velopack 0.0.1298 → 1.2.161 | [1.0.1](https://github.com/velopack/velopack/releases/tag/1.0.1), [1.1.1](https://github.com/velopack/velopack/releases/tag/1.1.1), [1.2.0](https://github.com/velopack/velopack/releases/tag/1.2.0), [1.2.158](https://github.com/velopack/velopack/releases/tag/1.2.158), [1.2.161](https://github.com/velopack/velopack/releases/tag/1.2.161). The switch away from `NuGet.Versioning` is reflected in the graph; current call sites use the version as text and CI already pins `vpk` to 1.2.161. The package brings native locator, installer, and delta-format changes. Installed feed discovery and update/restart across the version boundary were not exercised. |
| Autofac 9.1.0 → 9.3.4 and Autofac.Extensions.DependencyInjection 11.0.0 → 11.0.2 | [Autofac 9.2.0–9.3.4 releases](https://github.com/autofac/Autofac/releases), [DI bridge 11.0.1](https://github.com/autofac/Autofac.Extensions.DependencyInjection/releases/tag/v11.0.1), [11.0.2](https://github.com/autofac/Autofac.Extensions.DependencyInjection/releases/tag/v11.0.2). Improvements to scope ownership, open-generic resolution, registration and resolve cost are inherited; 9.3.4 includes the bridge-relevant 9.3.3 open-generic pipeline regression fix. Existing composition and scope ownership remain in place. |
| Autofac.Extras.Moq 7.0.0 → 8.0.1 and Moq 4.20.72 → 4.21.0 | [Extras.Moq releases](https://github.com/autofac/Autofac.Extras.Moq/releases) and [Moq 4.21.0](https://github.com/devlooped/moq/releases/tag/v4.21.0). The adapter uses the newer Autofac floor; Moq inherits matcher allocation and ValueTask setup fixes. Existing test infrastructure remains in place. The shutdown fixture now supplies the real constructor's lifetime-token contract rather than weakening strict mocks. |
| CliWrap 3.10.1 → 3.10.5 | [3.10.2–3.10.5 releases](https://github.com/Tyrrrz/CliWrap/releases). Process ownership and iterator cleanup are fixed upstream. The `PipeTarget.Merge` deadlock fix requires no Reaparr change; its command runners do not use that API. |
| FluentValidation 12.1.1 → 12.2.0 | [12.2.0 notes](https://github.com/FluentValidation/FluentValidation/releases/tag/12.2.0). Aligned direct references with FastEndpoints' dependency. The collection-path fix applies to the existing conditional `RuleForEach` rules in `PlexMediaDTOValidator` and `DeleteTorrentRequestValidator` and is inherited without a caller rewrite. The range and Native AOT fixes require no application change. |

Quartz release coverage includes stable 3.x maintenance notes (3.20.0–3.20.1, 3.21.0, 3.22.0–3.22.10) to distinguish backports from 4.x behavior. The target line covers 4.0.0–4.0.1, 4.1.0–4.1.1, 4.2.0–4.2.4, 4.3.0, 4.4.0, 4.5.0–4.5.1, 4.6.0–4.6.3, and 4.7.0–4.7.3. This is note coverage, not a claim that all 3.x maintenance releases were installed or are ancestors of 4.7.3.

Downloader release coverage includes 5.6.0, [5.7.0 package release notes](https://api.nuget.org/v3-flatcontainer/downloader/5.7.0/downloader.nuspec) (no GitHub release), 5.8.0–5.8.1, 5.9.0–5.9.1, 5.9.4–5.9.8, 5.10.0–5.10.2, 6.0.0–6.0.1, 7.0.0–7.0.2, and 7.1.0. The 5.7 transport retry changes run inside Downloader; they do not require another Reaparr retry path.

The ignored-range integration test proves exact bytes and cleanup for a two-chunk plan. Downloader 7.1's [single-connection fallback](https://github.com/bezzad/Downloader/blob/v7.1.0/src/Downloader/AbstractDownloadService.cs) does not run for an already single-segment plan. If a server advertises range support but replies with the whole file to a resumed single-segment request, Downloader rejects that response rather than appending it at the saved offset. Reaparr does not add another retry path.

Moq also resolves [Castle.Core 5.2.1](https://github.com/castleproject/Core/releases/tag/v5.2.1) instead of 5.1.1. Upstream skipped 5.2.0. Its nullable proxy annotations, default interface method support, and generic proxy fixes do not require direct proxy changes in Reaparr's Moq-based fixtures.

The filesystem wrappers also resolve `Testably.Abstractions.FileSystem.Interface` 10.1.0 → 11.0.0. The [published-source comparison](https://github.com/Testably/Testably.Abstractions/compare/1253c6bd6309cb15623fac09bd841e782bbca626...ae901911f9dafb826fc40b564c1053743105e2ec) is the evidence for that range rather than the separately versioned GitHub releases. New file-handle and random-access APIs are not needed by Reaparr's existing filesystem calls. The comparison includes changes in sibling packages, so those are not attributed to the interface package.

FlexQuery's released notes span 3.0.0–3.0.6, 3.1.0–3.1.1, and 4.0.0. FastEndpoints publishes 9.0.0 under tag `v9.0`; there is no intervening stable 8.4 release. The OpenAPI.NET release review covers the transitive range resolved by the FastEndpoints upgrade, not a migration to OpenAPI.NET 3.x or to NSwag.

Velopack 1.2.158 removes legacy delta support in favor of zstd; the new client falls back to the full package if delta application fails. Reaparr explicitly supplies `GithubSource`, so changes to the default update source do not apply. The .NET 10 checks do not establish that an already-installed older client and the new feed can complete an update; that needs a packaged update rehearsal, not another managed unit run.

The package table covers all 75 current direct IDs for version status, and the implementation ledger covers the approved direct-package changes and transitive changes relevant to Reaparr's application or test contracts. The latter is not an exhaustive changelog audit of every package in the resolved graph.

### .NET 10 verification

- `dotnet test --solution Reaparr.sln --no-restore --no-ansi --no-progress --max-parallel-test-modules 2`: **4,751 passed, 0 failed, 0 skipped** across the backend unit and integration suites.
- Captured OpenAPI 3.1.1 from the upgraded local host: 90 internal and 13 public paths. Regenerated the frontend client from the internal document; `bun run typecheck` passed. The focused directory-browser suite passed **8/8** against the generated client. This is not a full browser or SignalR check.
- Locked restore passed on .NET 10. Docker and desktop smoke tests were excluded at the user's request; installed update/restart and native platform verification are not established by the unit suite. On .NET 11, locked restore/compilation evidence alone is not acceptance. The current targeted Data concurrency class has 2 passing and 2 failing tests for restored BulkExtensions 10.0.1; no full .NET 11 test suite passed. CodeScene's per-file review tool was not exposed in this session.

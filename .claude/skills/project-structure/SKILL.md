---
name: dotnet-project-structure
description: Organize .NET solutions with modern project structure, Directory.Build.props, Central Package Management, SourceLink, and SDK pinning. Emphasizes .NET 8/9+ best practices.
invocable: false
---

# .NET Project Structure and Build Configuration

## When to Use This Skill

Use this skill when:
- Creating new .NET solutions or projects
- Setting up build configuration across multiple projects
- Managing NuGet dependencies consistently
- Configuring CI/CD for .NET builds
- Establishing project conventions for a team

---

## Solution Structure

### Recommended Layout

```
MySolution/
├── global.json                          # SDK version pinning
├── Directory.Build.props                # Shared build properties
├── Directory.Build.targets              # Shared build targets
├── Directory.Packages.props             # Central Package Management
├── MySolution.sln                       # Solution file
├── src/
│   ├── MyApp.Api/
│   │   └── MyApp.Api.csproj
│   ├── MyApp.Core/
│   │   └── MyApp.Core.csproj
│   └── MyApp.Infrastructure/
│       └── MyApp.Infrastructure.csproj
└── tests/
    ├── MyApp.Tests.Unit/
    │   └── MyApp.Tests.Unit.csproj
    └── MyApp.Tests.Integration/
        └── MyApp.Tests.Integration.csproj
```

---

## SDK Pinning with global.json

Pin SDK version for consistent builds:

```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestFeature"
  }
}
```

`latestFeature` allows security patches within the feature band while preventing unexpected SDK upgrades.

---

## Directory.Build.props

Centralize shared build configuration:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <!-- SourceLink for debugging -->
  <PropertyGroup>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>
</Project>
```

---

## Central Package Management

Use `Directory.Packages.props` to manage all NuGet versions in one place:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Framework -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.9.0" />
    <PackageVersion Include="FluentAssertions" Version="7.0.0" />
  </ItemGroup>
</Project>
```

In individual `.csproj` files, reference without version:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
</ItemGroup>
```

**Benefits:**
- Single source of truth for dependency versions
- No version drift across projects
- Easier security audit and updates
- Simpler PR reviews (version changes in one file)

---

## Project File Best Practices

Keep `.csproj` files minimal - push shared config to `Directory.Build.props`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Only project-specific settings -->
  <PropertyGroup>
    <RootNamespace>MyApp.Api</RootNamespace>
    <AssemblyName>MyApp.Api</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp.Core\MyApp.Core.csproj" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
  </ItemGroup>
</Project>
```

---

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Solution | `CompanyOrProject.sln` | `Jellyfin.Plugin.MusicDiscovery.sln` |
| Project | `Namespace.Feature.csproj` | `Jellyfin.Plugin.MusicDiscovery.csproj` |
| Test project | `Project.Tests.Category.csproj` | `MyApp.Tests.Unit.csproj` |
| Root namespace | Matches project name | `Jellyfin.Plugin.MusicDiscovery` |

---

## Resources

- **Central Package Management**: https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
- **Directory.Build.props**: https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory
- **SourceLink**: https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink

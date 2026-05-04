# DarivaBIM.Revit.Hosting

Source-shared layer of Revit-aware composition: DI host, command executor,
ribbon builder and external-event scaffolding. Each plugin
(`DarivaBIM.Plugin.V2025`, `DarivaBIM.Plugin.V2026`) compiles these files
against its own `RevitAPI.dll` via `<Compile Include>` in the `.csproj`.

## Política

Esta pasta **não tem `.csproj`** e **não gera DLL própria**, pelo mesmo
motivo que `DarivaBIM.Plugin.SharedSource` e
`DarivaBIM.Revit.Adapters.SharedSource` (ver ADR-0008, ADR-0015 e
ADR-0017): cada Revit (2025/2026) tem sua própria
`RevitAPI.dll` e uma DLL pré-compilada contra uma versão pode falhar em
outra. Compartilhando **fonte**, cada plugin tem um Hosting binariamente
correto para a sua versão.

## O que vive aqui

| Subfolder              | Responsabilidade                                   |
| ---------------------- | -------------------------------------------------- |
| `Commands/`            | `RevitCommandExecutor`, `RevitCommandContext`,     |
|                        | `RevitDocumentContext` — DI scope por comando      |
| `DependencyInjection/` | `PluginHost` — root provider, escopo de comando    |
| `Events/`              | `RevitApplicationContext` — wrap do                |
|                        | `UIControlledApplication`                          |
| `Ribbon/`              | `RibbonBuilder` — declarativo, traduz              |
|                        | `RibbonDefinition` para PushButton/ContextualHelp  |

## Como o include funciona

No `Plugin.V20XX.csproj`:

```xml
<ItemGroup>
  <Compile Include="..\..\Revit\DarivaBIM.Revit.Hosting\**\*.cs"
           Exclude="..\..\Revit\DarivaBIM.Revit.Hosting\**\bin\**;..\..\Revit\DarivaBIM.Revit.Hosting\**\obj\**"
           Link="Hosting\%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

E os pacotes que antes vinham via `ProjectReference` agora são declarados
diretamente em cada plugin:

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
```

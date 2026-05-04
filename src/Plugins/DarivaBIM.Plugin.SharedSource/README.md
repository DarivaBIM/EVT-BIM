# DarivaBIM.Plugin.SharedSource

Pasta de **arquivos-fonte e recursos compartilhados** entre os plugins
versionados (`DarivaBIM.Plugin.V2025`, `DarivaBIM.Plugin.V2026`).

## Política

Esta pasta **não tem `.csproj` próprio** e **não gera DLL**. Cada plugin
versionado inclui os arquivos daqui via `MSBuild Link` no seu próprio
`.csproj`, compilando-os contra a `RevitAPI.dll` da versão correspondente.

Princípio (ADR-0015 + ADR-0008):

- Compartilhar **fonte**, não DLL — porque cada Revit (2025/2026) tem
  sua própria `RevitAPI.dll` e uma DLL pré-compilada contra uma versão
  pode falhar em outra.
- Cada `Plugin.V20XX` continua produzindo seu próprio assembly final
  com nome e identidade independentes.

## O que vai aqui

| Tipo                                        | Vai aqui? | Observação                              |
| ------------------------------------------- | :-------: | --------------------------------------- |
| Ícones (PNG/ICO)                            |    Sim    | `Resources/Icons/`                      |
| `RibbonDefinition`, `CommandRegistry`       |    Sim    | `Ribbon/`                               |
| Definições declarativas de painel/botão     |    Sim    | `Ribbon/Panels/`, `Features/*/`         |
| Comandos (`IExternalCommand`) idênticos     |    Sim    | `Features/<Tool>/`                      |
| `Tool`/`Handler`/`ExternalEvent` idênticos  |    Sim    | `Features/<Tool>/`                      |
| XAML que usa RevitAPI/UIApplication         |    Sim    | `Ui/`                                   |
| Recursos JSON/embarcados compartilhados     |    Sim    | `Resources/`                            |

## O que NÃO vai aqui

| Tipo                                                | Onde fica                                   |
| --------------------------------------------------- | ------------------------------------------- |
| `App.cs`, `IExternalApplication`                    | `Plugin.V20XX/`                             |
| `.addin` manifest                                   | `Build/AddinManifests/`                     |
| `.csproj`                                           | `Plugin.V20XX/`                             |
| ViewModel WPF puro (sem RevitAPI)                   | `Presentation.Wpf/`                         |
| Regra de negócio                                    | `Domain/` ou `Application/`                 |
| Código que difere entre Revit 2025 e 2026           | `Plugin.V20XX/VersionSpecific/` com `#if`   |

## Convenção de namespaces

Use namespace **neutro** (sem `V2025`/`V2026`):

```csharp
namespace DarivaBIM.Plugin.Features.TigreCodes
namespace DarivaBIM.Plugin.Ribbon
namespace DarivaBIM.Plugin.Ui
```

Assim o mesmo arquivo compila identicamente em ambos os plugins, sem
divergência por namespace.

## Como o include funciona

No `Plugin.V20XX.csproj`:

```xml
<ItemGroup>
  <Compile Include="..\DarivaBIM.Plugin.SharedSource\**\*.cs"
           Exclude="..\DarivaBIM.Plugin.SharedSource\**\bin\**;..\DarivaBIM.Plugin.SharedSource\**\obj\**"
           Link="Shared\%(RecursiveDir)%(Filename)%(Extension)" />
  <Page    Include="..\DarivaBIM.Plugin.SharedSource\Ui\**\*.xaml"
           Link="Shared\Ui\%(RecursiveDir)%(Filename)%(Extension)" />
  <None    Include="..\DarivaBIM.Plugin.SharedSource\Resources\Icons\*.png"
           Link="Ribbon\Resources\Icons\%(Filename)%(Extension)">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Os ícones precisam ser `<None CopyToOutputDirectory>` (não `<Resource>`)
porque o `RibbonBuilder` os carrega como arquivo físico no disco.

## Diferenças entre 2025 e 2026

Quando aparecer divergência real de RevitAPI, isole com `#if`:

```csharp
#if REVIT2026
    long id = elementId.Value;
#else
    long id = elementId.Value;  // ou IntegerValue, conforme API real
#endif
```

Mantenha o trecho `#if` o menor possível. Se ficar grande, mova para
uma classe específica em `Plugin.V20XX/VersionSpecific/`.

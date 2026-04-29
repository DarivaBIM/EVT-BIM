# DarivaBIM.Revit.SharedSource

Pasta reservada para **arquivos-fonte compartilhados** entre adapters de
diferentes versões do Revit.

> Hoje está intencionalmente vazia. Materializar apenas quando houver
> duplicação real entre dois adapters. Ver `docs/adr/ADR-0008-revit-sharedsource-policy.md`.

## Como usar quando for materializar
Em cada `DarivaBIM.Revit.Adapters.V20XX.csproj` adicione:

```xml
<ItemGroup>
  <Compile Include="..\DarivaBIM.Revit.SharedSource\**\*.cs">
    <Link>SharedSource\%(RecursiveDir)%(Filename)%(Extension)</Link>
  </Compile>
</ItemGroup>
```

Ou crie um Shared Project (`DarivaBIM.Revit.SharedSource.shproj`) e
referencie via `<Import>`.

Não publicar como DLL.

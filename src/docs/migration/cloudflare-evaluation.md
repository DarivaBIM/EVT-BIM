# Avaliação de migração para Cloudflare — EVT-BIM (plugin Revit)

> **Data:** 2026-05-17
> **Documento mestre:** Ver `AcervoBIM/docs/migration/cloudflare-evaluation.md` para o plano completo do ecossistema.

---

## TL;DR

**EVT-BIM fica fora da migração para Cloudflare.**

É um plugin desktop nativo C# / .NET 8 / WPF que roda **dentro do Autodesk Revit 2025/2026**, distribuído via instalador `.exe` (Inno Setup) através de **GitHub Releases**. Não consome cloud em produção.

---

## 1. Estado atual

| Item | Atual | Cloudflare relevante? |
|------|-------|----------------------|
| Tipo | Plugin desktop, executa dentro do Revit | ❌ Não |
| Runtime | .NET 8 (`net8.0-windows`), WPF | ❌ Não |
| Distribuição | `.exe` via GitHub Releases | ❌ Não (GitHub já é grátis) |
| Catálogo Tigre | JSON embarcado no assembly | ❌ Não |
| API consumida | **Nenhuma** em produção. Pasta `Infrastructure/Api/` ainda stub | ❌ Não hoje |
| Telemetria | Pasta `Infrastructure/Telemetry/` vazia | ⚠️ Eventual (ver §3) |
| Licensing | Pasta `Infrastructure/Licensing/` vazia | ⚠️ Eventual (ver §3) |

**Conclusão:** Hoje o EVT-BIM **não toca infraestrutura cloud nenhuma** — nem AWS, nem Vercel. Logo, migrar para Cloudflare é não-aplicável.

---

## 2. O que NÃO muda

- ✅ Continuar com **GitHub Releases** para distribuir o instalador (já é grátis, mundial, CDN-edge no GitHub)
- ✅ Continuar com **Inno Setup** para gerar o `.exe`
- ✅ Continuar com **PowerShell `release.ps1`** para bump version + tag + release
- ✅ Manter `tigre_codes.json` embarcado no assembly
- ✅ Arquitetura Clean (Domain → Application → Adapters → Plugin) **não é afetada**

---

## 3. Quando a Cloudflare entra na vida do EVT-BIM (futuro)

Hoje as pastas `Infrastructure/Api/`, `Infrastructure/Licensing/`, e `Infrastructure/Telemetry/` existem mas estão vazias. Quando essas features forem implementadas (provavelmente perto do lançamento de **DarivaBIM V2**, que herda este código), elas vão precisar de backend cloud.

Recomendações para esse momento futuro:

### 3.1 Telemetria
- Apontar para um endpoint Cloudflare Worker (não Express, porque telemetria é fire-and-forget e Workers escala melhor)
- Sink em Cloudflare Workers Analytics Engine (free tier generoso)
- Custo: ~R$0/mês até dezenas de milhares de eventos/dia

### 3.2 Licensing / Premium check
- Endpoint para validar `isPremium` do usuário — pode reusar o backend Express atual em `darivabim.link/api`
- Ou criar Worker dedicado em `license.darivabim.com` para reduzir carga no Express
- Cache de resposta em local file (`%LOCALAPPDATA%`) com TTL para tolerar offline

### 3.3 Downloads de famílias / sync com AcervoBIM
- Se o plugin futuramente baixar famílias diretamente do AcervoBIM:
  - Usar URLs **R2 via `cdn.darivabim.com`** (já estará em produção pós-Fase 1B do plano mestre)
  - Zero egress = downloads gratuitos para a plataforma

### 3.4 Auto-update do plugin
- Hoje: usuário precisa reinstalar manualmente
- Futuro: hospedar `latest.json` (manifest com versão + URL do `.exe`) em **R2 + custom domain** `updates.darivabim.com`
- Custo: praticamente zero, latência baixa global

---

## 4. Resumo

| Aspecto | Recomendação |
|---------|-------------|
| Migrar EVT-BIM para Cloudflare agora? | **Não — não há nada cloud para migrar.** |
| Mudar distribuição? | **Não — manter GitHub Releases.** |
| Mudar build/release? | **Não — manter Inno Setup + `release.ps1`.** |
| Quando entra Cloudflare? | Quando `Infrastructure/Api`, `Telemetry`, `Licensing` forem implementadas. **Usar Workers + R2 nessa hora**, alinhado com o resto do ecossistema. |

Sem ação imediata neste repo. Este documento é apenas registro de avaliação para a branch `claude/evaluate-cloudflare-migration-fCT0Y`.

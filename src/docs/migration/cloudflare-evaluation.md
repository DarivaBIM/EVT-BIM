# EVT-BIM → DarivaBIM V2 — Integração com ecossistema Cloudflare

> **Atualizado:** 2026-05-17
> **Plano mestre:** `AcervoBIM/docs/migration/cloudflare-migration-plan.md`

---

## TL;DR

EVT-BIM continua sendo um plugin desktop C# / .NET 8 / WPF, distribuído via GitHub Releases. **Nada disso muda.**

O que muda: quando as pastas hoje vazias `Infrastructure/Api/`, `Infrastructure/Licensing/` e `Infrastructure/Telemetry/` forem implementadas, elas vão se conectar diretamente ao **ecossistema Cloudflare novo** (não ao backend Express legado).

Este repo é **a base do DarivaBIM V2** (sucessor do plugin DarivaBIM v1.9 antigo). Quando V2 estiver pronto para distribuição, EVT-BIM passa por rebranding (ADR-0014 já documenta isso) e vira o plugin oficial.

---

## Como o plugin se conecta ao ecossistema

```
DarivaBIM V2 plugin (este repo, rebranded)
              │
              ├─ api.darivabim.com  (Workers + Hono)
              │    - Licensing check (isPremium)
              │    - Telemetry events (fire-and-forget)
              │    - Sync de famílias com AcervoBIM
              │
              ├─ cdn.darivabim.com  (R2 público)
              │    - Download de famílias públicas
              │    - Templates compartilhados
              │
              ├─ files.darivabim.com  (R2 privado, presigned)
              │    - Download de famílias premium / privadas
              │
              └─ updates.darivabim.com  (R2 público)
                   - manifest.json com versão mais recente
                   - .exe / .msi do auto-update
```

**Nenhum dos endpoints fala com a stack AWS legada.**

---

## Roadmap de integração

### Quando implementar `Infrastructure/Api/`
- Cliente HTTP em C# (`HttpClient` ou `Refit`) apontando para `api.darivabim.com`
- Endpoints consumidos:
  - `POST /api/auth/login` (se plugin tiver login próprio)
  - `GET /api/me/profile`
  - `GET /api/families?owner=me` (listar famílias do usuário)
  - `GET /api/families/:id/file/:version` (presigned URL para download)
  - `POST /api/families` (upload de família via plugin — alternativa ao web)
- TLS 1.2+, cert pinning opcional

### Quando implementar `Infrastructure/Licensing/`
- Endpoint: `GET /api/licensing/check?userId=...`
- Resposta: `{ tier: 'free' | 'basic' | 'pro' | 'studio' | 'enterprise', expiresAt: ISO8601 }`
- Cache local em `%LOCALAPPDATA%\DarivaBIM\license.json` com TTL 24h (tolera offline)
- Renovação background quando online

### Quando implementar `Infrastructure/Telemetry/`
- Endpoint: `POST /api/telemetry/events` (Workers, fire-and-forget)
- Eventos: plugin loaded, tool invoked, error caught, family inserted
- Sink: Cloudflare Workers Analytics Engine (free, generoso)
- Sem PII: usar UUID anônimo persistido em registro

### Auto-update
- Manifest: `https://updates.darivabim.com/manifest.json`
  ```json
  {
    "latest": "2.1.0",
    "url": "https://updates.darivabim.com/DarivaBIM-V2-2.1.0.exe",
    "minRevit": "2025",
    "changelog": "..."
  }
  ```
- Plugin checa na inicialização (background, com timeout 3s)
- Se versão nova: mostra banner não-intrusivo
- Download em `%TEMP%`, executa instalador, fecha plugin

---

## Rebranding EVT-BIM → DarivaBIM V2 (ADR-0014)

Ver `src/docs/adr/ADR-0014-evt-bim-rebrand-darivabim-origins.md` — plano já documentado.

Resumo:
- Assemblies: `EVT-BIM` → `DarivaBIM` (namespace, AppId Inno Setup, install paths)
- Manifest `.addin`: nome visível "DarivaBIM 2026"
- Catálogo Tigre: continua embarcado (parceria Tigre é parte do appeal)
- README, docs, instalador: marca DarivaBIM

Timing: após backend novo estar estável e o ecossistema Cloudflare em produção. Provavelmente Q3 2026.

---

## Nada para fazer agora

Este repo não tem dependência da migração Cloudflare. Continua sendo desenvolvido normalmente conforme o roadmap do EVT-BIM:
- Mais ferramentas (Tools)
- Migração de modeless windows (V2026)
- Cobertura de testes
- Mais ADRs conforme necessário

A integração cloud entra quando essas pastas `Infrastructure/*` deixarem de ser stubs.

---

## Documentos relacionados

- Plano mestre do ecossistema: `AcervoBIM/docs/migration/cloudflare-migration-plan.md`
- Status do backend legado: `darivabim-backend/docs/migration/cloudflare-evaluation.md`
- ADR-0014 (rebranding): `src/docs/adr/ADR-0014-evt-bim-rebrand-darivabim-origins.md`
- ADRs gerais do projeto: `src/docs/adr/`

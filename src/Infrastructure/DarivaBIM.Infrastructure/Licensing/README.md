# Licensing

Future home of license validation, premium gating and subscription checks.

Antes da consolidacao da Infrastructure (ADR-0016) esta pasta era um projeto
separado (`DarivaBIM.Infrastructure.Licensing`) com apenas um `Placeholder.cs`.
Foi mantida como pasta reservada porque o tema de licenca/cobranca e
suficientemente isolado para ser desenvolvido em paralelo com Api e
Persistence.

`InfrastructureBoundariesTests` (em `tests/DarivaBIM.Architecture.Tests`)
garante que codigo aqui dentro nao dependa de Api, Persistence ou Telemetry.

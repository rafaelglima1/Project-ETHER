# Project Ether — Blueprints

Reference blueprints for Project Ether. These documents are the product and
technical contract for the project and must be preserved.

| Version | File | Role |
| --- | --- | --- |
| v1.0 | `PROJECT ETHER - Blueprint v1.0 - Visão do produto e game design.pdf` | Product vision and game design |
| v2.0 | `PROJECT ETHER - Blueprint v2.0 - Regras e arquitetura do jogo.pdf` | Game rules and architecture baseline |
| v3.0 | `PROJECT ETHER - Blueprint v3.0 - Contrato técnico de implementação.pdf` | Technical implementation contract |
| v4.0 | `PROJECT ETHER - Blueprint v4.0 - Implementation Pack.pdf` | Implementation pack (tasks, schemas) |
| v5.0 | `PROJECT ETHER - Blueprint v5.0 - Repository Bootstrap + Code-base.pdf` | Repository bootstrap and code base (current contract) |

## Precedence

When blueprints conflict, the most recent version wins:

```
v5.0 > v4.0 > v3.0 > v2.0 > v1.0
```

An older document must never override a decision made in a newer one.

If the current blueprint explicitly leaves a decision open, it stays as
`PRODUCT_DECISION_REQUIRED` — it must not be silently decided.

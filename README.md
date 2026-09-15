# CatalogAPI — Fase 3

Jogos, promoções, pedidos, biblioteca e avaliações. Usa SQLite para o fluxo transacional, MongoDB para avaliações flexíveis e Redis para reduzir consultas de avaliações.

- Administradores gerenciam jogos e promoções.
- Usuários autenticados compram e consultam seus pedidos/biblioteca.
- Compra usa o maior desconto ativo, arredondado a duas casas.
- Resultado aprovado/rejeitado é salvo em transação. Reentregas de pedidos finalizados são ignoradas.
- Uma avaliação por usuário/jogo, com nota, comentário, tags e data. Driver oficial MongoDB.Driver.
- Redis via IDistributedCache: primeira página por jogo, TTL absoluto 30s, invalidação após PUT e fallback quando indisponível.
- Header X-Cache: HIT, MISS ou BYPASS.
- Métricas prometheus-net e fcg_review_cache_total; logs JSON para Alloy/Loki.

## Configuração

ConnectionStrings__Db, Jwt__Key, Jwt__Issuer, Jwt__Audience e RabbitMq__Host/Username/Password.
Mongo__ConnectionString (padrão localhost:27017) e Mongo__Database (padrão fcg_catalog).
Redis__ConnectionString (padrão localhost:6379). O ambiente de orquestração fornece todos os valores.

Liveness em /health/live; /health/ready verifica SQLite. Não é uma checagem de todas as dependências. /metrics é interno.

## Executar e testar

Use o [guia central](../FIAP-CloudGames-Orchestration/README.md) no workspace; no GitHub, procure FIAP-CloudGames-Orchestration na mesma conta.

```powershell
dotnet test CatalogAPI.sln
```

Os testes cobrem persistência SQLite, desconto, resultados repetidos de pagamento e cache/fallback com doubles. A validação real de MongoDB/Redis é feita por Test-Flow.ps1 após subir os containers. Consulte os limites de consistência e mensageria no guia central.

# FIAP Cloud Games - CatalogAPI

Microsservico responsavel pelo catalogo de jogos, cadastro de promocoes, fluxo de compra e biblioteca de jogos dos usuarios.

Este servico faz parte do Tech Challenge Fase 2 da FIAP e representa a parte de catalogo e compra dentro da arquitetura de microsservicos.

## Responsabilidades

- Cadastrar jogos no catalogo.
- Listar jogos disponiveis.
- Registrar compras de jogos.
- Criar pedidos com status inicial `Pending`.
- Publicar evento de pedido criado.
- Consumir evento de pagamento processado.
- Atualizar o status do pedido.
- Liberar jogo na biblioteca do usuario quando o pagamento for aprovado.

## Tecnologias

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- JWT Bearer Authentication
- RabbitMQ
- MassTransit
- Swagger
- Docker

## Principais rotas

| Metodo | Rota | Descricao |
| --- | --- | --- |
| GET | `/api/games` | Lista jogos cadastrados |
| POST | `/api/games` | Cadastra um jogo, rota restrita a admin |
| POST | `/api/games/{id}/purchase` | Inicia a compra de um jogo pelo usuario autenticado |
| GET | `/api/library/me` | Lista jogos liberados na biblioteca do usuario autenticado |

## Exemplo de cadastro de jogo

```json
{
  "name": "Cyber FIAP 2077",
  "price": 199.90
}
```

## Fluxo de compra

1. Usuario autenticado chama `POST /api/games/{id}/purchase`.
2. CatalogAPI cria um pedido com status `Pending`.
3. CatalogAPI publica o evento `OrderPlacedEvent`.
4. PaymentsAPI consome o evento e simula o pagamento.
5. PaymentsAPI publica `PaymentProcessedEvent`.
6. CatalogAPI consome o retorno do pagamento.
7. Se aprovado, o jogo e adicionado a biblioteca do usuario.
8. Se rejeitado, o pedido permanece rejeitado e o jogo nao e liberado.

## Eventos

Evento publicado:

```text
OrderPlacedEvent
```

Evento consumido:

```text
PaymentProcessedEvent
```

## Variaveis de ambiente

| Variavel | Descricao | Exemplo |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Ambiente da aplicacao | `Development` |
| `ConnectionStrings__DefaultConnection` | String de conexao do SQLite | `Data Source=/data/catalog.db` |
| `Jwt__Key` | Chave usada para validar o token JWT | `fiap-cloud-games-secret-key` |
| `Jwt__Issuer` | Emissor esperado do token JWT | `FIAP.CloudGames` |
| `Jwt__Audience` | Audiencia esperada do token JWT | `FIAP.CloudGames` |
| `RabbitMq__Host` | Host do RabbitMQ | `rabbitmq` |
| `RabbitMq__Username` | Usuario do RabbitMQ | `guest` |
| `RabbitMq__Password` | Senha do RabbitMQ | `guest` |

## Executando localmente

```bash
dotnet restore
dotnet run --project src/CatalogAPI/CatalogAPI.csproj
```

Swagger:

```text
http://localhost:5002/swagger
```

## Executando com Docker

```bash
docker build -t fiap-catalog-api:latest .
docker run -p 5002:8080 fiap-catalog-api:latest
```

No projeto completo, a execucao recomendada e pelo repositorio de orquestracao, usando Docker Compose ou Kubernetes.


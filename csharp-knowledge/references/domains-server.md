# Серверные и облачные домены .NET

Справочник по серверной части .NET (актуально на октябрь 2026: .NET 10 — текущий LTS с поддержкой до ноября 2028, .NET 11 в стадии RC1 с go-live лицензией, релиз 10 ноября 2026). Для каждого домена — архитектурные и производительные правила, пакеты (в скобках — актуальная стабильная версия на nuget.org на 7 октября 2026) и проверенные ресурсы. Пометка `[устарело]` — подход или ресурс, который ещё встречается в статьях, но не годится для нового кода; `[не проверено]` — факт, который не удалось подтвердить первоисточником. Общие идиомы языка, async и тулчейн описаны в соседних файлах справочника.

- [1. Web API на ASP.NET Core](#1-web-api-на-aspnet-core)
- [2. Доступ к данным](#2-доступ-к-данным)
- [3. Распределённые системы и обмен сообщениями](#3-распределённые-системы-и-обмен-сообщениями)
- [4. Real-time и RPC](#4-real-time-и-rpc)
- [5. Web UI на сервере и в WebAssembly](#5-web-ui-на-сервере-и-в-webassembly)
- [6. Облако, serverless и контейнеры](#6-облако-serverless-и-контейнеры)
- [7. Фоновая обработка](#7-фоновая-обработка)
- [8. Наблюдаемость и устойчивость](#8-наблюдаемость-и-устойчивость)
- [9. Безопасность веб-приложений](#9-безопасность-веб-приложений)

---

## 1. Web API на ASP.NET Core

### Ключевые выводы
- **Minimal APIs или контроллеры.** Для новых сервисов по умолчанию берите minimal APIs: меньше накладных расходов, поддержка Native AOT через Request Delegate Generator, `TypedResults` дают типизированные ответы и сразу попадают в OpenAPI. Группируйте эндпоинты через `MapGroup` и выносите каждую группу в отдельный статический класс с методом-расширением — тогда `Program.cs` не превращается в свалку. Контроллеры (`[ApiController]`) остаются нормальным выбором для больших команд с привычной MVC-структурой, фильтрами и model binding; смешивать оба подхода в одном приложении можно.
- **Хендлер тонкий.** Схема: привязать данные → провалидировать → вызвать прикладной сервис → вернуть `TypedResults`. Доменный слой не должен знать про `HttpContext`. MediatR ради «чистоты» не обязателен: прямой вызов сервиса проще и быстрее. Если нужен медиатор, учитывайте, что MediatR с версии 13 коммерческий (см. ниже); бесплатная альтернатива на source generators — `Mediator` (martinothamar).
- **Валидация в .NET 10 встроена.** `builder.Services.AddValidation()` включает source-generated проверку DataAnnotations для параметров, record-типов и вложенных объектов в minimal APIs; при ошибке возвращается 400 с `ProblemDetails` и словарём `errors`, отключить для эндпоинта — `.DisableValidation()`. В .NET 11 добавлена асинхронная валидация (`AsyncValidationAttribute`, `IAsyncValidatableObject`), атрибуты `[ValidatableType]`/`[SkipValidation]` перестали быть экспериментальными. FluentValidation по-прежнему уместен для сложных правил, но подключается вручную (через endpoint filter).
- **Ошибки — только ProblemDetails (RFC 9457).** `builder.Services.AddProblemDetails()` + `app.UseExceptionHandler()` + свой `IExceptionHandler` для сопоставления доменных исключений со статус-кодами. Ожидаемые ошибки (не найдено, конфликт) выражайте результатом (`Results<Ok<T>, NotFound, ValidationProblem>`), а не исключениями: исключения в .NET дорогие. С .NET 10 исключения, обработанные `IExceptionHandler` (вернувшим `true`), по умолчанию не логируются — проверьте, что логирование делаете сами.
- **OpenAPI встроен.** `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()` + `MapOpenApi()`) генерирует документ на этапе выполнения или сборки; в .NET 10 по умолчанию OpenAPI 3.1, есть YAML (`MapOpenApi("/openapi/{documentName}.yaml")`) и XML-комментарии через source generator (только для методов, не для лямбд). В .NET 11 — OpenAPI 3.2. UI — Scalar (`app.MapScalarApiReference()`) или Swagger UI. Swashbuckle убран из шаблона в .NET 9, но сам проект жив (10.x) — менять работающую интеграцию не обязательно. При апгрейде трансформеров учтите breaking changes Microsoft.OpenApi 2.x (`OpenApiAny` → `JsonNode`).
- **Порядок middleware важен:** `UseExceptionHandler` → `UseHsts`/`UseHttpsRedirection` → `UseRouting` (неявно) → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → `UseOutputCache` → эндпоинты. Ошибки в порядке — частая причина «CORS не работает» и «кеш отдаёт чужие данные».
- **Аутентификация.** Для API — JWT Bearer от внешнего провайдера (Entra ID, Keycloak, Auth0, Duende/OpenIddict); валидируйте `issuer`, `audience`, срок жизни; не выпускайте токены вручную в контроллере. ASP.NET Core Identity + `MapIdentityApi()` подходит для простого собственного логина SPA/мобильного клиента, но это не OAuth-сервер. Для своего OIDC-сервера: Duende IdentityServer (коммерческий, бесплатен для разработки и тестов; бесплатная [Community Edition](https://duendesoftware.com/products/communityedition) — при выручке до $1 млн и привлечённом капитале до $3 млн или для некоммерческих организаций с бюджетом до $1 млн) или OpenIddict (Apache-2.0); Keycloak — отдельный сервер, интеграция через `Keycloak.AuthServices`. IdentityServer4 `[устарело]` — не поддерживается с 2022. В .NET 10 cookie-аутентификация для API-эндпоинтов возвращает 401/403 вместо редиректа на логин; добавлены passkeys (WebAuthn) в Identity.
- **Rate limiting и кеш вывода — встроенные.** `AddRateLimiter` (fixed/sliding window, token bucket, concurrency; партиционируйте по пользователю или IP, отдавайте 429 с `Retry-After`). Output caching (`AddOutputCache`) — серверный кеш ответа с тегами и инвалидацией, в отличие от response caching (только HTTP-заголовки). Для кеша данных — `HybridCache` (раздел 2).
- **Health checks:** разделяйте `liveness` (процесс жив, без внешних зависимостей) и `readiness` (БД, брокер доступны) — разные эндпоинты с фильтром по тегам. Иначе упавшая БД перезапустит все поды. Готовые проверки — пакеты `AspNetCore.HealthChecks.*` (Xabaril).
- **Версионирование API** — `Asp.Versioning.Http`/`.Mvc` (версия в URL или заголовке), интегрируется с OpenAPI. Не ломайте контракт без новой версии.
- **Производительность:** JSON — `System.Text.Json` с source generation (`JsonSerializerContext`), обязателен для Native AOT; в .NET 10 десериализация читает напрямую из `PipeReader`. Никакого синхронного I/O и `.Result`/`.Wait()` в запросе — это голодание пула потоков. Передавайте `CancellationToken` (`HttpContext.RequestAborted`) до БД. Для Native AOT используйте `WebApplication.CreateSlimBuilder` и проверяйте предупреждения тримминга.
- **Конфигурация — через Options pattern:** `services.AddOptions<T>().BindConfiguration("Section").ValidateDataAnnotations().ValidateOnStart()` — приложение падает при старте, а не на первом запросе. `IOptionsMonitor<T>` — только там, где реально нужна перезагрузка на лету; в singleton-сервисах не используйте `IOptionsSnapshot<T>` (он scoped).
- **DI-время жизни:** не захватывайте scoped-сервисы (DbContext) в singleton; включите `ValidateScopes`/`ValidateOnBuild` (в Development включены по умолчанию). `DbContext` не потокобезопасен — никаких параллельных запросов через один контекст (`Task.WhenAll` по одному `DbContext` — ошибка).
- **Kestrel-лимиты** (`MaxRequestBodySize`, `MaxConcurrentConnections`, таймауты заголовков) задавайте явно для публичных сервисов; HTTP/2 и HTTP/3 включаются в `ListenOptions.Protocols`.
- **Интеграционные тесты:** `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`) + реальная БД в контейнере (Testcontainers), а не in-memory провайдер EF.
- **Локализация и культура:** тексты — `IStringLocalizer<T>` + `.resx` (`AddLocalization()`), культура запроса — `UseRequestLocalization` с явным списком `SupportedCultures`. Машинные форматы (JSON, CSV, логи, ключи кеша) парсите и форматируйте с `CultureInfo.InvariantCulture`: `decimal.Parse("1,5")` на сервере с другой культурой даст другой результат. Время храните в UTC (`DateTimeOffset`), часовые пояса — по IANA-идентификаторам; для сложной работы с датами есть NodaTime.

### Пакеты
- `Microsoft.AspNetCore.App` (10.0.12, часть SDK) — сам фреймворк; .NET 11 RC1 — `11.0.0-rc.1`.
- `Microsoft.AspNetCore.OpenApi` (10.0.12) — генерация OpenAPI; `Scalar.AspNetCore` (2.17) — UI; `Swashbuckle.AspNetCore` (10.3) — альтернатива.
- `Microsoft.Extensions.Validation` (10.0.12) — встроенная валидация; `FluentValidation` (12.1) — правила в коде.
- `Asp.Versioning.Http` (10.2) / `Asp.Versioning.Mvc` (10.2) — версионирование.
- `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.12), `Microsoft.Identity.Web` (4.16) — JWT и Entra ID.
- `Duende.IdentityServer` (8.0, коммерческий), `OpenIddict.*` (7.7; 8.0 в preview), `Keycloak.AuthServices.Authentication` (3.0) — OIDC.
- `AspNetCore.HealthChecks.*` (9.0) — готовые health checks (Xabaril; релизы реже, чем у фреймворка).
- `FastEndpoints` (8.3) — REPR-фреймворк поверх minimal APIs.
- `MediatR` (14.2, коммерческая лицензия), `Mediator.SourceGenerator` (3.0, MIT) — медиаторы.
- `Microsoft.AspNetCore.Mvc.Testing` (10.0.12), `Testcontainers.PostgreSql` (4.15) — тесты.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [What's new in ASP.NET Core in .NET 10](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0) | Microsoft, 2025 | Валидация, SSE, OpenAPI 3.1, passkeys, breaking changes cookie-аутентификации |
| [What's new in ASP.NET Core in .NET 11](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11) | Microsoft, 2026 (RC) | Async-валидация, авто-CSRF, OpenAPI 3.2, union-типы |
| [Choose between controller-based and minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis) | Microsoft, живой | Официальное сравнение подходов |
| [Minimal APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis) | Microsoft, живой | Группы, фильтры, binding; [ответы и TypedResults](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses) |
| [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/) | Microsoft, живой | Порядок конвейера с диаграммой |
| [Handle errors in APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api) | Microsoft, живой | ProblemDetails, `IExceptionHandler` |
| [OpenAPI overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview) | Microsoft, живой | Генерация документа; [Microsoft.AspNetCore.OpenApi](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi), [UI и клиенты](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/using-openapi-documents) |
| [OpenAPI document generation in .NET 9](https://devblogs.microsoft.com/dotnet/dotnet9-openapi/) | .NET Blog, 2024 | Почему ушли от Swashbuckle по умолчанию; [обсуждение](https://github.com/dotnet/aspnetcore/issues/54599) |
| [Scalar](https://github.com/scalar/scalar) | Scalar, живой | UI для OpenAPI-документа |
| [ASP.NET 10: Validating incoming models in Minimal APIs](https://timdeschryver.dev/blog/aspnet-10-validating-incoming-models-in-minimal-apis) | Tim Deschryver, 2025 | Практический разбор встроенной валидации |
| [Rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit) | Microsoft, живой | Алгоритмы и партиционирование |
| [Output caching middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output) | Microsoft, живой | Политики, теги, инвалидация |
| [Health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) | Microsoft, живой | Liveness/readiness; [готовые проверки Xabaril](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks) |
| [API Versioning](https://github.com/dotnet/aspnet-api-versioning) | .NET Foundation, живой | Схемы версионирования и интеграция с OpenAPI |
| [Configure JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication) | Microsoft, живой | Валидация токенов; [обзор аутентификации](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/), [авторизации](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction) |
| [Identity API endpoints for SPAs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization) | Microsoft, живой | `MapIdentityApi` и его ограничения; [Identity в целом](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity) |
| [Duende IdentityServer](https://duendesoftware.com/products/identityserver) | Duende Software, живой | Лицензирование и редакции; [исходники](https://github.com/DuendeSoftware/products) |
| [OpenIddict documentation](https://documentation.openiddict.com/) | Kévin Chalet, живой | Бесплатный OIDC-сервер; [репозиторий](https://github.com/openiddict/openiddict-core) |
| [Keycloak](https://www.keycloak.org/) | CNCF, живой | Самостоятельный IdP |
| [Native AOT support](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot) | Microsoft, живой | Что работает в AOT; [Request Delegate Generator](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/aot/request-delegate-generator/rdg) |
| [Integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) | Microsoft, живой | `WebApplicationFactory`; [Testcontainers for .NET](https://dotnet.testcontainers.org/) |
| [Globalization and localization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization?view=aspnetcore-10.0) | Microsoft, живой | `IStringLocalizer`, ресурсы, выбор культуры запроса; [общие принципы в .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-and-localization) |
| [NodaTime](https://nodatime.org/) | Jon Skeet и др., живой | Даты, время и часовые пояса без ловушек `DateTime` (3.3) |
| [FastEndpoints](https://fast-endpoints.com/) | FastEndpoints, живой | REPR-паттерн как альтернатива контроллерам |
| [ASP.NET Core in Action, 3e](https://www.manning.com/books/asp-net-core-in-action-third-edition) | Andrew Lock, 2023 | Лучшая книга по фреймворку (база .NET 7; новые фичи 8–11 — в блоге автора) |
| [.NET Escapades](https://andrewlock.net/) | Andrew Lock, живой | Глубокие разборы каждой версии ASP.NET Core |
| [Milan Jovanović — blog](https://www.milanjovanovic.tech/blog) | Milan Jovanović, живой | Короткие практические статьи по API, EF Core, архитектуре |

---

## 2. Доступ к данным

### Ключевые выводы
- **EF Core 10 — выбор по умолчанию** для CRUD и доменной модели; это LTS до 10 ноября 2028, требует .NET 10. Dapper — для сложных отчётных запросов и горячих путей, где нужен ручной SQL; они отлично сосуществуют на одном `DbConnection`. linq2db — типобезопасный LINQ-to-SQL без change tracker, ближе к SQL, чем EF.
- **Чтение без трекинга.** Для запросов только на чтение — `AsNoTracking()` (или `QueryTrackingBehavior.NoTracking` в контексте чтения) и проекция `Select` в DTO вместо загрузки целых сущностей. Это главный выигрыш производительности, обычно больший, чем замена EF на Dapper.
- **N+1 и картезианский взрыв.** Ленивую загрузку не включайте. Связанные данные — `Include` или проекция; при нескольких коллекционных `Include` используйте `AsSplitQuery()` (в EF 10 исправлен порядок сортировки между частями split-запроса). Всегда ставьте `OrderBy` перед `Skip/Take`. Логируйте SQL в разработке и смотрите план.
- **Массовые операции** — `ExecuteUpdateAsync`/`ExecuteDeleteAsync` вместо загрузки и `SaveChanges` в цикле; в EF 10 `ExecuteUpdateAsync` принимает обычную лямбду (условные `SetProperty` без деревьев выражений) и умеет обновлять JSON-колонки. Для вставки сотен тысяч строк — `COPY` в Npgsql (`BeginBinaryImport`) или `SqlBulkCopy`.
- **Новое в EF 10:** complex types с маппингом в JSON и как struct (рекомендуются вместо owned types), нативные типы `json` и `vector` SQL Server 2025 (`EF.Functions.VectorDistance`), операторы `LeftJoin`/`RightJoin`, именованные глобальные фильтры (`HasQueryFilter("SoftDelete", ...)` + `IgnoreQueryFilters(["SoftDelete"])`). Коллекции-параметры (`ids.Contains(x.Id)`) теперь транслируются в отдельные параметры с «добивкой» (padding) — меньше разных планов; режим настраивается `UseParameterizedCollectionMode`. Внимание: при переходе на compat level 170 следующая миграция переведёт JSON-колонки `nvarchar` в `json`.
- **Пулинг и компиляция.** `AddDbContextPool` снижает аллокации на запрос (контекст не должен хранить состояние). Компилированные запросы (`EF.CompileAsyncQuery`) — для самых частых запросов в горячем пути. Компилированная модель (`dotnet ef dbcontext optimize`) ускоряет старт при сотнях сущностей и нужна для NativeAOT-сценариев; пересобирайте её при изменении модели.
- **Миграции в проде** не применяйте через `Database.Migrate()` при старте нескольких реплик — гонка. Используйте SQL-скрипты (`dotnet ef migrations script --idempotent`), migration bundles или отдельный job/init-контейнер. В EF 10 миграции снова не оборачиваются в одну общую транзакцию.
- **Интерсепторы** (`SaveChangesInterceptor`, `DbCommandInterceptor`) — для аудита, soft delete, доменных событий и outbox; не делайте в них тяжёлых синхронных операций.
- **PostgreSQL:** Npgsql + `Npgsql.EntityFrameworkCore.PostgreSQL`; регистрируйте `NpgsqlDataSource` (`AddNpgsqlDataSource`), а не строки подключения по месту. `EFCore.NamingConventions` — snake_case имена. Marten превращает PostgreSQL в документную БД и event store (MIT, JasperFx).
- **NoSQL:** MongoDB — официальный драйвер 3.x и провайдер `MongoDB.EntityFrameworkCore`; Cosmos DB — SDK v3 с одним singleton `CosmosClient` на приложение, режим Direct, ключ партиции в каждом запросе (EF-провайдер Cosmos удобен, но скрывает стоимость RU). Redis — `StackExchange.Redis`, один `ConnectionMultiplexer` на процесс.
- **Кеш данных — HybridCache** (`Microsoft.Extensions.Caching.Hybrid`, GA с .NET 9): L1 в памяти + L2 через `IDistributedCache` (Redis/SQL Server/Postgres), защита от cache stampede, теги (`RemoveByTagAsync`). Ограничения: защита от stampede работает только внутри одного экземпляра; инвалидация не очищает L1 на других серверах — держите короткий `LocalCacheExpiration` или берите FusionCache с backplane. Не используйте сырой ввод пользователя в ключах. Неизменяемые типы помечайте `sealed` + `[ImmutableObject(true)]`, чтобы избежать повторной десериализации.
- **Конкурентность:** оптимистическая блокировка через concurrency token (`[Timestamp]`/`rowversion` в SQL Server, `xmin` в PostgreSQL) и обработка `DbUpdateConcurrencyException`; явные транзакции — `BeginTransactionAsync` только когда одного `SaveChanges` мало. При `EnableRetryOnFailure` пользовательские транзакции оборачивайте в `CreateExecutionStrategy().ExecuteAsync(...)`.
- **Пул соединений** — общий ресурс: открывайте соединение как можно позже и закрывайте сразу (`await using`); не держите `DbContext` дольше запроса. Размер пула и `MaxPoolSize` согласуйте с лимитом соединений БД с учётом числа реплик (или используйте PgBouncer).
- **Тесты БД** — на настоящей СУБД в Testcontainers; провайдер EF InMemory не проверяет SQL, ограничения и транзакции.
- **Деньги и точные величины:** только `decimal` (десятичная арифметика, 28–29 значащих цифр), никогда `double`/`float` — двоичная плавающая точка не представляет 0.1 точно. В EF задавайте точность явно (`HasPrecision(19, 4)` или `[Precision]`), иначе действует умолчание провайдера: SQL Server-провайдер предупреждает, что не влезающие в точность по умолчанию значения будут молча обрезаны. `Math.Round` по умолчанию округляет банковским способом (`MidpointRounding.ToEven`): правило округления фиксируйте явно и по требованиям бизнеса. Сумму храните вместе с валютой (value object), а не голым числом.

### Пакеты
- `Microsoft.EntityFrameworkCore` и провайдеры `.SqlServer`/`.Sqlite`/`.Cosmos` (10.0.12; 11.0 в RC) — ORM.
- `Npgsql` (10.0), `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0), `Npgsql.DependencyInjection` (10.0) — PostgreSQL.
- `Microsoft.Data.SqlClient` (7.1) — SQL Server.
- `Dapper` (2.1) — micro-ORM.
- `linq2db` (6.5) — LINQ-слой без трекинга.
- `Marten` (9.x, релизы каждые несколько дней) — документная БД и event sourcing на PostgreSQL.
- `MongoDB.Driver` (3.12), `MongoDB.EntityFrameworkCore` (10.0) — MongoDB.
- `Microsoft.Azure.Cosmos` (3.63) — Cosmos DB SDK.
- `StackExchange.Redis` (3.3), `Microsoft.Extensions.Caching.StackExchangeRedis` (10.0.12) — Redis.
- `Microsoft.Extensions.Caching.Hybrid` (10.10), `ZiggyCreatures.FusionCache` (2.9) — гибридный кеш.
- `EFCore.NamingConventions` (10.0) — соглашения об именах; `EFCore.BulkExtensions` (10.0) — массовые операции; двойная лицензия: бесплатно при выручке до $1 млн, для некоммерческих и OSS-проектов, иначе коммерческая.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [What's New in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew) | Microsoft, 2025 | Complex types, JSON/vector, named filters, коллекции-параметры |
| [EF Core performance](https://learn.microsoft.com/en-us/ef/core/performance/) | Microsoft, живой | Диагностика и [эффективные запросы](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) |
| [Advanced performance topics](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics) | Microsoft, живой | Пулинг контекстов, компилированные запросы и модели |
| [Single vs. split queries](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries) | Microsoft, живой | Картезианский взрыв и когда делить запрос |
| [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete) | Microsoft, живой | Массовые изменения без трекинга |
| [Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) | Microsoft, живой | Скрипты, bundles, почему не `Migrate()` в проде |
| [Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors) | Microsoft, живой | Аудит и перехват команд; [пример у Milan](https://www.milanjovanovic.tech/blog/how-to-use-ef-core-interceptors) |
| [Global query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters) | Microsoft, живой | Soft delete, multitenancy, именованные фильтры |
| [Complex types](https://learn.microsoft.com/en-us/ef/core/modeling/complex-types) | Microsoft, живой | Замена owned types |
| [Entity properties](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties) | Microsoft, живой | Типы колонок, precision и scale для `decimal` |
| [Floating-point numeric types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types) | Microsoft, живой | Чем `decimal` отличается от `double` и когда что брать |
| [Entity Framework Core in Action, 2e](https://www.manning.com/books/entity-framework-core-in-action-second-edition) | Jon P Smith, 2021 | Архитектура и производительность EF; база EF Core 5/6 — новые API смотрите в документации |
| [Dapper](https://github.com/DapperLib/Dapper) | Marc Gravell и др., живой | README — вся документация |
| [Npgsql documentation](https://www.npgsql.org/doc/) | Npgsql, живой | Пулы, типы, [производительность](https://www.npgsql.org/doc/performance.html), [EF-провайдер](https://www.npgsql.org/efcore/) |
| [Marten](https://martendb.io/) | JasperFx, живой | Документы и event store на PostgreSQL |
| [linq2db](https://linq2db.github.io/) | linq2db, живой | LINQ ближе к SQL |
| [MongoDB C# driver](https://www.mongodb.com/docs/drivers/csharp/current/) | MongoDB, живой | Официальный драйвер |
| [Cosmos DB .NET SDK best practices](https://learn.microsoft.com/en-us/azure/cosmos-db/best-practice-dotnet) | Microsoft, живой | Singleton-клиент, Direct mode, RU; [EF-провайдер Cosmos](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/) |
| [StackExchange.Redis](https://seredis.dev/) | StackExchange, живой | Мультиплексор, pipelining, типичные ошибки |
| [HybridCache library](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) | Microsoft, живой | L1/L2, теги, ограничения инвалидации; [анонс GA](https://devblogs.microsoft.com/dotnet/hybrid-cache-is-now-ga/) |
| [FusionCache](https://github.com/ZiggyCreatures/FusionCache) | Jody Donetti, живой | Backplane, fail-safe, может служить реализацией HybridCache |

---

## 3. Распределённые системы и обмен сообщениями

### Ключевые выводы
- **Не начинайте с микросервисов.** Модульный монолит с чёткими границами модулей и обменом через интерфейсы или in-process сообщения проще в эксплуатации; разделяйте по сервисам, когда есть независимое масштабирование или независимые команды.
- **Aspire** (с версии 13, ноябрь 2025, называется просто «Aspire», без «.NET»; документация переехала на aspire.dev, репозиторий — `microsoft/aspire`). AppHost описывает граф сервисов и инфраструктуры кодом на C#, поднимает контейнеры локально, раздаёт строки подключения и включает OpenTelemetry-дашборд; ServiceDefaults задают единые health checks, телеметрию, устойчивость и service discovery. С 13-й версии на равных поддерживаются Python и JavaScript. Aspire — инструмент разработки и описания приложения; для продакшна он генерирует артефакты деплоя (Azure Container Apps, Kubernetes, Docker Compose), а не заменяет оркестратор. Старая документация на learn.microsoft.com/dotnet/aspire `[устарело]` — теперь редирект.
- **Выбор шины сообщений с учётом лицензий:**
  - MassTransit v9 — коммерческий (компания Massient, автор Chris Patterson); v8 остаётся открытым (Apache-2.0), последняя версия 8.5.11, поддержка — до конца 2026 года. Новые проекты на v8 — технический долг; либо покупайте v9, либо выбирайте другую библиотеку.
  - Wolverine (JasperFx, MIT) — медиатор и шина в одном, встроенный transactional outbox/inbox (EF Core, Marten, PostgreSQL, SQL Server), source-generated хендлеры, саги; на NuGet пакет называется `WolverineFx`.
  - NServiceBus (Particular) — зрелый коммерческий стек с ServicePulse/ServiceInsight, долгая поддержка.
  - Rebus, Brighter, CAP — открытые альтернативы (все MIT); CAP особенно прост для outbox + шины поверх EF/Dapper.
  - Без фреймворка — прямые клиенты: `Confluent.Kafka`, `RabbitMQ.Client` 7.x (полностью асинхронный API, несовместим с 6.x), `Azure.Messaging.ServiceBus`.
- **Transactional outbox обязателен,** если вы пишете в БД и публикуете событие: запись события в ту же транзакцию, отдельный диспетчер публикует его. Иначе при сбое между `SaveChanges` и `Publish` теряется событие или появляется «призрачное». На стороне получателя — идемпотентность (inbox/таблица обработанных ID): доставка у всех брокеров «как минимум один раз».
- **Саги/процессы:** для долгих бизнес-процессов с компенсациями — саги в Wolverine/NServiceBus/MassTransit или durable workflow (Durable Functions, Dapr Workflow, Temporal). Распределённые транзакции (2PC) не используйте.
- **Kafka:** один `IProducer` на приложение (thread-safe), идемпотентный продюсер (`EnableIdempotence = true`), ключ сообщения определяет партицию и порядок; потребитель — фиксируйте offset после обработки, обработку выносите из цикла `Consume` осторожно, чтобы не нарушить порядок.
- **Orleans** (10.x, MIT) — виртуальные акторы (grains) для stateful-сервисов с высокой конкуренцией (сессии, игры, IoT, корзины); не нужен для обычного CRUD. **Dapr** — sidecar с API для state, pub/sub, workflow, секретов; полезен в polyglot-средах, но добавляет инфраструктурный слой.
- **Контракты сообщений** — отдельная сборка/пакет с record-типами или схемы (Protobuf/Avro/JSON Schema с реестром для Kafka); версионируйте аддитивно (новые поля необязательные), не переименовывайте типы — многие шины маршрутизируют по полному имени типа.
- **Обработка ошибок в потребителе:** ограниченные повторы с задержкой → dead-letter очередь → алерт; не делайте бесконечных мгновенных ретраев, они превращают одну «ядовитую» запись в остановку всей очереди. Correlation/causation ID передавайте в заголовках и связывайте с трассировкой OpenTelemetry.
- **Event sourcing** — Marten или KurrentDB (бывший EventStoreDB); используйте точечно для агрегатов с богатой историей, а не для всей системы.

### Пакеты
- `Aspire.Hosting.AppHost` / `Aspire.Hosting` (13.6), клиентские интеграции `Aspire.*` (13.6), `CommunityToolkit.Aspire.*` (13.6) — оркестрация разработки.
- `Microsoft.Extensions.ServiceDiscovery` (10.10) — service discovery.
- `MassTransit` (9.2, коммерческий; 8.5.11 — последний открытый).
- `WolverineFx` (6.x, MIT), `WolverineFx.Http`, `WolverineFx.Marten`.
- `NServiceBus` (10.2, коммерческий), `Rebus` (8.9), `Paramore.Brighter` (10.8), `DotNetCore.CAP` (10.0).
- `Confluent.Kafka` (2.16), `RabbitMQ.Client` (7.2), `Azure.Messaging.ServiceBus` (7.21).
- `Microsoft.Orleans.Server` / `.Client` / `.Sdk` (10.4) — акторы.
- `Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Workflow` (1.18; 1.19 в RC).
- `Elsa` (3.9) — workflow-движок; `Akka` (1.5) — акторы в стиле Akka.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Aspire](https://aspire.dev/) | Microsoft, живой | Документация и быстрый старт; [что нового в 13](https://aspire.dev/whats-new/aspire-13/), [релизы](https://github.com/microsoft/aspire/releases) |
| [Aspire repo](https://github.com/microsoft/aspire) | Microsoft, живой | Исходники и issues; [Aspire samples](https://github.com/microsoft/aspire-samples) |
| [eShop reference app](https://github.com/dotnet/eShop) | Microsoft, живой | Эталонное приложение на Aspire: сервисы, события, outbox, gRPC, Identity |
| [.NET Microservices: Architecture for Containerized .NET Apps](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/) | Microsoft, 2017–2023 | Концепции DDD, CQRS, событий верны; примеры кода `[устарело]` (eShopOnContainers) |
| [Cloud design patterns](https://learn.microsoft.com/en-us/azure/architecture/patterns/) | Microsoft, живой | Каталог паттернов: outbox, saga, CQRS, retry, bulkhead |
| [MediatR and MassTransit going commercial](https://www.milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you) | Milan Jovanović, 2025 | Что изменилось и какие варианты миграции |
| [MassTransit (Massient)](https://masstransit.massient.com/) | Massient, живой | Документация v9 и лицензии; [репозиторий](https://github.com/MassTransit/MassTransit) |
| [Wolverine](https://wolverinefx.net/) | Jeremy D. Miller / JasperFx, живой | Шина + outbox + саги; [репозиторий](https://github.com/JasperFx/wolverine), [блог автора](https://jeremydmiller.com/) |
| [NServiceBus](https://particular.net/nservicebus) | Particular Software, живой | Коммерческая шина, обучающие материалы по распределённым системам |
| [Rebus](https://github.com/rebus-org/Rebus) | Rebus, живой | Лёгкая открытая шина |
| [Brighter](https://brightercommand.gitbook.io/paramore-brighter-documentation) | Brighter, живой | Command processor + outbox |
| [CAP](https://cap.dotnetcore.xyz/) | NCC, живой | Простое решение outbox + event bus |
| [Implementing the Outbox Pattern](https://www.milanjovanovic.tech/blog/implementing-the-outbox-pattern) | Milan Jovanović, 2024 | Ручная реализация шаг за шагом |
| [Outbox, Inbox patterns and delivery guarantees explained](https://event-driven.io/en/outbox_inbox_patterns_and_delivery_guarantees_explained/) | Oskar Dudycz, 2022 | Почему «exactly once» — миф и как жить с at-least-once |
| [EventSourcing.NetCore](https://github.com/oskardudycz/EventSourcing.NetCore) | Oskar Dudycz, живой | Примеры event sourcing на Marten и KurrentDB; [блог](https://event-driven.io/en/) |
| [Life beyond distributed transactions](https://www.jimmybogard.com/life-beyond-distributed-transactions-an-apostates-implementation-relational-resources/) | Jimmy Bogard, 2018 | Серия о согласованности без 2PC |
| [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) | Confluent, живой | Клиент Kafka и примеры |
| [RabbitMQ .NET API guide](https://www.rabbitmq.com/client-libraries/dotnet-api-guide) | RabbitMQ, живой | API 7.x, подтверждения, каналы |
| [Orleans overview](https://learn.microsoft.com/en-us/dotnet/orleans/overview) | Microsoft, живой | Модель виртуальных акторов; [репозиторий](https://github.com/dotnet/orleans) |
| [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/) | Dapr, живой | Sidecar-подход; [сайт проекта](https://dapr.io/) |
| [KurrentDB](https://github.com/kurrent-io/KurrentDB) | Kurrent, живой | Специализированная event store БД |

---

## 4. Real-time и RPC

### Ключевые выводы
- **SignalR** — для push-уведомлений, чатов, дашбордов. Хабы без состояния, строго типизированные (`Hub<IClient>`); не держите долгие операции в методах хаба. Масштабирование на несколько серверов — Redis backplane или Azure SignalR Service, плюс sticky sessions (кроме случая «только WebSockets без negotiate»). Аутентификация WebSocket — токен в query string (`access_token`), поэтому не логируйте URL целиком. В .NET 11 добавлены обновление аутентификации без переподключения и отмена непотоковых вызовов через `CancellationToken`.
- **Server-Sent Events** — встроенный `TypedResults.ServerSentEvents` с .NET 10; для однонаправленного потока сервер → клиент проще SignalR и работает через обычный HTTP.
- **gRPC (grpc-dotnet)** — для межсервисного взаимодействия: контракт `.proto`, HTTP/2, бинарный Protobuf. Каналы (`GrpcChannel`) дорогие — переиспользуйте или регистрируйте клиента через `AddGrpcClient`. Ставьте deadline на каждый вызов. Для браузеров — gRPC-Web или JSON transcoding (`Microsoft.AspNetCore.Grpc.JsonTranscoding`), который публикует те же сервисы как REST. `Grpc.Net.ClientFactory` до 2.63 несовместим со стандартным resilience handler — используйте актуальные версии.
- **Сырые WebSockets** (`app.UseWebSockets()`) — только если нужен собственный протокол; иначе SignalR даёт переподключение, группы и fallback.
- **GraphQL — Hot Chocolate** (16.x, MIT): DataLoader обязателен против N+1; интеграция с EF через проекции (`[UseProjection]`, `[UseFiltering]`), ограничивайте глубину и сложность запросов, persisted operations для публичных API. Не выбирайте GraphQL «по умолчанию» для внутренних API — REST/gRPC проще кешировать и наблюдать.
- **JSON-RPC** (StreamJsonRpc) — для инструментов и межпроцессного взаимодействия (LSP-подобные протоколы).

### Пакеты
- `Microsoft.AspNetCore.SignalR.Client` (10.0.12), `Microsoft.AspNetCore.SignalR.StackExchangeRedis` — клиент и backplane.
- `Grpc.AspNetCore`, `Grpc.Net.Client`, `Grpc.Tools` (2.84) — gRPC.
- `Microsoft.AspNetCore.Grpc.JsonTranscoding` (10.0.12) — REST поверх gRPC.
- `HotChocolate.AspNetCore` (16.6) — GraphQL-сервер.
- `StreamJsonRpc` (2.25) — JSON-RPC.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Overview of ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction) | Microsoft, живой | Хабы, транспорты; [хабы подробно](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs) |
| [SignalR hosting and scaling](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale) | Microsoft, живой | Backplane, sticky sessions, Azure SignalR |
| [gRPC on .NET](https://learn.microsoft.com/en-us/aspnet/core/grpc/) | Microsoft, живой | Сервисы, [сервер](https://learn.microsoft.com/en-us/aspnet/core/grpc/aspnetcore), [клиент](https://learn.microsoft.com/en-us/aspnet/core/grpc/client) |
| [Performance best practices with gRPC](https://learn.microsoft.com/en-us/aspnet/core/grpc/performance) | Microsoft, живой | Переиспользование каналов, keep-alive, балансировка |
| [gRPC JSON transcoding](https://learn.microsoft.com/en-us/aspnet/core/grpc/json-transcoding) | Microsoft, живой | Один контракт для gRPC и REST |
| [grpc-dotnet](https://github.com/grpc/grpc-dotnet) | gRPC Authors, живой | Исходники и примеры |
| [WebSockets support](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets) | Microsoft, живой | Низкоуровневый WebSocket |
| [Hot Chocolate](https://chillicream.com/docs/hotchocolate) | ChilliCream, живой | GraphQL-сервер, DataLoader, проекции; [репозиторий](https://github.com/ChilliCream/graphql-platform) |
| [HTTP/3 with Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/http3) | Microsoft, живой | QUIC-транспорт и требования к ОС |

---

## 5. Web UI на сервере и в WebAssembly

### Ключевые выводы
- **Blazor Web App и режимы рендеринга.** По умолчанию страница рендерится статически на сервере (Static SSR) — быстро и без постоянного соединения. Интерактивность включайте точечно на компонент или страницу: `InteractiveServer` (состояние на сервере, SignalR-цепь на каждого пользователя — следите за памятью и задержкой), `InteractiveWebAssembly` (код в браузере, большой первый вход), `InteractiveAuto` (сначала Server, потом WASM после загрузки). Не делайте всё приложение интерактивным «на всякий случай».
- **Пререндеринг** включён по умолчанию: компонент инициализируется дважды. В .NET 10 состояние переносится декларативно атрибутом `[PersistentState]` — используйте его, чтобы не вызывать API дважды.
- **Новое в .NET 10:** компонент `ReconnectModal` в шаблоне, `NavigationManager.NotFound()` и параметр `NotFoundPage` у `Router`, сохранение состояния цепи при обрыве соединения, fingerprinting и прелоад ассетов фреймворка, source-generated валидация вложенных форм, passkeys в шаблоне с Identity. В .NET 11 — клиентская валидация в статическом SSR, TempData и session в статическом SSR, `CacheView` для кеширования поддеревьев, виртуализация с разной высотой строк, `IHostedService` в WebAssembly.
- **Безопасность Blazor Server:** весь код серверный, но любые параметры, приходящие из браузера (события, JS interop), — недоверенный ввод. В WASM-приложении никаких секретов в клиенте; авторизацию проверяйте на API, а не только в UI.
- **Razor Pages/MVC** — зрелый выбор для серверных форм и контентных сайтов; Razor Pages проще для страничных сценариев. **htmx + Razor** — лёгкая интерактивность без SPA: сервер отдаёт фрагменты HTML; пакет `Htmx` даёт хелперы для заголовков `HX-*`, `Htmx.TagHelpers` — tag helpers. При htmx не забывайте антифорджери-токен в запросах.
- **.NET в браузере без Blazor:** если UI уже на JS/TS-фреймворке, а на C# нужна только логика (парсер, расчёты, общая с сервером валидация), соберите WebAssembly-модуль (workload `wasm-tools`, шаблон `wasmbrowser` из `wasm-experimental`) и свяжите его с JS через `[JSImport]`/`[JSExport]` (`System.Runtime.InteropServices.JavaScript`). Вызовы через границу делайте крупными, загрузку рантайма (несколько МБ) — ленивой.
- **Производительность Blazor:** `@key` для списков, `Virtualize` для длинных списков, избегайте частых `StateHasChanged`, для WASM — AOT-компиляция и тримминг (ценой размера), lazy loading сборок.

### Пакеты
- `Microsoft.AspNetCore.Components.WebAssembly` (10.0.12) и сопутствующие пакеты фреймворка — Blazor.
- `Microsoft.AspNetCore.Components.QuickGrid` (10.0.12, версия следует за фреймворком) — таблица с сортировкой и пагинацией.
- `Htmx` (1.12), `Htmx.TagHelpers` (1.12) — интеграция htmx (Khalid Abuhakmeh).

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [ASP.NET Core Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/) | Microsoft, живой | Вход в документацию; [модели хостинга](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models) |
| [Blazor render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes) | Microsoft, живой | Static SSR, Server, WebAssembly, Auto — когда что |
| [Prerender components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/prerender) | Microsoft, живой | Двойная инициализация и перенос состояния |
| [Blazor state management](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/) | Microsoft, живой | Где хранить состояние в разных режимах |
| [Blazor security](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/) | Microsoft, живой | Аутентификация в Server и WASM |
| [Razor Pages](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/) | Microsoft, живой | Страничная модель; [MVC](https://learn.microsoft.com/en-us/aspnet/core/mvc/overview) |
| [Htmx.Net](https://github.com/khalidabuhakmeh/Htmx.Net) | Khalid Abuhakmeh, живой | Хелперы htmx для ASP.NET Core; [блог автора](https://khalidabuhakmeh.com/) |
| [htmx](https://htmx.org/) | Carson Gross, живой | Документация и эссе о hypermedia |
| [JSImport/JSExport interop in .NET WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/?view=aspnetcore-10.0) | Microsoft, живой | .NET-модуль в JS-приложении без Blazor |

---

## 6. Облако, serverless и контейнеры

### Ключевые выводы
- **Контейнеры без Dockerfile:** `dotnet publish --os linux --arch x64 /t:PublishContainer` собирает OCI-образ средствами SDK, Docker нужен только для запуска. Настройки — свойства MSBuild `ContainerRepository`, `ContainerRegistry` (публикация прямо в реестр), `ContainerArchiveOutputPath` (tar для сканеров), `ContainerFamily`. Образы .NET 8+ запускаются не от root (`APP_UID`), порт по умолчанию 8080.
- **Chiseled-образы** (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`): без shell и пакетного менеджера, non-root по умолчанию, в разы меньше и с меньшим числом CVE. Отлаживать сложнее — для диагностики используйте sidecar или `dotnet-monitor`. SDK-образа chiseled нет: собирайте на обычном SDK-образе. Для Native AOT — `runtime-deps`. В обычных chiseled-образах нет ICU и tzdata, а `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` включён: культуры работают как инвариантная, а поиск часовых поясов по имени не находит зону. Если нужны культуры или часовые пояса, берите вариант `-extra` (`10.0-noble-chiseled-extra`).
- **Kubernetes:** liveness/readiness/startup-пробы на разные эндпоинты health checks; graceful shutdown — `HostOptions.ShutdownTimeout` больше, чем время дренирования, `terminationGracePeriodSeconds` в манифесте согласован с ним. Задавайте лимиты памяти — .NET GC учитывает лимит cgroup; на маленьких подах (≤1 CPU) проверьте режим GC (Server GC + DATAS в .NET 9+ включён по умолчанию для ASP.NET Core).
- **За прокси/ingress** включайте `UseForwardedHeaders` с явным списком доверенных прокси, иначе ломаются схема (https), IP клиента и redirect URI. **YARP** — обратный прокси/шлюз как библиотека ASP.NET Core: маршрутизация, балансировка, трансформации, BFF-шлюз; конфигурация из `appsettings` или кода.
- **Azure Functions:** поддержка in-process модели заканчивается 10 ноября 2026 — всё новое и всё существующее переводите на isolated worker на .NET 10. Используйте `ConfigureFunctionsWebApplication()` (ASP.NET Core-интеграция: `HttpRequest`/`IActionResult`) и оставляйте `FrameworkReference` на ASP.NET Core даже без HTTP-триггеров — так быстрее. Подвохи миграции: синхронный I/O запрещён; сериализация по умолчанию `System.Text.Json`, а атрибуты Newtonsoft (`[JsonProperty]`) молча игнорируются; логи из вашего кода фильтруются в `Program.cs`, а не в `host.json`. Переключайте рантайм через staging-слот. Долгие процессы — Durable Functions. Модель in-process `[устарело]`.
- **AWS Lambda:** управляемый рантайм `dotnet10` (Amazon Linux 2023, поддержка до ноября 2028); `dotnet8` выводится из поддержки 10 ноября 2026. Холодный старт сокращает Native AOT (на `provided.al2023`) — требует trim-совместимых библиотек и source-generated JSON. `Amazon.Lambda.Annotations` генерирует обвязку хендлеров; `Amazon.Lambda.AspNetCoreServer.Hosting` позволяет запустить minimal API целиком в Lambda (удобно, но тяжелее по старту).
- **Graceful shutdown:** при SIGTERM хост перестаёт принимать запросы, дожидается текущих и останавливает `IHostedService`; время задаёт `HostOptions.ShutdownTimeout` (по умолчанию 30 с). В Kubernetes добавьте небольшую паузу до остановки (preStop), чтобы ingress успел убрать под из балансировки.
- **Native AOT для сервисов:** быстрый старт и малый размер, но нет рефлексии и динамической генерации кода — EF Core в AOT ограничен (нужна компилированная модель и precompiled queries; в EF Core 10 это всё ещё экспериментальная функция, и [документация](https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries) прямо не рекомендует такой код в продакшене), MVC и SignalR-клиенты частично. Для долгоживущих сервисов в Kubernetes JIT с Dynamic PGO часто даёт лучшую пиковую производительность; AOT выгоден в serverless и CLI.
- **Конфигурация и секреты в облаке:** managed identity / workload identity + `DefaultAzureCredential` (или IAM-роль в AWS) вместо ключей в конфиге; Key Vault как источник конфигурации. Azure Container Apps — простой вариант хостинга контейнеров с KEDA-масштабированием, если не нужен «свой» Kubernetes.

### Пакеты
- `Microsoft.NET.Build.Containers` — встроено в SDK (`/t:PublishContainer`).
- `Yarp.ReverseProxy` (2.3) — обратный прокси.
- `Microsoft.Azure.Functions.Worker` (2.52), `Microsoft.Azure.Functions.Worker.Sdk` (2.1), `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` (2.1); новый project SDK `Azure.Functions.Sdk` (1.0) — Azure Functions isolated.
- `Amazon.Lambda.Core` (3.3), `Amazon.Lambda.RuntimeSupport` (2.2), `Amazon.Lambda.Annotations` (2.4), `Amazon.Lambda.AspNetCoreServer.Hosting` (2.2) — AWS Lambda.
- `Azure.Identity` (1.21), `Azure.Extensions.AspNetCore.Configuration.Secrets` (1.5) — идентичность и Key Vault.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Containerize an app with dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish) | Microsoft, 2026 | Сборка образов средствами SDK |
| [dotnet-docker](https://github.com/dotnet/dotnet-docker) | Microsoft, живой | Все теги образов; [Ubuntu Chiseled](https://github.com/dotnet/dotnet-docker/blob/main/documentation/ubuntu-chiseled.md) |
| [Announcing .NET Chiseled Containers](https://devblogs.microsoft.com/dotnet/announcing-dotnet-chiseled-containers/) | .NET Blog, 2023 | Зачем нужны chiseled-образы |
| [Kubernetes probes](https://kubernetes.io/docs/concepts/workloads/pods/probes/) | Kubernetes, живой | Семантика liveness/readiness/startup |
| [Configure for proxy servers and load balancers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) | Microsoft, живой | Forwarded headers и доверенные прокси |
| [Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel) | Microsoft, живой | Лимиты, эндпоинты, [привязка адресов](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints) |
| [YARP overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/yarp-overview) | Microsoft, живой | Обратный прокси; [быстрый старт](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/getting-started), [репозиторий](https://github.com/dotnet/yarp) |
| [Guide for running C# Azure Functions in the isolated worker model](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide) | Microsoft, живой | Основная модель Functions |
| [Migrate C# apps from in-process to isolated worker](https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model) | Microsoft, 2026 | Дата окончания in-process и пошаговая миграция |
| [Durable Functions overview](https://learn.microsoft.com/en-us/azure/durable-task/durable-functions/durable-functions-overview) | Microsoft, живой | Оркестрации и долгие процессы |
| [azure-functions-dotnet-worker](https://github.com/Azure/azure-functions-dotnet-worker) | Microsoft, живой | Исходники и примеры isolated worker |
| [Lambda runtimes](https://docs.aws.amazon.com/lambda/latest/dg/lambda-runtimes.html) | AWS, живой | Даты поддержки `dotnet10`/`dotnet8` |
| [Building Lambda functions with C#](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html) | AWS, живой | Модели хендлеров; [Native AOT](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-native-aot.html) |
| [aws-lambda-dotnet](https://github.com/aws/aws-lambda-dotnet) | AWS, живой | Библиотеки, шаблоны, Annotations |
| [Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/overview) | Microsoft, живой | Хостинг контейнеров без своего Kubernetes |

---

## 7. Фоновая обработка

### Ключевые выводы
- **`BackgroundService`** — база для фоновых задач в любом хосте (`dotnet new worker`). Правила: всегда уважайте `stoppingToken`; ловите исключения внутри цикла (необработанное исключение в `ExecuteAsync` по умолчанию останавливает весь хост — `BackgroundServiceExceptionBehavior.StopHost`); не блокируйте старт — тяжёлую инициализацию делайте после `await Task.Yield()` или в `ExecuteAsync`; scoped-зависимости (DbContext) получайте через `IServiceScopeFactory.CreateAsyncScope()` на каждую итерацию.
- **Периодика** — `PeriodicTimer` вместо `Task.Delay` в цикле (не накапливает дрейф, корректно отменяется). Очереди в памяти — `System.Threading.Channels` с ограниченной ёмкостью (backpressure).
- **Несколько реплик:** in-memory фоновая задача запустится на каждой реплике. Нужна распределённая блокировка (БД, Redis) или планировщик с кластерным режимом (Quartz с `JobStore` в БД, Hangfire, TickerQ с Redis-координацией), либо выносите задачи в отдельный worker с одной репликой/очередью.
- **Выбор планировщика:**
  - Hangfire — fire-and-forget, отложенные и cron-задачи с хранилищем в БД и дашбордом; ядро LGPL-3.0, расширенные функции (батчи, приоритеты) — платный Pro. Аргументы задач сериализуются: передавайте ID, а не сущности.
  - Quartz.NET (Apache-2.0) — гибкое cron-расписание, календари, кластеризация, `Quartz.Extensions.Hosting`.
  - TickerQ (MIT/Apache-2.0) — новый планировщик на source generators без рефлексии, совместим с AOT, хранилища EF Core (PostgreSQL, SQL Server, SQLite, MySQL) и Redis, дашборд на SignalR. Молодой проект — проверяйте зрелость под свои требования.
  - Durable/долгие бизнес-процессы — не планировщик, а шина с сагами или workflow-движок (раздел 3).
- **Идемпотентность** задач обязательна: любой планировщик может повторить задачу после сбоя.
- **Не используйте fire-and-forget из запроса** (`_ = Task.Run(...)` в контроллере): после ответа scoped-сервисы уже уничтожены, исключения теряются, при остановке процесса работа пропадает. Кладите задачу в `Channel<T>` с обработкой в `BackgroundService` или в персистентную очередь.
- **Службы ОС без контейнеров:** тот же worker становится службой Windows через `builder.Services.AddWindowsService(o => o.ServiceName = "...")` (регистрация — `sc.exe create` или инсталлятор) или демоном systemd через `AddSystemd()` (уведомление о готовности, журнал в формате journald). Вне соответствующей среды оба вызова ничего не делают, поэтому один бинарник запускается и как консольное приложение. Текущий каталог службы Windows — `System32`: пути к файлам стройте от `AppContext.BaseDirectory`.
- **Выбор по требованиям:** задача должна пережить рестарт → персистентный планировщик или брокер; нужна только периодика внутри одной реплики → `BackgroundService` + `PeriodicTimer`; облако → таймер-триггер Functions/EventBridge + Lambda вместо постоянно работающего процесса.

### Пакеты
- `Microsoft.Extensions.Hosting` (10.0.12) — Generic Host, `BackgroundService`.
- `Microsoft.Extensions.Hosting.WindowsServices` / `Microsoft.Extensions.Hosting.Systemd` (10.0.12) — служба Windows и демон systemd.
- `Hangfire.Core` / `Hangfire.AspNetCore` (1.8), `Hangfire.PostgreSql` (1.21).
- `Quartz` / `Quartz.Extensions.Hosting` (4.3).
- `TickerQ` (10.4).

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services) | Microsoft, живой | `IHostedService`, scoped-сервисы, очереди |
| [Worker services in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers) | Microsoft, живой | Шаблон worker, поведение при исключениях |
| [Create Windows Service using BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service) | Microsoft, живой | `AddWindowsService`, публикация, `sc.exe`, восстановление после сбоя |
| [Hangfire documentation](https://docs.hangfire.io/en/latest/) | Hangfire OÜ, живой | Типы задач, хранилища; [сайт](https://www.hangfire.io/), [цены Pro](https://www.hangfire.io/pricing/), [репозиторий](https://github.com/HangfireIO/Hangfire) |
| [Quartz.NET](https://www.quartz-scheduler.net/) | Quartz.NET, живой | Расписания, кластеризация; [репозиторий](https://github.com/quartznet/quartznet) |
| [TickerQ](https://tickerq.net/) | Arcenox, живой | Документация; [репозиторий](https://github.com/Arcenox-co/TickerQ), [NuGet](https://www.nuget.org/packages/TickerQ) |
| [Async guidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md) | David Fowler, 2018–2024 | Классика: sync-over-async, fire-and-forget, таймеры — что ломает сервер |

---

## 8. Наблюдаемость и устойчивость

### Ключевые выводы
- **OpenTelemetry — стандарт.** .NET генерирует трассировки через `ActivitySource`, метрики через `Meter` и логи через `ILogger` — OpenTelemetry SDK лишь собирает и экспортирует их (OTLP). Настройка: `AddOpenTelemetry().WithTracing(...).WithMetrics(...)` + `UseOtlpExporter()`; инструментирование ASP.NET Core, HttpClient, EF Core/Npgsql, gRPC. В Aspire это делают ServiceDefaults. Инструментирование EF Core в OpenTelemetry всё ещё beta — для PostgreSQL надёжнее встроенная трассировка Npgsql.
- **Логирование:** `ILogger<T>` + генератор `[LoggerMessage]` для горячих путей (без боксинга и парсинга шаблона, проверка уровня до форматирования). Структурные шаблоны (`"Order {OrderId} created"`), никакой интерполяции строк в вызовах логгера. Serilog остаётся популярным бэкендом (sinks, обогащение); при OTel-пайплайне используйте `Serilog.Sinks.OpenTelemetry` или стандартный провайдер OTel. `Microsoft.Extensions.Compliance.Redaction` маскирует персональные данные в логах; HTTP logging (`AddHttpLogging`) включайте выборочно — он дорогой и может логировать секреты.
- **HttpClient:** только через `IHttpClientFactory` (типизированные или именованные клиенты) или долгоживущий singleton с `SocketsHttpHandler { PooledConnectionLifetime = ... }`. `new HttpClient()` на каждый запрос `[устарело]` — исчерпание сокетов; singleton без `PooledConnectionLifetime` — не видит смену DNS. Не захватывайте типизированный клиент в singleton-сервис — handler не будет ротироваться.
- **Устойчивость — `Microsoft.Extensions.Http.Resilience` (Polly v8):** `AddStandardResilienceHandler()` задаёт цепочку rate limiter (1000) → общий таймаут 30 с → retry (3 попытки, экспонента с jitter) → circuit breaker → таймаут попытки 10 с. Добавляйте один handler на клиента, не стекируйте; ретраи по умолчанию идут для всех методов — для неидемпотентных вызовов `options.Retry.DisableForUnsafeHttpMethods()`. Hedging (`AddStandardHedgingHandler`) — для чтения с несколькими репликами. Polly v8 бросает `TimeoutRejectedException`, а не `TimeoutException`. `Microsoft.Extensions.Http.Polly` и API Polly v7 (`Policy.Handle<>()`) `[устарело]`.
- **Не только HTTP:** `AddResiliencePipeline` (Microsoft.Extensions.Resilience) — для БД, брокеров, SDK. Для EF Core используйте встроенный `EnableRetryOnFailure` провайдера, а не Polly поверх `SaveChanges`.
- **Корреляция:** W3C `traceparent` распространяется автоматически через HttpClient, gRPC и большинство шин; добавляйте `TraceId` в ответы об ошибках (`ProblemDetails.Extensions["traceId"]` по умолчанию) — это сильно ускоряет разбор инцидентов. Собственные спаны — `ActivitySource.StartActivity()` с проверкой на `null` (нет слушателя — нет накладных расходов).
- **Сэмплирование и стоимость:** в проде трассируйте выборочно (`ParentBased` + `TraceIdRatioBased` или tail sampling в коллекторе); не превращайте высококардинальные значения (ID пользователя, URL с параметрами) в теги метрик.
- **Метрики, которые важны:** латентность p95/p99, число ошибок, длина очереди пула потоков (`dotnet.thread_pool.queue.length`), GC pause, количество активных соединений. В .NET 10 есть встроенные метрики аутентификации, авторизации и Identity.

### Пакеты
- `OpenTelemetry` / `OpenTelemetry.Extensions.Hosting` / `OpenTelemetry.Exporter.OpenTelemetryProtocol` (1.19), `OpenTelemetry.Instrumentation.AspNetCore` / `.Http` (1.19); `.EntityFrameworkCore` — только beta.
- `Serilog` (4.4), `Serilog.AspNetCore` (10.0), `Serilog.Sinks.OpenTelemetry` (4.2).
- `Polly.Core` / `Polly` (8.8), `Microsoft.Extensions.Http.Resilience` / `Microsoft.Extensions.Resilience` (10.10).
- `Microsoft.Extensions.Telemetry` (10.10), `Microsoft.Extensions.Compliance.Redaction` (10.10) — обогащение и маскирование.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [.NET observability with OpenTelemetry](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel) | Microsoft, живой | Как .NET API ложатся на OTel; [distributed tracing](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing) |
| [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/) | OpenTelemetry, живой | Настройка SDK и экспортёров; [репозиторий](https://github.com/open-telemetry/opentelemetry-dotnet) |
| [Compile-time logging source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/source-generation) | Microsoft, живой | `[LoggerMessage]`; [high-performance logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/high-performance-logging) |
| [HTTP logging](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/) | Microsoft, живой | Что и как логировать на уровне HTTP |
| [Data redaction](https://learn.microsoft.com/en-us/dotnet/core/extensions/compliance) | Microsoft, живой | Классификация и маскирование данных |
| [Serilog](https://serilog.net/) | Serilog, живой | Структурное логирование; [Serilog.AspNetCore](https://github.com/serilog/serilog-aspnetcore), [OTel sink](https://github.com/serilog/serilog-sinks-opentelemetry), [блог автора](https://nblumhardt.com/) |
| [IHttpClientFactory with .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory) | Microsoft, живой | Типизированные клиенты и время жизни |
| [HttpClient guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines) | Microsoft, живой | Сокеты, DNS, `PooledConnectionLifetime` |
| [Introduction to HttpClientFactory](https://www.stevejgordon.co.uk/introduction-to-httpclientfactory-aspnetcore) | Steve Gordon, 2018 | Классическая серия о внутреннем устройстве фабрики; [блог](https://www.stevejgordon.co.uk/) |
| [Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) | Microsoft, 2026 | Standard/hedging handlers и их дефолты; [введение в resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/) |
| [Polly docs](https://www.pollydocs.org/) | App vNext, живой | [Стратегии](https://www.pollydocs.org/strategies/) и [миграция с v7](https://www.pollydocs.org/migration-v8) |
| [dotnet/extensions](https://github.com/dotnet/extensions) | Microsoft, живой | Исходники Resilience, HybridCache, Telemetry, AI |

---

## 9. Безопасность веб-приложений

### Ключевые выводы
- **Ориентиры:** OWASP Top 10 (актуальная редакция — 2025) и OWASP .NET Security Cheat Sheet. Большинство уязвимостей в .NET-сервисах — не в криптографии, а в авторизации: проверяйте право на конкретный ресурс (владелец, тенант) в каждом обработчике, а не только `[Authorize]` на уровне эндпоинта (IDOR/BOLA).
- **Секреты:** в разработке — user-secrets (`dotnet user-secrets`, хранятся вне репозитория); в проде — Key Vault/Secrets Manager через managed identity, а не переменные с ключами в CI. Никогда не коммитьте `appsettings.Production.json` с паролями; сканируйте репозиторий на секреты.
- **Data Protection** (шифрует cookies, antiforgery-токены, TempData): при нескольких репликах ключи обязаны быть общими (`PersistKeysToDbContext`/Redis/Blob) и защищёнными (`ProtectKeysWith...`). Иначе после деплоя или на другой реплике пользователей «разлогинивает» и формы падают с ошибкой antiforgery.
- **CSRF:** Razor Pages/MVC/Blazor-формы защищены antiforgery-токенами по умолчанию; для minimal API с формами нужен `UseAntiforgery`. В .NET 11 появилась автоматическая CSRF-защита на основе заголовков Fetch Metadata (`Sec-Fetch-Site`), включённая по умолчанию. API на Bearer-токенах (без cookies) CSRF не подвержены — но если вы используете cookie-аутентификацию для SPA (BFF), защита нужна.
- **XSS:** Razor экранирует вывод по умолчанию; опасны `Html.Raw`, `MarkupString` в Blazor и вставка данных в `<script>`/JS interop. Добавьте Content-Security-Policy и другие заголовки (`NetEscapades.AspNetCore.SecurityHeaders`).
- **CORS** — явный список origin; `AllowAnyOrigin` вместе с credentials запрещён фреймворком, и это правильно. CORS — не механизм авторизации.
- **HTTPS и HSTS** (`UseHsts` в проде), `UseHttpsRedirection` только если TLS не терминируется на прокси. Cookies — `Secure`, `HttpOnly`, `SameSite=Lax/Strict`.
- **SQL-инъекции:** EF Core и Dapper параметризуют запросы; опасны `FromSqlRaw`/`ExecuteSqlRaw` с конкатенацией — EF 10 выдаёт на это предупреждение анализатора, используйте `FromSql` с интерполяцией (`FormattableString`). EF 10 по умолчанию маскирует встроенные константы в логах SQL.
- **Входные данные:** ограничивайте размер тела (`MaxRequestBodySize`), число элементов коллекций, глубину JSON; rate limiting на дорогих и анонимных эндпоинтах (логин, регистрация, поиск). Загрузки файлов — проверка типа по содержимому, хранение вне веб-корня, случайные имена.
- **Пароли и аутентификация:** не пишите свою — ASP.NET Core Identity (PBKDF2 с высоким числом итераций), passkeys с .NET 10, внешний IdP. Валидируйте JWT полностью; короткоживущие access-токены + refresh.
- **Зависимости:** `dotnet list package --vulnerable` и NuGet audit (включён по умолчанию в восстановлении пакетов) в CI.

### Пакеты
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (10.0.12) — Identity.
- `Microsoft.AspNetCore.DataProtection.*` (часть фреймворка), `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` / `.StackExchangeRedis` — хранение ключей.
- `Azure.Extensions.AspNetCore.Configuration.Secrets` (1.5), `Azure.Identity` (1.21) — Key Vault.
- `NetEscapades.AspNetCore.SecurityHeaders` (1.3) — CSP и заголовки безопасности.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [OWASP Top 10](https://top10.owasp.org/) | OWASP, 2025 | Актуальный список основных рисков |
| [OWASP .NET Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html) | OWASP, живой | Конкретные рекомендации для .NET |
| [Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) | Microsoft, живой | user-secrets |
| [Azure Key Vault configuration provider](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration) | Microsoft, живой | Секреты в конфигурации через managed identity; [обзор Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/general/overview) |
| [ASP.NET Core Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction) | Microsoft, живой | Ключи, хранение, ротация |
| [Prevent CSRF attacks](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery) | Microsoft, живой | Antiforgery в MVC, Razor Pages, minimal APIs |
| [Automatic CSRF protection based on Fetch Metadata headers](https://andrewlock.net/exploring-the-dotnet-11-preview-6-automatic-csrf-protection-based-on-fetch-metadata-http-headers/) | Andrew Lock, 2026 | Как работает новая защита в .NET 11 |
| [Prevent XSS](https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting) | Microsoft, живой | Кодирование вывода в Razor и JS |
| [Enable CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors) | Microsoft, живой | Политики и типичные ошибки |
| [Enforce HTTPS](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl) | Microsoft, живой | HSTS, редиректы, прокси |
| [Passkeys in ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/) | Microsoft, 2025 | WebAuthn с .NET 10 |
| [JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn) | Microsoft, живой | Выпуск и проверка токенов, dev-инструмент `dotnet user-jwts` |
| [NetEscapades.AspNetCore.SecurityHeaders](https://github.com/andrewlock/NetEscapades.AspNetCore.SecurityHeaders) | Andrew Lock, живой | CSP и заголовки одной строкой |
| [AutoMapper & MediatR commercial editions](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/) | Jimmy Bogard, 2025 | Условия лицензий (важно для юридической проверки зависимостей); [блог](https://www.jimmybogard.com/) |
| [Nick Chapsas](https://www.youtube.com/@nickchapsas) | Nick Chapsas, живой | Короткие видео о новых фичах и производительности .NET |
| [.NET Blog](https://devblogs.microsoft.com/dotnet/) | Microsoft, живой | Анонсы релизов; [.NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/) |

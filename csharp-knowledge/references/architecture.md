# Архитектура .NET-решений и стратегия тестирования

Как раскладывать C#/.NET-решение любого размера и назначения: файлы решения и общая настройка сборки, направление зависимостей, слоистая, «чистая», вертикальные срезы и модульный монолит, DDD, CQRS и медиаторы после смены лицензии MediatR, event sourcing, архитектурные тесты, ошибки по слоям, конфигурация и feature flags, библиотека против приложения, версионирование публичного API, влияние графа проектов на сборку и стратегия тестирования от unit до мутационных тестов. В конце — кодовые базы для изучения, разногласия источников, анти-паттерны и чеклист. Актуально на октябрь 2026 года: .NET 10 (LTS) — текущий релиз, .NET 11 в стадии RC1 (релиз 10 ноября 2026), `.slnx` — формат решения по умолчанию, Microsoft.Testing.Platform поддерживают все основные тестовые фреймворки, MediatR 13+ и MassTransit 9 распространяются по коммерческим лицензиям. За 2025–2026 годы разговор сместился от «какой шаблон Clean Architecture взять» к «сколько церемоний реально нужно» и к замене коммерциализированных библиотек.

- [Структура решения](#структура-решения)
- [Направление зависимостей и слои](#направление-зависимостей-и-слои)
- [Архитектурные стили](#архитектурные-стили)
- [DDD: тактические паттерны](#ddd-тактические-паттерны)
- [CQRS и медиаторы](#cqrs-и-медиаторы)
- [Event sourcing](#event-sourcing)
- [Архитектурные тесты](#архитектурные-тесты)
- [Ошибки по слоям](#ошибки-по-слоям)
- [Конфигурация, логирование, feature flags](#конфигурация-логирование-feature-flags)
- [Библиотека против приложения и версионирование API](#библиотека-против-приложения-и-версионирование-api)
- [Граф проектов и время сборки](#граф-проектов-и-время-сборки)
- [Стратегия тестирования](#стратегия-тестирования)
- [Тестовые фреймворки и платформа](#тестовые-фреймворки-и-платформа)
- [Интеграционные тесты](#интеграционные-тесты)
- [Снапшоты, контракты, property-based и мутации](#снапшоты-контракты-property-based-и-мутации)
- [Тестовые двойники и ассерты](#тестовые-двойники-и-ассерты)
- [Кодовые базы для изучения](#кодовые-базы-для-изучения)
- [Где источники расходятся](#где-источники-расходятся)
- [Анти-паттерны](#анти-паттерны)
- [Чеклист: структура нового решения](#чеклист-структура-нового-решения)

## Структура решения

### Ключевые выводы

- Раскладка по умолчанию: `src/` для продуктового кода, `tests/` для тестов, в корне — файл решения, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`. Имя папки совпадает с именем проекта и корневым namespace.
- Файл решения — `.slnx`. С .NET 10 `dotnet new sln` создаёт SLNX по умолчанию; поддержка в CLI появилась в SDK 9.0.200, миграция — `dotnet sln migrate`. Старый формат — `dotnet new sln --format sln`. Файлы `.sln` в новых решениях считайте [устарело].
- Общие свойства (TargetFramework, Nullable, LangVersion, TreatWarningsAsErrors, анализаторы) держите в `Directory.Build.props`, а цели и свойства, зависящие от значений проекта, — в `Directory.Build.targets`. MSBuild ищет эти файлы вверх по дереву и импортирует первый найденный. На Linux имя файла чувствительно к регистру.
- Версии пакетов централизуйте через Central Package Management: `Directory.Packages.props` с `ManagePackageVersionsCentrally=true`, `<PackageVersion>` в нём и `<PackageReference>` без версии в проектах. Импортируется только ближайший файл, родительский подключайте вручную.
- Инструменты, нужные всем проектам (анализаторы, версионирование вроде Nerdbank.GitVersioning), подключайте через `GlobalPackageReference`. Транзитивный пиннинг (`CentralPackageTransitivePinningEnabled`) удобен в приложениях, но в библиотеках он переносит закреплённые транзитивные зависимости в nuspec, так что включайте его осознанно.
- При нескольких источниках NuGet CPM выдаёт предупреждение NU1507. Настройте package source mapping: это ещё и защита от подмены пакетов.
- `global.json` закрепляет SDK (`sdk.version` + `rollForward`) и с .NET 10 SDK выбирает режим `dotnet test` (раздел о тестах).
- Внутри одного репозитория используйте `ProjectReference`, а не локальные NuGet-пакеты. Пакет нужен, когда у компонента свой цикл релизов и свои потребители.
- `InternalsVisibleTo` для тестового проекта (`<InternalsVisibleTo Include="MyApp.Domain.Tests" />` в csproj) допустим, но это сигнал: если тесту нужны internal-члены, возможно, тестируется реализация, а не поведение.

```xml
<!-- Directory.Build.props: общее для всех проектов под этой папкой -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

```xml
<!-- Directory.Packages.props: единственное место с версиями -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="xunit.v3" Version="4.0.1" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.15.0" />
  </ItemGroup>
  <ItemGroup>
    <GlobalPackageReference Include="Nerdbank.GitVersioning" Version="3.10.94" />
  </ItemGroup>
</Project>
```

Версии в примере — последние стабильные на nuget.org на 7 октября 2026; при создании решения берите актуальные.

```json
// global.json
{
  "sdk": { "version": "10.0.100", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Breaking change: dotnet new sln defaults to SLNX](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default) | Microsoft Learn, 2025 | Официально: SLNX по умолчанию в .NET 10, как вернуть старый формат |
| [Introducing support for SLNX in the .NET CLI](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli) | .NET Blog, март 2025 | Появление формата в SDK 9.0.200, `dotnet sln migrate` |
| [The New .slnx Solution Format (migration guide)](https://milanjovanovic.tech/blog/the-new-slnx-solution-format-migration-guide) | Milan Jovanović, 2025 | Практическая миграция |
| [SLNX: the new .NET solution XML file format](https://blog.ndepend.com/slnx-the-new-net-solution-xml-file-format/) | NDepend blog | Устройство формата и поддержка в инструментах |
| [Customize your build by folder](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory) | Microsoft Learn, живой | Как ищутся и импортируются `Directory.Build.props/targets` |
| [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) | Microsoft Learn, 2025 | Нормативный источник: `VersionOverride`, транзитивный пиннинг, `GlobalPackageReference`, NU1507 |
| [Introducing Central Package Management](https://devblogs.microsoft.com/nuget/introducing-central-package-management/) | NuGet Blog, 2022 | Исходный анонс и мотивация |

## Направление зависимостей и слои

### Ключевые выводы

- Зависимости направлены внутрь: домен не знает об EF Core, ASP.NET Core, HTTP, очередях и сериализации. Инфраструктура реализует интерфейсы, объявленные в домене или слое приложения (dependency inversion).
- Минимальный вариант для сервиса: `Domain` (сущности, value objects, доменные события, без пакетов фреймворков) ← `Application` (сценарии, порты) ← `Infrastructure` (EF Core, внешние API) и `Web`/`Api` (composition root). Каждый слой — отдельный проект, чтобы компилятор запрещал обратные ссылки.
- Composition root один — `Program.cs` хост-проекта. Расширения `AddInfrastructure()` в каждом слое удобны, но регистрацию всё равно вызывает только хост.
- Граница проекта — самый дешёвый способ проверить направление зависимостей: обратную ссылку не даст сделать компилятор. Внутри проекта границы держат `internal` и архитектурные тесты.
- По умолчанию делайте типы `internal`, а `public` оставляйте для того, что реально нужно другим проектам. В модульном монолите публичен только контракт модуля.
- Не выносите в отдельный проект то, что не является границей. Проекты `*.Common`, `*.Shared`, `*.Utils`, на которые ссылаются все, быстро становятся свалкой и связывают модули.
- DI-контейнер не отменяет архитектуру: внедрять `DbContext` в контроллер технически можно, но так слой приложения теряет смысл. Выберите правило и закрепите его тестом.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) | Jason Taylor, живой | Каноничная раскладка Domain / Application / Infrastructure / Web на .NET 10 с Aspire |
| [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) | Steve Smith, живой | Core / UseCases / Infrastructure / Web на net10.0, FastEndpoints, медиатор — source-generated Mediator вместо MediatR, slnx и CPM, есть минимальный вариант |
| [modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd) | Kamil Grzybek, 2019–… | Слои внутри каждого модуля и правило «только IntegrationEvents видны другим модулям» |

## Архитектурные стили

### Ключевые выводы

- **Слоистая / Clean / onion / hexagonal** — по сути одна идея: ядро без инфраструктуры, порты и адаптеры вокруг. Шаблоны Jason Taylor и Ardalis — хорошая отправная точка для долгоживущего сервиса с богатым доменом, но для CRUD они дают много церемоний: четыре проекта, маппинг на каждом шаге, интерфейс на каждый класс.
- **Vertical Slice Architecture** (Jimmy Bogard, 2018): код группируется вокруг запросов, а не технических слоёв. Принцип: «minimize coupling between slices, and maximize coupling in a slice». Срез (endpoint + handler + валидация + доступ к данным) лежит в одной папке, общий код появляется только по факту дублирования. В 2026 году Bogard продвигает VSA ещё и как удобные рамки для разработки с ИИ-ассистентами.
- **Модульный монолит** — разумный выбор по умолчанию для нового бизнес-приложения: один деплой, но жёсткие границы модулей. У каждого модуля свои данные (схема БД), публичный контракт и интеграционные события. Транзакций через границу модулей нет; надёжная доставка — через Outbox/Inbox. Внутри модуля можно брать слои или вертикальные срезы.
- **Микросервисы** оправданы, когда нужны независимый деплой, масштабирование или разные команды, а не ради «чистоты». Цена: распределённые транзакции, контракты, наблюдаемость, сложные локальная разработка и тестирование. Выделяйте сервис из модульного монолита, когда модуль «это заслужил» (формулировка Milan Jovanović).
- **Aspire** (с версии 13, ноябрь 2025, без «.NET» в названии; документация переехала на aspire.dev) — оркестрация для локальной разработки, service discovery, телеметрия и дашборд, плюс тестирование всего распределённого приложения. Это не архитектура, а инструмент: модульный монолит с Postgres и Redis тоже выигрывает от AppHost.
- eShop — эталон «сервисной» архитектуры на .NET 10 + Aspire: Catalog/Basket/Ordering API, `Ordering.Domain`, EventBus, AppHost и E2E-тесты на Playwright. Изучайте его как каталог приёмов, а не как шаблон для маленького проекта.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/) | Jimmy Bogard, 2018 | Первоисточник термина и аргументы против слоёв |
| [Vertical Slice Architecture (тег блога)](https://www.jimmybogard.com/tag/vertical-slice-architecture/) | Jimmy Bogard, 2026 | Свежие вебинары: VSA как рамки для разработки с ИИ |
| [VerticalSliceArchitecture.Documentation](https://github.com/Hona/VerticalSliceArchitecture.Documentation) | Luke Parker (Hona) и сообщество | История термина и cookbook; последние изменения — март 2024 |
| [Modular Monolith Architecture in .NET: The Complete Guide](https://milanjovanovic.tech/blog/modular-monolith-architecture-dotnet) | Milan Jovanović, июль 2026 | Границы модулей, изоляция данных, коммуникация, тесты, выделение сервисов |
| [Design DDD modular monolith](https://www.infoq.com/news/2019/09/design-ddd-modular-monolith/) | InfoQ, 2019 | Обзор проекта Grzybek и его мотивации |
| [dotnet/eShop](https://github.com/dotnet/eShop) | Microsoft, живой | Референсное сервисное приложение на Aspire |
| [Microsoft Releases Aspire 13](https://visualstudiomagazine.com/Articles/2025/11/12/Microsoft-Releases-Aspire-13.aspx) | Visual Studio Magazine, 2025 | Переименование и polyglot-курс |
| [Aspire 13: Python first class](https://visualstudiomagazine.com/Articles/2025/12/08/Aspire-13-Makes-Python-a-First-Class-Workload-with-NET-and-JavaScript.aspx) | Visual Studio Magazine, 2025 | Что изменилось для смешанных стеков |

## DDD: тактические паттерны

### Ключевые выводы

- **Value object** в C# — `readonly record struct` или `sealed record` с фабрикой, которая валидирует инвариант (`Email.Create(string)` возвращает результат или ошибку). Так примитивная одержимость (`string email` повсюду) исчезает, а невалидное значение не может существовать.
- **Сущность и агрегат**: агрегат — граница консистентности и транзакции. Изменения идут через методы корня (`order.AddLine(...)`), а не через публичные сеттеры и коллекции. Наружу отдавайте `IReadOnlyList<T>`, ссылки на другие агрегаты храните как идентификаторы (`CustomerId`), а не навигационные свойства.
- Строго типизированные идентификаторы (`readonly record struct OrderId(Guid Value)`) ловят перепутанные аргументы на этапе компиляции. EF Core поддерживает их через value converters.
- **Доменные события**: агрегат копит события в списке, а их публикация происходит при сохранении (в `SaveChanges` или в интерсепторе) — внутри транзакции для внутримодульных обработчиков и через Outbox для интеграционных. Доменное событие (внутри модуля) и интеграционное (контракт между модулями или сервисами) — разные типы.
- Репозиторий поверх EF Core нужен только агрегатам и только когда он скрывает что-то полезное. Generic `IRepository<T>` поверх `DbContext`, который сам уже реализует Unit of Work и Repository, обычно лишний слой.
- Тактические паттерны без стратегических (bounded contexts, ubiquitous language) дают «DDD-lite» с большим количеством кода и малой пользой. Для CRUD-областей берите простые сущности и transaction script.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd) | Kamil Grzybek | Агрегаты, бизнес-правила как объекты, доменные и интеграционные события, Outbox/Inbox — с подробным README |
| [dotnet/eShop: Ordering.Domain](https://github.com/dotnet/eShop) | Microsoft | Агрегат заказа, доменные события, EF Core-маппинг |
| [Event Sourcing in .NET (samples)](https://github.com/oskardudycz/EventSourcing.NetCore/) | Oskar Dudycz, живой | Агрегаты и события в разных стилях: с классами и функциональном |

## CQRS и медиаторы

### Ключевые выводы

- CQRS — это разделение моделей записи и чтения, а не обязательная шина. Достаточно отдельных классов-обработчиков команд и запросов; запросы могут читать проекции или делать SQL напрямую в обход агрегатов.
- **MediatR сменил лицензию**: 2 июля 2025 года вышла версия 13.0 под двойной лицензией RPL-1.5 / коммерческой (Lucky Penny Software). 12.5.0 — последняя версия под Apache 2.0, исправлений для неё не будет. Компании с выручкой до $5 млн могут бесплатно использовать Community edition (нужна регистрация). AutoMapper с версии 15 сменил лицензию так же, MassTransit v9 тоже стал коммерческим, а v8 получает патчи безопасности до конца 2026 года.
- Реакция сообщества разделилась. Jason Taylor оставил MediatR и AutoMapper в шаблоне: стоимость лицензии «insignificant» по сравнению с пользой зрелых библиотек. Другие авторы советуют по умолчанию брать обычные сервисы или обработчики, а медиатор — только если реально нужны pipeline behaviors.
- Альтернативы: **Mediator** (martinothamar) — source-generated, API почти как у MediatR, совместим с Native AOT, ошибки регистрации ловятся при сборке. **Wolverine** (JasperFx) — медиатор и шина сообщений с outbox, сагами и интеграцией с EF Core и Marten. Ещё вариант — написанный вручную `ICommandHandler<T>` + декораторы (Scrutor) для сквозной логики. FastEndpoints закрывает часть сценариев на HTTP-слое.
- Медиатор усложняет навигацию: «Go to definition» ведёт в `Send`, а не в обработчик. Если pipeline не используется, вызов через медиатор — лишний уровень косвенности.
- Для сообщений между сервисами без MassTransit смотрите Wolverine, Rebus или клиенты брокеров напрямую (`RabbitMQ.Client`, `Azure.Messaging.ServiceBus`).

```csharp
// Вариант без медиатора: обработчик — обычный сервис, DI регистрирует его напрямую
public sealed record PlaceOrder(CustomerId CustomerId, IReadOnlyList<OrderLineDto> Lines);

public sealed class PlaceOrderHandler(AppDbContext db, TimeProvider clock)
{
    public async Task<Result<OrderId>> Handle(PlaceOrder cmd, CancellationToken ct)
    {
        var order = Order.Place(cmd.CustomerId, cmd.Lines, clock.GetUtcNow());
        if (order.IsFailure) return order.Error;
        db.Orders.Add(order.Value);
        await db.SaveChangesAsync(ct);
        return order.Value.Id;
    }
}
```

`Result<T>` в примере — ваш тип или библиотека (раздел об ошибках).

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [MediatR vs plain service classes in 2026](https://startdebugging.net/2026/05/mediatr-vs-plain-service-classes-in-2026/) | Start Debugging, май 2026 | Факты о лицензии и критерии «оставить или убрать» |
| [MediatR and MassTransit Going Commercial](https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you) | Milan Jovanović, апрель 2025 | Хронология, цены MassTransit v9, альтернативы |
| [AutoMapper, MediatR and MassTransit Are Going Commercial](https://engincanveske.substack.com/p/automapper-mediatr-and-masstransit) | Engin Can Veske, 2025 | Обзор реакции сообщества |
| [AutoMapper and MediatR Went Commercial — Here's What We're Doing](https://github.com/jasontaylordev/CleanArchitecture/discussions/1346) | Jason Taylor, июль 2025 | Аргумент «оставить и заплатить» |
| [martinothamar/Mediator](https://github.com/martinothamar/Mediator) | Martin Othamar, живой | Source-generated замена MediatR, AOT ([NuGet](https://www.nuget.org/packages/Mediator.SourceGenerator)) |
| [Wolverine](https://wolverinefx.net/) | JasperFx (Jeremy D. Miller), живой | Медиатор + шина + outbox, «Critter Stack» вместе с Marten |

## Event sourcing

### Ключевые выводы

- Event sourcing уместен, когда важна история изменений (аудит, финансы, процессы) или нужны разные проекции одних и тех же фактов. Для CRUD-данных это лишняя сложность: версии событий, upcasting, перестроение проекций.
- **Marten** использует PostgreSQL как документную БД и хранилище событий, с проекциями (inline, async, live). Это прагматичный выбор, если у вас уже есть Postgres. С Wolverine образует «Critter Stack».
- **EventStoreDB переименован в KurrentDB** (ребрендинг объявлен 22 ноября 2024 года, первый релиз KurrentDB — 25.0). EventStoreDB 24.10 LTS поддерживается до октября 2026 года, поэтому новые проекты сразу ставьте на KurrentDB. Имя EventStoreDB в старых статьях — [устарело].
- Event sourcing применяйте точечно, к отдельным агрегатам или модулям, а не ко всей системе. Проекции для чтения — это CQRS-модель чтения.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Marten: Introduction](https://martendb.io/introduction) | JasperFx, живой | Документы и события на PostgreSQL |
| [Marten: Event Store](https://martendb.io/events/) | JasperFx, живой | Потоки, проекции, асинхронный демон |
| [Understanding Event Sourcing with Marten](https://www.martendb.io/events/learning) | JasperFx | Концептуальное введение |
| [Event Store to Kurrent Rebrand FAQ](https://kurrent.io/blog/kurrent-re-brand-faq) | Kurrent, 2024 | Переименование и сроки поддержки |
| [Introduction to Event Sourcing](https://event-driven.io/en/introduction_to_event_sourcing) | Oskar Dudycz | Основы без привязки к инструменту |
| [Comparing KurrentDB, Marten, and Cratis Chronicle](https://www.cratis.io/compare-event-sourcing-dotnet/) | Cratis (вендор), 2026 | Сравнение хранилищ; учитывайте, что автор — заинтересованная сторона |

## Архитектурные тесты

### Ключевые выводы

- Правила зависимостей, которые не проверяются в CI, со временем нарушаются. Архитектурный тест делает нарушение красной сборкой.
- **NetArchTest** — простой fluent API поверх рефлексии (`Types.InAssembly(...).ShouldNot().HaveDependencyOn(...)`). Его использует проект Grzybek. Оригинал фактически не развивается: последний релиз `NetArchTest.Rules` 1.3.2 — май 2021, последний коммит — 2023 год. Совместимый форк [NetArchTest.eNhancedEdition](https://github.com/NeVeSpl/NetArchTest.eNhancedEdition) (MIT, 1.4.5, июнь 2025) обновлялся позже, но тоже редко.
- **ArchUnitNET** (TNG) — порт Java ArchUnit с более богатой моделью (слои, срезы, циклы) и пакетами для xUnit v3, NUnit, MSTest и TUnit.
- Что проверять в первую очередь: домен не зависит от инфраструктуры и ASP.NET Core; модули не ссылаются на внутренности друг друга; обработчики `sealed` и `internal`; контроллеры и эндпоинты не ходят в `DbContext` напрямую (если так решено).
- Проектные ссылки уже дают половину гарантий. Тесты нужны для правил внутри проекта и для модулей, разделённых по namespace.

```csharp
// xUnit v3 + NetArchTest.Rules
public class LayerTests
{
    private static readonly Assembly Domain = typeof(Order).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_infrastructure_or_web()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MyApp.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [NetArchTest](https://github.com/BenMorris/NetArchTest) | Ben Morris | API и примеры правил |
| [ArchUnitNET](https://github.com/TNG/ArchUnitNET) | TNG Technology Consulting, живой | Слои, срезы, интеграции с фреймворками ([NuGet: TngTech.ArchUnitNET](https://www.nuget.org/packages/TngTech.ArchUnitNET)) |
| [5 Architecture Tests You Should Add](https://www.milanjovanovic.tech/blog/5-architecture-tests-you-should-add-to-your-dotnet-projects) | Milan Jovanović | Готовый набор правил |
| [Enforcing Software Architecture With Architecture Tests](https://milanjovanovic.tech/blog/enforcing-software-architecture-with-architecture-tests) | Milan Jovanović | Введение в подход |
| [Make Bad Architecture Fail the Build](https://www.dotnet-guide.com/tutorials/software-architecture/architecture-testing-dotnet/) | dotnet-guide, июль 2026 | Сравнение NetArchTest и ArchUnitNET |

## Ошибки по слоям

### Ключевые выводы

- Исключения — для исключительного: баги, нарушенные инварианты, недоступность инфраструктуры. Ожидаемые бизнес-исходы («товара нет», «email занят», ошибки валидации) моделируйте возвращаемым результатом. Так их не забудут обработать, и они не стоят как исключения.
- Тип результата: свой `Result<T>`/`Error` (20–50 строк) или библиотека (ErrorOr 2.1, Ardalis.Result 10.1, FluentResults 4.0, OneOf 3.0). Главное — один подход на всё решение. Нативные union-типы (`public union Pet(Cat, Dog, Bird);`) появляются в C# 15 вместе с .NET 11 (ноябрь 2026, в RC1 уже есть): компилятор проверяет полноту `switch` по вариантам. Для LTS-проектов на .NET 10 библиотеки остаются актуальными ([What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15), [Explore C# 15](https://devblogs.microsoft.com/dotnet/explore-csharp-15), август 2026).
- Домен и слой приложения возвращают доменные ошибки (код + сообщение + тип: Validation / NotFound / Conflict). HTTP-коды появляются только на краю: эндпоинт или фильтр переводит `Error.Type` в статус.
- На HTTP-границе ответ об ошибке — Problem Details (RFC 9457). В ASP.NET Core: `AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()`. Для типизированной обработки исключений с .NET 8 есть `IExceptionHandler`: обработчики выстраиваются в цепочку, `TryHandleAsync` возвращает `true`, если исключение обработано.
- Стек-трейсы и детали исключений наружу не отдавайте: Developer Exception Page — только в окружении Development. Подробности пишите в лог с trace id, а trace id кладите в Problem Details.
- Не ловите `Exception` в каждом слое ради логирования. Логирует граница (middleware или хост), а слои добавляют контекст или пробрасывают исключение дальше.
- В библиотеках бросайте стандартные исключения (`ArgumentException`, `InvalidOperationException`) или свои типы с понятной иерархией. `Result` в публичном API библиотеки навязывает потребителю вашу зависимость.

```csharp
internal sealed class DomainExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is not ConcurrencyConflictException) return false; // передать следующему обработчику
        ctx.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problems.TryWriteAsync(new() { HttpContext = ctx, Exception = ex,
            ProblemDetails = { Title = "Conflict", Detail = "Ресурс изменён другим запросом" } });
    }
}
// Program.cs
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
app.UseExceptionHandler();
app.UseStatusCodePages();
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api) | Microsoft Learn, 2026 | Нормативный источник: Problem Details, exception handler, status code pages |
| [RFC 9457: Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457) | IETF, 2023 | Формат ответа; заменяет RFC 7807 |
| [Global Error Handling in ASP.NET Core](https://milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-from-middleware-to-modern-handlers) | Milan Jovanović | От middleware к `IExceptionHandler` |
| [Problem Details for ASP.NET Core APIs](https://milanjovanovic.tech/blog/problem-details-for-aspnetcore-apis) | Milan Jovanović | Настройка `IProblemDetailsService` |
| [Use IExceptionHandler to handle exceptions](https://code-maze.com/dotnet-use-iexceptionhandler-to-handle-exceptions/) | Code Maze | Пошаговый пример цепочки обработчиков |

## Конфигурация, логирование, feature flags

### Ключевые выводы

- Конфигурацию привязывайте к типизированным классам через Options pattern (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`) и валидируйте при старте: `AddOptions<T>().BindConfiguration("Section").ValidateDataAnnotations().ValidateOnStart()`. Ошибка конфигурации должна ронять приложение на старте, а не на первом запросе.
- Для AOT и без рефлексии есть source-generated валидатор опций: атрибут `[OptionsValidator]` на пустом `partial class ValidateX : IValidateOptions<X>` генерирует проверку DataAnnotations при компиляции (Microsoft.Extensions.Options 8+); класс регистрируется как singleton `IValidateOptions<X>` ([документация](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator)).
- Секреты не храните в `appsettings.json`: локально используйте user-secrets, в проде — переменные окружения или хранилище секретов.
- Домен не читает `IConfiguration` напрямую: ему передают значения или опции. `IConfiguration` в конструкторе доменного класса — признак протечки инфраструктуры.
- Логирование и наблюдаемость — сквозной аспект хоста. Библиотеки пишут через `ILogger<T>`, `ActivitySource` и `Meter` и не конфигурируют провайдеры: экспорт OpenTelemetry настраивает приложение (в Aspire — ServiceDefaults). Для горячих путей используйте source-generated `[LoggerMessage]`.
- Время и случайность внедряйте как зависимости: `TimeProvider` (встроен с .NET 8) вместо `DateTime.UtcNow` делает тесты детерминированными.
- Feature flags: **Microsoft.FeatureManagement** берёт состояние из любого `IConfiguration`-провайдера (от `appsettings.json` до Azure App Configuration), поддерживает фильтры (процент, время, таргетинг) и варианты. Для ASP.NET Core есть отдельный пакет с атрибутами и фильтрами. Флаги — временный долг: у каждого должен быть владелец и срок удаления.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [OptionsBuilderExtensions.ValidateOnStart](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.optionsbuilderextensions.validateonstart) | Microsoft Learn | Валидация опций при старте |
| [Adding Validation To The Options Pattern](https://milanjovanovic.tech/blog/adding-validation-to-the-options-pattern-in-asp-net-core) | Milan Jovanović | Практика DataAnnotations и `IValidateOptions` |
| [Options Pattern Validation With FluentValidation](https://www.milanjovanovic.tech/blog/options-pattern-validation-in-aspnetcore-with-fluentvalidation) | Milan Jovanović | Сложные правила валидации конфигурации |
| [.NET feature management reference](https://learn.microsoft.com/en-us/azure/azure-app-configuration/feature-management-dotnet-reference) | Microsoft Learn, живой | Флаги, фильтры, варианты, телеметрия |
| [FeatureManagement-Dotnet](https://github.com/microsoft/FeatureManagement-Dotnet) | Microsoft | Исходники и README |
| [Feature Flags in .NET, from simple to more advanced](https://timdeschryver.dev/blog/feature-flags-in-net-from-simple-to-more-advanced) | Tim Deschryver | От `if` по конфигурации до FeatureManagement |

## Библиотека против приложения и версионирование API

### Ключевые выводы

- Приложение оптимизируется под изменяемость внутри одной команды. Публичный API библиотеки — обещание внешним потребителям: любое изменение сигнатуры — потенциальный binary breaking change. Добавление параметра к методу, например, вызывает `MissingMethodException` у уже скомпилированных потребителей.
- Версионируйте NuGet-пакет по SemVer. `AssemblyVersion` меняйте только на мажорных версиях: она важна для strong-named сборок и .NET Framework.
- Strong naming нужен, только если библиотека таргетирует .NET Framework или .NET Standard. Для библиотек только под современный .NET он не требуется.
- Включайте **Package Validation** (`<EnablePackageValidation>true</EnablePackageValidation>` + `PackageValidationBaselineVersion`): после `dotnet pack` он сравнивает пакет с прошлой версией и проверяет совместимость между target frameworks и runtime-специфичными сборками.
- **Microsoft.CodeAnalysis.PublicApiAnalyzers** фиксирует публичную поверхность в `PublicAPI.Shipped.txt` и `PublicAPI.Unshipped.txt`: любое новое публичное API видно в диффе PR, а случайно ставший публичным тип не пройдёт ревью.
- Библиотеки не тянут фреймворки приложения: никакого `IConfiguration`, глобального логгера или статического состояния. Предоставляйте `IServiceCollection`-расширения отдельным пакетом `*.DependencyInjection`, если DI нужен не всем.
- Мультитаргетинг (`netstandard2.0;net8.0`) расширяет аудиторию, но умножает матрицу тестов. Если потребители на современном .NET, таргетируйте только его.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Open-source library guidance: Get started](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/get-started) | Microsoft Learn, живой | Вход в официальное руководство для авторов библиотек |
| [Library guidance: Versioning](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning) | Microsoft Learn | Package / Assembly / File version: что и когда менять |
| [Library guidance: Breaking changes](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes) | Microsoft Learn | Source vs binary breaking changes |
| [Library guidance: Strong naming](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/strong-naming) | Microsoft Learn | Когда подписывать сборку |
| [Versioning in C#](https://learn.microsoft.com/en-us/dotnet/csharp/versioning) | Microsoft Learn | Языковые аспекты эволюции API |
| [Package validation overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/package-validation/overview) | Microsoft Learn | Три валидатора и настройка baseline |
| [Package Validation](https://devblogs.microsoft.com/dotnet/package-validation/) | .NET Blog, 2021 | Мотивация и примеры |
| [Detecting breaking changes between two versions of a NuGet package](https://meziantou.net/detecting-breaking-changes-between-two-versions-of-a-nuget-package-at-packaging.htm) | Gérald Barré (Meziantou) | Package validation на практике |
| [Microsoft.CodeAnalysis.PublicApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers) | dotnet/roslyn-analyzers | Учёт публичного API в текстовых файлах |

## Граф проектов и время сборки

### Ключевые выводы

- Каждый csproj добавляет вычисление MSBuild, restore и отдельную компиляцию. Десятки мелких проектов замедляют загрузку IDE и холодную сборку. Делите по границам (модули, слои, библиотеки), а не «на всякий случай».
- Длинные цепочки ссылок (A→B→C→D) сериализуют сборку. Широкий граф (общие контракты → независимые модули → хост) собирается параллельно.
- Reference assemblies (`ProduceReferenceAssembly`, по умолчанию включено в SDK-проектах) позволяют не пересобирать зависимые проекты, если не изменилась ref-сборка: тела методов и приватные члены в неё не входят, а internal-члены входят, только если у проекта есть `InternalsVisibleTo` ([Reference assemblies](https://learn.microsoft.com/en-us/dotnet/standard/assembly/reference-assemblies)). Ещё одна причина держать публичную поверхность проектов узкой.
- Source generators и анализаторы выполняются при каждой компиляции. Тяжёлый генератор в проекте-«основании» замедляет всё решение.
- Измеряйте: `dotnet build -bl` создаёт binlog, который открывается в [MSBuild Structured Log Viewer](https://msbuildlog.com/) (вкладка с таймингами проектов и целей).
- Тестовых проектов не должно быть больше, чем нужно: каждый — отдельная сборка и процесс. Типичный набор — `Domain.Tests` (unit), `Api.IntegrationTests`, `ArchitectureTests`, по желанию `E2E`.

## Стратегия тестирования

### Ключевые выводы

- Единица unit-теста — единица поведения, а не класс. Khorikov различает две школы. Лондонская изолирует класс от всех зависимостей моками. Классическая (Детройтская) изолирует тесты друг от друга и мокает только то, что выходит за процесс. Автор выбирает классическую: тесты, завязанные на моки внутренних классов, ломаются при каждом рефакторинге.
- Мокайте только неуправляемые внешние зависимости, взаимодействие с которыми видно снаружи: SMTP, шина сообщений, сторонний API. Собственную БД сервиса лучше поднимать по-настоящему (Testcontainers), а не мокать.
- Форма набора тестов для бизнес-сервиса на .NET ближе к «трофею», чем к пирамиде: много интеграционных тестов через HTTP с реальной БД в контейнере, unit-тесты для доменной логики с ветвлениями, немного E2E. Для библиотек и алгоритмов пирамида с упором на unit и property-тесты по-прежнему верна.
- Тестируйте через стабильную точку входа (публичный API модуля, HTTP-эндпоинт), а не через internal-детали. Тогда тесты переживают рефакторинг.
- Покрытие — индикатор, а не цель. Мутационное тестирование (Stryker.NET) честнее показывает, ловят ли тесты ошибки.
- Детерминизм: `TimeProvider` (и `FakeTimeProvider` в тестах), фиксированный seed, никаких `Thread.Sleep`. Нестабильный тест немедленно чинится или помещается в карантин.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Unit Testing: Principles, Practices, and Patterns — глава 1](https://enterprisecraftsmanship.com/files/Unit-Testing-Chapter-1-Excerpt.pdf) | Vladimir Khorikov, Manning 2020 | Главная книга по философии тестов на C#: две школы, что мокать, ценность теста |
| [Lessons from Unit Testing Principles, Practices, and Patterns](https://dzx.fr/blog/unit-testing-principles-practices-patterns/) | dzx.fr | Подробный конспект книги |
| [The Testing Trophy and Testing Classifications](https://kentcdodds.com/blog/the-testing-trophy-and-testing-classifications) | Kent C. Dodds, 2021 | Источник метафоры «трофея» (JS, но идея общая) |

## Тестовые фреймворки и платформа

### Ключевые выводы

- **Microsoft.Testing.Platform (MTP)** — лёгкая замена VSTest, встроенная в тестовый проект: тесты собираются в исполняемый файл и запускаются без `vstest.console`. Её поддерживают MSTest, NUnit, xUnit.net (v3 и v2) и TUnit; TUnit построен целиком на MTP. Целевые платформы — .NET 8+ и .NET Framework 4.6.2+. Пакеты расширений Microsoft с MTP 2.4 привязаны к текущей мажорной версии платформы, поэтому держите их на одной линии релизов.
- С .NET 10 SDK у `dotnet test` два режима: VSTest (прежний по умолчанию) и MTP, который включается в `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`) и требует MTP 1.7+. Переключатель в `global.json` действует на всё решение, но каждый тестовый проект должен сам поддерживать MTP (xUnit v3 — `UseMicrosoftTestingPlatformRunner`, MSTest — `EnableMSTestRunner`, NUnit — `EnableNUnitRunner`). Проект только с VSTest в режиме MTP даёт ошибку. С .NET 11 режим можно выбрать и переменной `DOTNET_TEST_RUNNER`.
- По замерам Meziantou (сентябрь 2026; xUnit v3 4.0.0, NUnit 4.6.1, MSTest 4.4.0, TUnit 1.65): «the runner matters more than the framework». Запуск через MTP-исполняемый файл быстрее `dotnet test` в 1.5–5 раз, а разница между фреймворками — десятки микросекунд на тест.
- **xUnit v3** — разумный выбор по умолчанию для нового проекта: новый экземпляр класса на тест, параллельность по умолчанию, точные сообщения об ошибках. **MSTest 4** тоже создаёт экземпляр на тест и быстр. **NUnit 4/5** по умолчанию делит один экземпляр на фикстуру (`[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` меняет это) и выполняет тесты последовательно. NUnit 5.0 (27 сентября 2026) эту модель не менял; его ломающие изменения — убранный target .NET 6, обязательный `await` у `Assert.ThrowsAsync`, другой смысл `[Platform("NET")]` ([NUnit 5 breaking changes](https://docs.nunit.org/articles/nunit/V5BreakingChanges.html)).
- **TUnit** — обнаружение тестов source generator'ом при сборке, Native AOT, параллельность по умолчанию, анализатор ловит ассерты, у которых забыли `await`, есть ассистент миграции с xUnit. Подходит для новых проектов. Переводить на него существующий набор стоит только при реальной боли.
- xUnit v2 [устарело] для новых проектов: развивается v3. Существующие наборы на NUnit или MSTest не переписывайте без причины — разница после включения параллельности невелика.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Microsoft.Testing.Platform overview](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro) | Microsoft Learn, октябрь 2026 | Принципы MTP, поддерживаемые фреймворки, MSBuild-интеграция |
| [Testing with dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test) | Microsoft Learn | Режимы VSTest и MTP, настройка через global.json |
| [MTP adoption across frameworks](https://devblogs.microsoft.com/dotnet/mtp-adoption-frameworks/) | .NET Blog, февраль 2025 | Состояние поддержки во фреймворках |
| [Microsoft Testing Platform in xUnit.net v3](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) | xUnit.net | `UseMicrosoftTestingPlatformRunner` |
| [Benchmarking .NET test frameworks](https://www.meziantou.net/benchmarking-dotnet-test-frameworks-xunit-v3-nunit-mstest-and-tunit.htm) | Gérald Barré, сентябрь 2026 | Замеры, вывод о роли раннера. Native AOT работает в xUnit v3, MSTest (но публикация 10 000 тестов шла 84 минуты) и TUnit; у NUnit поддержки AOT нет |
| [xUnit v3 vs NUnit vs MSTest in 2026](https://startdebugging.net/2026/08/xunit-v3-vs-nunit-vs-mstest-in-2026/) | Start Debugging, август 2026 | Жизненный цикл экземпляров, параллельность, подвохи NUnit-адаптера |
| [TUnit](https://tunit.dev/) | Tom Longhurst, живой | Документация, миграция, бенчмарки |
| [Converting an xUnit project to TUnit](https://andrewlock.net/converting-an-xunit-project-to-tunit) | Andrew Lock, август 2025 | Реальная миграция, сложности с TRX и Verify |

## Интеграционные тесты

### Ключевые выводы

- `WebApplicationFactory<Program>` (пакет Microsoft.AspNetCore.Mvc.Testing) поднимает приложение в памяти с `TestServer` и даёт `HttpClient`. Переопределяйте `ConfigureWebHost`, чтобы подменить внешние зависимости и строку подключения.
- Реальную БД поднимайте через **Testcontainers**: контейнер стартует в `IAsyncLifetime.InitializeAsync` фикстуры, строка подключения передаётся через `UseSetting` (порты динамические), тег образа закреплён. Провайдер EF Core InMemory как замена БД — [устарело]: он не воспроизводит SQL, транзакции и ограничения.
- Между тестами БД сбрасывает **Respawn**: удаляет данные в порядке внешних ключей, примерно в 3 раза быстрее, чем «отключить FK / truncate / включить FK», и намного быстрее пересоздания БД. Один контейнер на сборку тестов + Respawn перед тестом — хороший компромисс между скоростью и изоляцией.
- Для распределённого приложения на Aspire есть `DistributedApplicationTestingBuilder` (пакет Aspire.Hosting.Testing): он запускает AppHost со всеми ресурсами и проверяет систему как чёрный ящик. Для одного проекта в изоляции документация Aspire сама советует `WebApplicationFactory`.
- Аутентификацию в тестах подменяйте тестовым `AuthenticationHandler`, а не отключайте авторизацию: иначе политики останутся непроверенными.

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Main", _db.GetConnectionString());

    public async ValueTask InitializeAsync() => await _db.StartAsync();
    public override async ValueTask DisposeAsync() { await _db.DisposeAsync(); await base.DisposeAsync(); }
}
```

В xUnit v3 `IAsyncLifetime` наследует `IAsyncDisposable` и возвращает `ValueTask`. У `WebApplicationFactory` метод `DisposeAsync` виртуальный, поэтому достаточно `override`, как в примере.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests) | Microsoft Learn, живой | `WebApplicationFactory`, `TestServer`, подмена сервисов |
| [Testcontainers Best Practices for .NET](https://www.milanjovanovic.tech/blog/testcontainers-best-practices-dotnet-integration-testing) | Milan Jovanović | Жизненный цикл контейнеров, фикстуры, Respawn |
| [Integration tests against real SQL Server with Testcontainers](https://startdebugging.net/2026/05/how-to-write-integration-tests-against-real-sql-server-with-testcontainers/) | Start Debugging, май 2026 | `IAsyncLifetime`, `UseSetting`, закреплённые теги |
| [Testcontainers for .NET](https://dotnet.testcontainers.org/) | Testcontainers, живой | Модули для Postgres, SQL Server, Kafka и других |
| [Respawn 1.0.0 released](https://jimmybogard.com/respawn-1-0-0-released) | Jimmy Bogard | Как устроен сброс БД по графу FK |
| [Aspire testing overview](https://aspire.dev/testing/overview/) | Microsoft (aspire.dev), живой | `DistributedApplicationTestingBuilder` и когда выбрать `WebApplicationFactory` |

## Снапшоты, контракты, property-based и мутации

### Ключевые выводы

- **Verify** (Simon Cropp, с 2019) сериализует результат в файл `*.verified.*` и сравнивает при следующем запуске. Подходит для больших объектов, ответов API, сгенерированного кода, документов. Работает с xUnit, NUnit, MSTest, TUnit, у него 70+ пакетов-расширений (EF Core, ASP.NET Core, ImageSharp и другие). Снапшоты ревьюйте как код.
- **PactNet** — consumer-driven contract tests для HTTP и сообщений между сервисами: потребитель записывает ожидания, провайдер их проверяет. Пакет нужен, когда сервисы деплоятся независимо. В монолите те же гарантии дают обычные интеграционные тесты.
- **Property-based**: FsCheck (зрелый, есть C#-API и model-based тесты) и CsCheck (C#-first, быстрый shrinking на PCG, тесты линеаризуемости для конкурентного кода, встроенные сравнительные замеры). Хороши для парсеров, сериализации (round-trip), денежной арифметики и инвариантов агрегатов.
- **Stryker.NET** вносит мутации в код через синтаксическое дерево Roslyn и проверяет, падают ли тесты. Запускайте его на доменных проектах, а не на всём решении, и в CI как отчёт, а не блокирующий порог.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [VerifyTests/Verify](https://github.com/VerifyTests/Verify) | Simon Cropp, живой | Документация и расширения |
| [Snapshot Testing in .NET with Verify](https://blog.jetbrains.com/dotnet/2024/07/11/snapshot-testing-in-net-with-verify/) | JetBrains, 2024 | Введение и работа с diff-инструментами |
| [Verify — Open Source Sponsorship](https://duendesoftware.com/blog/20260409-verify-open-source-sponsorship) | Duende Software, апрель 2026 | Состояние проекта и его финансирование |
| [pact-foundation/pact-net](https://github.com/pact-foundation/pact-net) | Pact Foundation | Consumer и provider тесты |
| [Writing Better Tests Than Humans Can, Part 1: FsCheck](https://aaronstannard.com/fscheck-property-testing-csharp-part1/) | Aaron Stannard | FsCheck из C#; во второй части — model-based тесты |
| [Property-based testing with C#](https://www.codit.eu/blog/property-based-testing-with-c/) | Codit | Вводная статья |
| [Stryker.NET: Introduction](https://stryker-mutator.io/docs/stryker-net/introduction/) | Stryker Mutator, живой | Установка, конфигурация, уровни мутаций |
| [stryker-net на GitHub](https://github.com/stryker-mutator/stryker-net) | Stryker Mutator | Исходники и issues |
| [Mutation testing](https://learn.microsoft.com/en-us/dotnet/core/testing/mutation-testing) | Microsoft Learn | Официальное введение со Stryker.NET |

## Тестовые двойники и ассерты

### Ключевые выводы

- **Moq**: в августе 2023 года версия 4.20 добавила SponsorLink — закрытый код, который читал email из git config и отправлял его хэш в облако. После волны критики (AWS отозвал спонсорство) SponsorLink убрали: версия 4.20.2 (9 августа 2023, PR #1375), затем 4.20.69 (11 августа); PR #1402 (15 августа) окончательно откатил исходное изменение. Moq работает, но многие команды перешли на альтернативы из-за доверия. Версии 4.20.0–4.20.1 со SponsorLink сняты с листинга на NuGet — не используйте их; актуальная — 4.21.0.
- **NSubstitute** — лаконичный синтаксис, популярная замена Moq. **FakeItEasy** — тоже зрелый вариант. Выбор между ними — дело вкуса. Важнее мокать меньше (раздел о стратегии).
- Для времени — `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing), для логов — `FakeLogger` (Microsoft.Extensions.Diagnostics.Testing); оба пакета — 10.10. Самописные фейки часто проще мок-фреймворка.
- **Fluent Assertions 8** (январь 2025) распространяется по коммерческой лицензии Xceed ($130 за разработчика в год) для коммерческого использования. Версия 7 остаётся под Apache 2.0. Альтернативы: Shouldly (используется в шаблоне Jason Taylor), встроенные ассерты фреймворка, форк [AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions) (Apache 2.0, 9.6, август 2026) с почти тем же API.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Moq issue #1372: SponsorLink](https://github.com/devlooped/moq/issues/1372) | сообщество, август 2023 | Суть претензий к сбору данных |
| [Moq PR #1375: remove SponsorLink](https://github.com/devlooped/moq/pull/1375) / [PR #1402](https://github.com/devlooped/moq/pull/1402) | devlooped, август 2023 | Удаление SponsorLink и окончательный откат |
| [Moq criticized for quietly collecting data](https://yro.slashdot.org/story/23/08/09/2058205/popular-open-source-project-moq-criticized-for-quietly-collecting-data) | Slashdot, 2023 | Хронология скандала |
| [NSubstitute](https://nsubstitute.github.io/) | NSubstitute team | Документация |
| [FakeItEasy](https://fakeiteasy.github.io/) | FakeItEasy team | Документация |
| [Fluent Assertions shifts to restrictive license](https://www.devclass.com/development/2025/01/16/another-open-source-project-shifts-to-restrictive-license-fluent-assertions-following-xceed-partnership/1621343) | DevClass, январь 2025 | Условия лицензии v8 |
| [Fluent Assertions license changes](https://blog.inedo.com/proget/fluent-assertions-license-changes) | Inedo | Как учитывать лицензии пакетов в организации |

## Кодовые базы для изучения

### Ключевые выводы

- Сначала читайте README, ARCHITECTURE или CONTRIBUTING и дерево `src/`, затем `Directory.Build.props` и `Directory.Packages.props`. Так видно, как проект держит единые правила.
- Ищите границы: что публично в контрактных проектах, где composition root, как устроены тестовые проекты и фикстуры.
- Шаблоны (Jason Taylor, Ardalis) показывают намерение, а продуктовые кодовые базы — что выживает под нагрузкой реальных требований.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [dotnet/eShop](https://github.com/dotnet/eShop) | Microsoft | Сервисная архитектура на Aspire, доменная модель Ordering, EventBus, E2E на Playwright |
| [modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd) | Kamil Grzybek | Модульный монолит, DDD, Outbox/Inbox, архитектурные тесты, четыре вида тестов |
| [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) | Jason Taylor | Clean Architecture-шаблон, NUnit + Shouldly + Respawn |
| [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) | Steve Smith | Clean + FastEndpoints, slnx, CPM, ADR |
| [Jellyfin](https://github.com/jellyfin/jellyfin) | Jellyfin | Большое серверное приложение, выросшее из форка Emby (2018): видно, как рефакторят унаследованную структуру |
| [Bitwarden server](https://github.com/bitwarden/server) | Bitwarden | Продакшен-сервер с несколькими хостами (Api, Identity, Admin, Billing, Events, Notifications), общим Core и двумя реализациями инфраструктуры (Dapper и EF Core) |
| [Orchard Core](https://github.com/OrchardCMS/OrchardCore) | Orchard | Модульность и мультитенантность на уровне фреймворка |
| [ABP Framework](https://github.com/abpframework/abp) | Volosoft | Фреймворк модулей и DDD-слоёв. Показателен, но тяжёл как зависимость |
| [nopCommerce](https://github.com/nopSolutions/nopCommerce) | nopSolutions | Долгоживущая e-commerce-платформа с плагинами |
| [Marten](https://github.com/JasperFx/marten) / [Wolverine](https://github.com/JasperFx/wolverine) | JasperFx | Библиотеки с кодогенерацией, обширные интеграционные тесты на Postgres |
| [MassTransit](https://github.com/MassTransit/MassTransit) | Chris Patterson | Абстракции транспорта, саги, тестовый harness (v9 — коммерческая) |
| [Npgsql](https://github.com/npgsql/npgsql) | Npgsql | Высокопроизводительная библиотека с аккуратным публичным API |
| [Roslyn](https://github.com/dotnet/roslyn) | Microsoft | Родина PublicApiAnalyzers, слои компилятора и IDE |
| [dotnet/runtime](https://github.com/dotnet/runtime) | Microsoft | Ref-сборки, API review, контроль совместимости в масштабе |

## Где источники расходятся

### Ключевые выводы

- Большинство разногласий — это компромисс между церемониями и защитой от будущих изменений. Выбирайте по размеру домена и сроку жизни системы.

| Вопрос | Позиция A | Позиция B | Рекомендация по умолчанию |
|---|---|---|---|
| MediatR после смены лицензии | Jason Taylor: оставить и заплатить, стоимость несущественна ([обсуждение](https://github.com/jasontaylordev/CleanArchitecture/discussions/1346)) | Start Debugging, Milan: обычные сервисы или альтернативы ([статья](https://startdebugging.net/2026/05/mediatr-vs-plain-service-classes-in-2026/)) | Обычные обработчики, внедряемые через DI. Если нужен pipeline — Mediator (source-gen) или Wolverine. MediatR — только с понятной лицензией |
| Слои или срезы | Clean-шаблоны: проекты по слоям | Bogard: срезы по запросам ([статья](https://www.jimmybogard.com/vertical-slice-architecture/)) | Модульный монолит со срезами внутри модуля. Отдельный проект `Domain` — только при богатом домене |
| Тестовый фреймворк | xUnit v3 по умолчанию ([статья](https://startdebugging.net/2026/08/xunit-v3-vs-nunit-vs-mstest-in-2026/)) | TUnit как современный выбор ([Andrew Lock](https://andrewlock.net/converting-an-xunit-project-to-tunit)) | xUnit v3 на MTP для нового кода. TUnit допустим. Существующий набор не мигрировать без причины: раннер важнее фреймворка |
| Мокать или нет | Лондонская школа: моки всех зависимостей | Khorikov: моки только для внешних неуправляемых зависимостей | Классическая школа. Своя БД — через Testcontainers |
| Исключения или Result | Исключения везде (просто, привычно) | Result для ожидаемых ошибок | Result для бизнес-исходов в приложении, исключения для багов и инфраструктуры. В публичном API библиотек — исключения |
| Репозиторий поверх EF Core | Нужен для изоляции домена | `DbContext` уже репозиторий и Unit of Work | Без generic-репозиториев. Репозиторий агрегата — когда он скрывает загрузку графа или нужен порт для другого хранилища |
| Fluent Assertions | Оставаться на v7 (Apache 2.0) | Платить за v8 или уходить на Shouldly | Новые проекты — Shouldly или встроенные ассерты. Существующие — зафиксировать v7 через CPM |

## Анти-паттерны

### Ключевые выводы

- **Шаблон Clean Architecture для CRUD на пять таблиц**: четыре проекта, интерфейс на каждый класс, DTO и маппинг на каждой границе. Начинайте проще и выделяйте слои, когда появится доменная логика.
- **Анемичная модель под видом DDD**: сущности с публичными сеттерами и вся логика в «сервисах». Инварианты должны жить в агрегате.
- **Проект `Shared`/`Common`, на который ссылаются все**: скрытая связность модулей. Общими делайте только контракты и действительно общие примитивы.
- **Generic `IRepository<T>` + `IUnitOfWork` поверх EF Core**: дублирует `DbContext`, прячет `Include`/`AsNoTracking`, мешает оптимизации запросов.
- **Медиатор ради медиатора**: каждый вызов через `Send` без pipeline-поведений — косвенность без пользы, хуже навигация и диагностика.
- **Общая база данных между модулями или сервисами**: ломает границы сильнее любой ссылки в коде. Каждый модуль владеет своей схемой.
- **Распределённый монолит**: микросервисы, которые деплоятся вместе и синхронно вызывают друг друга по цепочке.
- **EF Core InMemory вместо реальной БД в интеграционных тестах** [устарело]: тесты проходят, а прод падает на ограничениях и SQL.
- **Моки внутренних классов и проверка последовательности вызовов**: тесты фиксируют реализацию и ломаются при рефакторинге.
- **`InternalsVisibleTo` на всё подряд и тестирование приватных деталей**.
- **Версии пакетов в каждом csproj**: расхождение версий и «ад биндингов». Используйте CPM.
- **Исключения для управления потоком в бизнес-логике и `catch (Exception)` в каждом слое**.
- **Конфигурация, прочитанная в домене** (`IConfiguration` в сущностях и сервисах домена) и отсутствие `ValidateOnStart`.
- **Стек-трейсы в HTTP-ответах** в продакшене вместо Problem Details с trace id.
- **Feature flags без срока жизни**: мёртвые ветки кода на годы.
- **Зависимость от библиотек со сменившейся лицензией без учёта**: обновились на MediatR 13, FluentAssertions 8 или MassTransit 9 и не заметили обязательств. Проверяйте лицензии при обновлении пакетов.

## Чеклист: структура нового решения

### Ключевые выводы

- Начинайте с простейшего уровня и переходите к следующему по сигналу (размер домена, число команд, требования к деплою), а не заранее.
- Переход дешевле, если с первого дня включены CPM, `Directory.Build.props`, nullable, анализаторы и есть интеграционные тесты через HTTP.

| Уровень | Когда | Что сделать |
|---|---|---|
| Один проект + тесты | Небольшой сервис, утилита, прототип; одна команда | `.slnx`, `src/App`, `tests/App.Tests`. `Directory.Build.props` (net10.0, Nullable, анализаторы, warnings as errors в CI), `Directory.Packages.props`, `global.json` с MTP-раннером. Папки по фичам (вертикальные срезы). Options с `ValidateOnStart`, Problem Details, `TimeProvider`. Тесты: xUnit v3, `WebApplicationFactory` + Testcontainers + Respawn |
| Модульный монолит | Бизнес-приложение со многими областями, несколько разработчиков | Модуль = папка или набор проектов с публичным контрактом и своей схемой БД. Интеграционные события и Outbox между модулями, без общих транзакций. Внутри модуля — срезы, при богатом домене — отдельный `Domain`. Архитектурные тесты (NetArchTest или ArchUnitNET) на границы модулей. Aspire AppHost для локальной инфраструктуры. Verify для сложных ответов |
| Распределённая система | Независимый деплой и масштабирование, несколько команд | Выделять сервисы из модулей по мере необходимости. Aspire для оркестрации и телеметрии, `DistributedApplicationTestingBuilder` для системных тестов, PactNet для контрактов между командами. Шина: Wolverine, Rebus или клиенты брокеров (MassTransit v9 — с лицензией). Event sourcing (Marten или KurrentDB) — только для модулей, где важна история |
| Публикуемая библиотека | Пакет на nuget.org или во внутреннем фиде | Узкий публичный API, `internal` по умолчанию. PublicApiAnalyzers, Package Validation с baseline-версией, SemVer, strong naming только при таргете .NET Framework или .NET Standard. Без `IConfiguration` и глобального состояния, логирование через `ILogger`. Мультитаргетинг только при реальной аудитории. Property-based тесты, Stryker на ядре |

---
name: csharp-knowledge
description: Knowledge base and workflow for writing, reviewing and optimizing C# and .NET code in any domain — libraries and NuGet packages, ASP.NET Core web APIs and microservices, data access with EF Core, cloud and serverless, desktop (WPF, WinUI, Avalonia, MAUI), mobile, CLI tools, games (Unity, Godot, MonoGame), AI/ML, IoT. Use when writing or reviewing C#, structuring a .NET solution, choosing NuGet packages or dotnet tooling (analyzers, dotnet format, xUnit/MSTest/TUnit, Microsoft.Testing.Platform, BenchmarkDotNet, dotnet-trace), benchmarking, profiling or optimizing .NET performance and allocations, working with async/concurrency/unsafe/P/Invoke, or picking C#/.NET books and articles. Includes a curated library of books, articles and patterns with links, a toolchain guide, and a development pipeline with benchmarks, code review and evidence-based optimization.
---

# C# и .NET: библиотека знаний и пайплайн

Скилл помогает писать, проверять и ускорять код на C# в любой предметной области. Внутри три вещи:

1. **Библиотека знаний.** Книги, статьи, доклады, кодовые базы и паттерны по категориям. Каждый источник дан со ссылкой, автором, годом и пояснением, зачем его читать.
2. **Тулчейн.** Какие инструменты ставить, какие команды запускать и в каком порядке.
3. **Пайплайн.** Порядок работы: тесты до кода, проверки качества, бенчмарки, ревью, поиск решений в библиотеке и повторный замер.

Состояние знаний — октябрь 2026: .NET 10 LTS (SDK 10.0.4xx) и C# 14; .NET 11 с C# 15 в стадии RC, выход ожидается в ноябре 2026.

## Куда смотреть

| Задача | Файл |
| --- | --- |
| Новая фича, рефакторинг или оптимизация: порядок работы | [references/pipeline.md](references/pipeline.md) |
| Инструменты, команды, анализаторы, тесты, CI, шаблоны `Directory.Build.props` и `.editorconfig` | [references/toolchain.md](references/toolchain.md) |
| Код медленный или много аллоцирует: техника по симптому, профилирование, бенчмарки | [references/performance.md](references/performance.md) — начните с таблицы «Симптом → техника → источник» |
| Возможности языка C# 12–15, records, pattern matching, nullable, ошибки, DI, паттерны, чеклист ревью | [references/idioms-patterns.md](references/idioms-patterns.md) |
| Структура решения, слои, clean/vertical slice/модульный монолит, DDD, CQRS, стратегия тестирования | [references/architecture.md](references/architecture.md) |
| async/await, `Task`/`ValueTask`, отмена, блокировки, каналы, `unsafe`, `Span`, P/Invoke, Native AOT interop | [references/concurrency-async-unsafe.md](references/concurrency-async-unsafe.md) |
| Серверные области: ASP.NET Core, EF Core и данные, сообщения, gRPC, Blazor, облако, фоновые задачи, наблюдаемость, безопасность | [references/domains-server.md](references/domains-server.md) |
| Клиентские и прочие области: десктоп, мобильные, CLI, NuGet-библиотеки, игры, AI/ML, IoT, interop, Roslyn | [references/domains-client.md](references/domains-client.md) |
| Что почитать, блоги, маршрут обучения по уровням | [references/books.md](references/books.md) |

Каждый справочник начинается с оглавления. Читайте только нужный раздел, а не файл целиком.

Скрипт [scripts/compare-bdn.cs](scripts/compare-bdn.cs) сравнивает два прогона BenchmarkDotNet и возвращает код 1 при регрессии. Он нужен на этапах 7 и 11 пайплайна.

## Как работать

**Для задачи больше одного метода** идите по [пайплайну](references/pipeline.md):

1. Опрос.
2. План и спецификация.
3. Подзадачи.
4. Тесты до кода.
5. Изоляция в worktree.
6. Бенчмарки.
7. Базовый замер и профиль.
8. Ревью.
9. Поиск решений в библиотеке.
10. Оптимизация.
11. Повторный замер.

Для мелкой правки хватает короткого режима из того же файла: тест, код, проверки, ревью.

**Перед тем как объявить работу готовой**, прогоните минимальные проверки и покажите их вывод:

```bash
dotnet format --verify-no-changes
dotnet build -c Release -p:TreatWarningsAsErrors=true
dotnet test -c Release --no-build
dotnet package list --vulnerable --include-transitive --format json   # в выводе не должно быть "severity"
```

Если в проекте EF Core, добавьте `dotnet ef migrations has-pending-model-changes`. Для публикуемой библиотеки включите Package Validation. Для приложения под Native AOT соберите `dotnet publish -p:PublishAot=true` и проверьте, что нет новых предупреждений IL2xxx и IL3xxx. Если инструмент не установлен, скажите об этом и предложите команду установки из [toolchain.md](references/toolchain.md), а не пропускайте шаг молча.

**При оптимизации:**

- Сначала замер, потом правка. Без базовой линии нельзя сказать, что код стал быстрее.
- Смотрите и на время, и на аллокации (`[MemoryDiagnoser]`).
- Ищите технику по симптому в [performance.md](references/performance.md) и указывайте источник рядом с правкой.
- Принимайте правку, только если бенчмарк показал значимый выигрыш, а тесты остались зелёными.

**При ревью** используйте чеклисты:

- [idioms-patterns.md](references/idioms-patterns.md) — для любого кода;
- [concurrency-async-unsafe.md](references/concurrency-async-unsafe.md) — для асинхронного, конкурентного и `unsafe`-кода;
- [architecture.md](references/architecture.md) — для структуры решения и границ слоёв.

## Правила использования библиотеки

- **Ссылайтесь на источник.** Рекомендуя паттерн или оптимизацию, давайте ссылку из справочника. Тогда пользователь может проверить совет, а решение не выглядит выдуманным.
- **Учитывайте пометки.** `[устарело]` значит, что у материала есть замена, и она указана рядом. `[не проверено]` значит, что утверждение взято из одного вторичного источника; его стоит перепроверить, прежде чем строить на нём решение.
- **Проверяйте версии и лицензии.** Версии пакетов и SDK указаны на октябрь 2026. Несколько популярных библиотек (MediatR, AutoMapper, MassTransit, FluentAssertions) в 2025 году перешли на коммерческие лицензии, подробности — в [idioms-patterns.md](references/idioms-patterns.md). Перед важными решениями сверяйтесь с nuget.org, learn.microsoft.com и release notes.
- **Учитывайте целевую платформу.** Совет для .NET 10 может не работать на .NET Framework, в Unity (Mono, C# 9) или в библиотеке под `netstandard2.0`. Уточните TFM проекта, прежде чем советовать новые API и возможности языка.
- **Говорите о пробелах прямо.** Если в библиотеке нет ответа, так и скажите, а затем ищите в официальной документации. Не выдавайте догадку за материал библиотеки.
- **Следуйте соглашениям проекта.** Если в проекте уже приняты стиль, библиотеки или архитектура, советы библиотеки применяются в их рамках, а не поверх.

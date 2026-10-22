---
name: csharp-knowledge
description: Knowledge base and workflow for C# and .NET in any domain — libraries and NuGet, ASP.NET Core and EF Core, cloud, desktop (WPF, WinUI, Avalonia, MAUI), mobile, CLI, games (Unity, Godot, MonoGame), AI/ML, IoT. Use for any question about C# or .NET code, even one that looks quick to answer from memory: writing or reviewing C#; compiler errors and warnings (CS8618, CS1998, CS4014) and runtime exceptions (Collection was modified, ObjectDisposedException); struct vs class, records, nullable; choosing between types or primitives (ConcurrentDictionary vs lock, FrozenDictionary, Span, ValueTask); solution structure and slow builds; dotnet tooling (analyzers, dotnet format, xUnit/MSTest/TUnit, BenchmarkDotNet, dotnet-trace); slow code, allocations, GC pauses, memory growth; async hangs, deadlocks, thread-pool starvation, .Result/.Wait(); P/Invoke, unsafe, Native AOT, startup time; C#/.NET books. Includes a curated library with links, a toolchain guide and a pipeline with benchmarks, review and measured optimization.
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
| Минимальный набор инструментов и порядок шагов CI | [toolchain.md → §11](references/toolchain.md#11-минимальный-набор-и-инструменты-по-ситуации) и [§12](references/toolchain.md#12-порядок-ci-пайплайна-и-workflow) |
| Инструменты, команды, анализаторы, тесты, шаблоны `Directory.Build.props` и `.editorconfig` | [references/toolchain.md](references/toolchain.md) |
| Код медленный или много аллоцирует: техника по симптому, профилирование, бенчмарки | [references/performance.md](references/performance.md) — начните с таблицы «Симптом → техника → источник» |
| Возможности языка C# 12–15, records, pattern matching, nullable, ошибки, DI, паттерны, чеклист ревью | [references/idioms-patterns.md](references/idioms-patterns.md) |
| Структура решения, слои, clean/vertical slice/модульный монолит, DDD, CQRS, стратегия тестирования | [references/architecture.md](references/architecture.md) |
| async/await, `Task`/`ValueTask`, отмена, блокировки, каналы, `unsafe`, `Span`, P/Invoke, Native AOT interop | [references/concurrency-async-unsafe.md](references/concurrency-async-unsafe.md) |
| Серверные области: ASP.NET Core, EF Core и данные, сообщения, gRPC, Blazor, облако, фоновые задачи, наблюдаемость, безопасность | [references/domains-server.md](references/domains-server.md) |
| Клиентские и прочие области: десктоп, мобильные, CLI, NuGet-библиотеки, игры, AI/ML, IoT, interop, Roslyn | [references/domains-client.md](references/domains-client.md) |
| Что почитать: сначала [«Быстрый выбор по задаче»](references/books.md#быстрый-выбор-по-задаче) и [«Must-read»](references/books.md#must-read-десять-книг), остальное — по запросу | [references/books.md](references/books.md) |

Справочники большие, 25–90 КБ каждый, и целиком их не читают. Сначала найдите заголовки (`Grep "^#"` по файлу), затем читайте `Read` с `offset` и `limit` только нужный раздел.

Скрипт [scripts/compare-bdn.cs](scripts/compare-bdn.cs) сравнивает два прогона BenchmarkDotNet и возвращает код 1 при регрессии. Он нужен на этапах 7 (A/A) и 11 пайплайна.

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
- До первой правки снимите базовую линию дважды и сравните прогоны (A/A). Так вы узнаете шум машины. Выигрыш меньше двух порогов перепроверяйте повторным прогоном.
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

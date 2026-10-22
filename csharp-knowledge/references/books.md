# Книги и ресурсы по C#/.NET: каталог и маршрут чтения

Аннотированный каталог книг, бесплатной документации, блогов, докладов и рассылок по C# и .NET для тех, кто пишет быстрый, идиоматичный и хорошо устроенный код в любой предметной области: библиотеки, веб-бэкенды, сервисы, десктоп и мобильные приложения, инструменты. Здесь отмечены обязательные книги, полка «по ситуации» и бесплатные источники. Указано, какие издания обновлены под современный .NET, а какие написаны в эпоху .NET Framework и что в них по-прежнему верно. В конце дан маршрут чтения по уровням. Состояние на октябрь 2026 года: актуальный LTS — .NET 10 с C# 14 (ноябрь 2025), .NET 11 (STS) и C# 15 выходят 10 ноября 2026 года, пока доступна версия RC1 (8 сентября 2026); поддержка .NET 8 и .NET 9 заканчивается 10 ноября 2026 года.

- [Как выбирать книгу](#как-выбирать-книгу)
- [Must-read: десять книг](#must-read-десять-книг)
- [Бесплатные онлайн-книги и официальная документация](#бесплатные-онлайн-книги-и-официальная-документация)
- [Полка «по ситуации»](#полка-по-ситуации)
- [Что устарело в старых книгах](#что-устарело-в-старых-книгах)
- [Блоги и люди](#блоги-и-люди)
- [Доклады, подкасты и рассылки](#доклады-подкасты-и-рассылки)
- [Маршрут чтения по уровням](#маршрут-чтения-по-уровням)
- [Каталоги и навигаторы](#каталоги-и-навигаторы)

**Как читать этот файл.** Файл большой, около 60 КБ, целиком его не читают. На вопрос «что почитать по теме X» обычно хватает таблицы «Быстрый выбор по задаче» (в первом разделе, около 15 строк) и строк по теме, найденных через `Grep` по ключевому слову (например, `GC|память|Kokosa`, `async|Cleary`, `DDD`). Остальные разделы открывайте, только когда нужен полный обзор, маршрут по уровням или список блогов.

## Как выбирать книгу

### Ключевые выводы

- Книг по C# 13/14 мало. Под .NET 10 и C# 14 подтверждённо обновлены только книги Mark J. Price (ноябрь 2025 — февраль 2026). Справочник Albahari остановился на C# 12 / .NET 8 (2023), а нового издания «in a Nutshell» на октябрь 2026 года не найдено.
- Возраст книги по C# вредит меньше, чем кажется. Язык развивается добавлением возможностей, а не ломкой старых. Сильнее всего стареют прикладные части: API ASP.NET Core и EF Core, хостинг, конфигурация, деплой.
- Главный водораздел — .NET Framework против .NET (Core). Всё, что написано до 2016–2019 годов про AppDomain, CAS, remoting, `web.config`, `SynchronizationContext` в ASP.NET или конфигурацию GC через `app.config`, к современному .NET не относится.
- Новые возможности языка (records, nullable reference types, pattern matching, primary constructors, collection expressions, `field`, extension members) изучайте по [What's new in C#](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) и proposals в [csharplang](https://github.com/dotnet/csharplang/tree/main/proposals), а не по книгам.
- По производительности самый свежий и самый точный источник — не книги, а ежегодные статьи Stephen Toub «Performance Improvements in .NET N» и документация. Книги дают модель мышления (GC, аллокации, методика измерений), статьи — актуальные цифры.
- Для архитектуры, DI, тестирования и DDD язык и версия .NET почти не важны: книги 2015–2021 годов там остаются эталоном.
- Packt выпускает много книг быстро: сверяйте автора и отзывы. Manning, O'Reilly, Addison-Wesley и Apress в среднем надёжнее, но тоже не обновляются каждый релиз.

### Быстрый выбор по задаче

| Задача | Первый источник | Второй источник |
|---|---|---|
| Выучить язык с нуля | Price, C# 14 and .NET 10 | Tour of C# и Microsoft Learn |
| Найти точный ответ про язык или BCL | C# 12 in a Nutshell | документация learn.microsoft.com |
| Понять, почему язык устроен так | C# in Depth 4e | csharplang proposals |
| Спроектировать публичную библиотеку | Framework Design Guidelines 3e | анализаторы CAxxxx |
| Писать полезные тесты | Khorikov, Unit Testing PP&P | Seemann, Code That Fits in Your Head |
| Развязать зависимости | van Deursen и Seemann, DI PP&P | документация MS.DI |
| Ускорить код | Akinshin, Pro .NET Benchmarking (сначала измерить) | Kokosa и др., Pro .NET Memory Management 2e; статьи Toub |
| Разобраться с async и потоками | Toub, How Async/Await Really Works | Cleary, Concurrency in C# Cookbook 2e |
| Веб-сервис на ASP.NET Core | Lock, ASP.NET Core in Action 3e | документация ASP.NET Core 10 |
| Сложная предметная область | Khononov, Learning DDD | Evans; Millett (примеры на C#) |

## Must-read: десять книг

### Ключевые выводы

- C# 12 in a Nutshell — главный справочник по языку, CLR и BCL. Для C# 13–14 дополняйте его документацией Microsoft.
- C# in Depth объясняет, почему язык устроен так, как устроен. Издание 2019 года, поэтому читайте его ради глубины, а не ради новинок.
- Pro .NET Memory Management 2e (2024, .NET 8) — главная книга про GC и память, обязательна для любой работы над производительностью.
- Pro .NET Benchmarking — методика измерений. Без неё легко «оптимизировать» шум.
- Concurrency in C# Cookbook 2e — практические рецепты async, параллелизма, Dataflow и Channels-подобных конвейеров.
- Framework Design Guidelines 3e — канон дизайна публичных API в .NET, основа правил анализаторов CAxxxx.
- DI PP&P и Unit Testing PP&P — две книги, после которых код становится тестируемым, а тесты полезными.
- Code That Fits in Your Head — эвристики устойчивой разработки на сквозном примере C#-сервиса.
- C# 14 and .NET 10 (Price) — единственная must-read книга, обновлённая под актуальный LTS.

| Ресурс | Автор, год | Издание, доступ | Уровень | Зачем читать: главы для производительности, паттернов, архитектуры |
|---|---|---|---|---|
| [C# 12 in a Nutshell](https://www.albahari.com/nutshell/) | Joseph Albahari, ноябрь 2023 | O'Reilly, 1083 стр., C# 12 / .NET 8 ([Amazon](https://www.amazon.com/C-12-Nutshell-Definitive-Reference/dp/1098147448)); платно. Нового издания (C# 13/14) не найдено | средний → продвинутый | Самый полный справочник: язык целиком, коллекции, LINQ и деревья выражений, освобождение ресурсов и GC, конкурентность и async, потоки ввода-вывода, рефлексия и emit, `Span<T>`/`Memory<T>`, нативный interop, регулярные выражения. Главы про продвинутую многопоточность и `Span<T>` прямо нужны для производительности |
| [C# in Depth, 4th ed.](https://www.manning.com/books/c-sharp-in-depth-fourth-edition) | Jon Skeet, март 2019 | Manning; платно; сайт [csharpindepth.com](https://csharpindepth.com/). 5-го издания нет | средний → продвинутый | Как и почему эволюционировал язык C# 2–7: generics, итераторы, LINQ и деревья выражений, замыкания, глава об async как конечном автомате, кортежи, `ref`-локали, введение в `Span<T>`. Всего, начиная с C# 8, нет |
| [Pro .NET Memory Management, 2nd ed.](https://link.springer.com/book/9798868804526) | Konrad Kokosa, Christophe Nasarre, Kevin Gosse, октябрь 2024 | Apress, ~760 стр. по данным Springer, «updated to .NET 8» ([сайт книги](https://prodotnetmemory.com/)); платно | продвинутый | Аллокации, разбиение кучи (SOH, LOH, POH), четыре главы о фазах GC, режимы GC (workstation/server, DATAS), финализация и `IDisposable`, слабые ссылки и хэндлы, измерения через `dotnet-trace`/`dotnet-gcdump`/PerfView, `Span<T>`, `stackalloc`, unsafe и пулы |
| [Pro .NET Benchmarking](https://link.springer.com/book/10.1007/978-1-4842-4941-3) | Andrey Akinshin, июнь 2019 | Apress, 662 стр.; платно | средний → продвинутый | Автор [BenchmarkDotNet](https://benchmarkdotnet.org/). Ловушки бенчмарков, влияние окружения, статистика для инженеров, тесты производительности в CI, CPU-bound и memory-bound бенчмарки, таймеры. Не стареет |
| [Concurrency in C# Cookbook, 2nd ed.](https://www.oreilly.com/library/view/concurrency-in-c/9781492054498/) | Stephen Cleary, август 2019 | O'Reilly, 254 стр., C# 8; платно | средний | Более 85 рецептов: async/await, асинхронные потоки `IAsyncEnumerable`, TPL и `Parallel`, TPL Dataflow, System.Reactive, потокобезопасные и неизменяемые коллекции, тестирование конкурентного кода, отмена, async-совместимое ООП. `Channel<T>` и `Parallel.ForEachAsync` дополняйте документацией |
| [Framework Design Guidelines, 3rd ed.](https://www.pearson.com/en-us/subject-catalog/p/framework-design-guidelines-conventions-idioms-and-patterns-for-reusable-net-libraries/P200000000204/9780135896327) | Krzysztof Cwalina, Jeremy Barton, Brad Abrams, июнь 2020 | Addison-Wesley; платно; сжатая версия [бесплатно онлайн](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) | средний → продвинутый | Именование, выбор между классом и структурой, дизайн членов (свойства против методов, перегрузки), исключения, коллекции в API, `IDisposable`, async-API, `Span`-API. Комментарии авторов BCL объясняют, почему правило такое |
| [Dependency Injection Principles, Practices, and Patterns](https://www.manning.com/books/dependency-injection-principles-practices-patterns) | Steven van Deursen, Mark Seemann, 2019 | Manning, 552 стр.; платно. Переработка «Dependency Injection in .NET» (2011) | средний | Composition root, время жизни и captive dependencies, антипаттерны (Service Locator, Ambient Context, Control Freak), декораторы и перехват, Pure DI против контейнеров (MS.DI, Autofac, Simple Injector) |
| [Unit Testing Principles, Practices, and Patterns](https://www.manning.com/books/unit-testing) | Vladimir Khorikov, январь 2020 | Manning; платно; примеры на C# | средний | Четыре свойства хорошего теста, классическая школа против лондонской, тесты на выход, состояние и взаимодействие, моки только для неуправляемых внешних зависимостей, интеграционные тесты с реальной БД, антипаттерны. 2-го издания нет |
| [Code That Fits in Your Head](https://www.pearson.com/en-gb/subject-catalog/p/code-that-fits-in-your-head-heuristics-for-software-engineering/P200000009593/9780137464357) | Mark Seemann, ноябрь 2021 | Addison-Wesley; платно ([анонс и содержание у автора](https://blog.ploeh.dk/2021/06/14/new-book-code-that-fits-in-your-head/), 2021) | средний → продвинутый | Эвристики на сквозном C#-проекте (API бронирования): walking skeleton, чеклисты, инкапсуляция и инварианты, декомпозиция «по размеру головы», дизайн API, тесты, разбор ошибок, командная работа |
| [C# 14 and .NET 10 – Modern Cross-Platform Development Fundamentals, 10th ed.](https://www.packtpub.com/en-us/product/c-14-and-net-10-modern-cross-platform-development-fundamentals-9781836206620) | Mark J. Price, ноябрь 2025 | Packt, 828 стр., C# 14, .NET 10, Visual Studio 2026; платно | начальный → средний | Самая свежая обзорная книга: язык до C# 14 (`field`, partial-конструкторы и события, extension members), ООП, тесты и отладка, BCL, файлы и сериализация, LINQ, EF Core, ASP.NET Core 10, Blazor, Minimal API, Native AOT |

## Бесплатные онлайн-книги и официальная документация

### Ключевые выводы

- Документация Microsoft — первоисточник для всего, что вышло после 2023 года. Начинайте с [What's new в C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) и [в .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview).
- Статьи Stephen Toub равны по объёму книгам. «How Async/Await Really Works» заменяет любую устаревшую главу об async, «Performance Improvements in .NET 10» и вышедшая 15 сентября 2026 года «Performance Improvements in .NET 11» — обзоры того, что стало быстрее и почему.
- Book of the Runtime (BOTR) — внутреннее устройство CoreCLR от его авторов. Читайте выборочно: GC, Type System, Threading, Exceptions, Vectors and Intrinsics.
- Стандарт ECMA-334 отстаёт: 7-е издание (декабрь 2023) описывает C# 7. Для C# 8+ нормативный текст — черновики [csharpstandard](https://github.com/dotnet/csharpstandard) и спецификации-proposals в csharplang.
- Бесплатные архитектурные e-книги Microsoft написаны под .NET 5–7. Принципы верны, но примеры кода и инфраструктура (eShopOnContainers, Docker Compose вместо Aspire) частично устарели.

| Ресурс | Автор, год | Издание, доступ | Уровень | Зачем читать |
|---|---|---|---|---|
| [Документация C#](https://learn.microsoft.com/en-us/dotnet/csharp/) и [Tour of C#](https://learn.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/) | Microsoft, живой | бесплатно | начальный → средний | Язык целиком, включая C# 14. Для обучения — [Microsoft Learn: .NET](https://learn.microsoft.com/en-us/training/dotnet/) и [Foundational C# with Microsoft](https://www.freecodecamp.org/learn/foundational-c-sharp-with-microsoft/) на freeCodeCamp |
| [C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) и [Framework Design Guidelines онлайн](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) | Microsoft, живой | бесплатно | средний | Стиль кода и [правила именования](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines); вместе с [анализаторами кода](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) — чеклист ревью |
| [How Async/Await Really Works in C#](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/) | Stephen Toub, 2023 | бесплатно | средний → продвинутый | От APM и EAP через `Task` к конечным автоматам, `SynchronizationContext`, `ExecutionContext`, `ValueTask`. Дополнение — [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) (2019) |
| [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/) | Stephen Toub, 10 сентября 2025 | бесплатно, ~55 тыс. слов | продвинутый | JIT (инлайнинг, девиртуализация, escape analysis, структуры), векторизация (AVX10.2, Arm64 SVE), GC, коллекции, LINQ, строки, регулярные выражения, сеть, сжатие (zlib-ng). Все утверждения подкреплены BenchmarkDotNet |
| [Performance Improvements in .NET 11](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-11/) | Stephen Toub, 15 сентября 2026 | бесплатно | продвинутый | Продолжение серии для .NET 11 (выходит 10 ноября 2026): что ускорилось в рантайме и BCL по сравнению с .NET 10. Для LTS-проектов на .NET 10 полезно как прогноз выигрыша от перехода |
| [AsyncGuidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md) | David Fowler, живой | бесплатно | средний | Сжатый список «так нельзя / так можно»: sync-over-async, `async void`, `Task.Run` в ASP.NET Core, таймеры, отмена. Весь репозиторий [AspNetCoreDiagnosticScenarios](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios) — разбор типичных проблем production |
| [Асинхронное программирование](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/), [паттерны](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/), [threading](https://learn.microsoft.com/en-us/dotnet/standard/threading/) | Microsoft, живой | бесплатно | средний | Официальная модель TAP, отмена, прогресс, примитивы синхронизации |
| [Сборка мусора](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/) и [диагностика](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/) | Microsoft, живой | бесплатно | средний → продвинутый | Режимы GC, настройки, `dotnet-counters`, `dotnet-trace`, `dotnet-dump`, EventPipe |
| [mem-doc](https://github.com/Maoni0/mem-doc) | Maoni Stephens, живой | бесплатно | продвинутый | Как думать о памяти в .NET от архитектора GC: метрики, поколения, паузы, диагностика |
| [Book of the Runtime](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/README.md) | команда CoreCLR, живой | бесплатно | продвинутый | [Garbage Collection Design](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/garbage-collection.md), [Type System](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/type-system.md), Threading, RyuJIT Overview, Virtual Stub Dispatch, Exceptions, ReadyToRun, CLR ABI, Vectors and Intrinsics, новая глава Runtime Async Codegen. Прочие проектные документы — [docs/design](https://github.com/dotnet/runtime/tree/main/docs/design) |
| [Спецификация C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/readme), [ECMA-334](https://ecma-international.org/publications-and-standards/standards/ecma-334/) | ECMA TC49, 7-е издание декабрь 2023 (C# 7) | бесплатно | продвинутый | Нормативная семантика. Для C# 8+ — [csharpstandard](https://github.com/dotnet/csharpstandard) (черновики v8, v9), [proposals](https://github.com/dotnet/csharplang/tree/main/proposals), [история версий языка](https://github.com/dotnet/csharplang/blob/main/Language-Version-History.md) |
| [.NET Microservices: Architecture for Containerized .NET Applications](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/) | Microsoft, .NET Core-эпоха | бесплатно, PDF и веб | средний | DDD-микросервисы, CQRS, интеграционные события, устойчивость. Эталонное приложение eShopOnContainers заменено на [dotnet/eShop](https://github.com/dotnet/eShop) с .NET Aspire. Частично [устарело] |
| [Architecting Modern Web Apps with ASP.NET Core and Azure](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/) | Microsoft | бесплатно | средний | Монолит с чистой архитектурой, слои, тестирование, общие принципы веб-архитектуры |
| [Cloud-native .NET apps for Azure](https://learn.microsoft.com/en-us/dotnet/architecture/cloud-native/) | Microsoft | бесплатно | средний | Контейнеры, Kubernetes, устойчивость, наблюдаемость. Инфраструктурная часть [устарело] относительно Aspire |
| [Blazor for ASP.NET Web Forms developers](https://learn.microsoft.com/en-us/dotnet/architecture/blazor-for-web-forms-developers/) | Microsoft | бесплатно | средний | Миграция с Web Forms: компоненты, состояние, маршрутизация |
| [Enterprise Application Patterns Using .NET MAUI](https://learn.microsoft.com/en-us/dotnet/architecture/maui/) | Microsoft | бесплатно | средний | MVVM, DI, навигация, валидация в MAUI |
| [ASP.NET Core docs](https://learn.microsoft.com/en-us/aspnet/core/) и [best practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) | Microsoft, живой | бесплатно | средний | Актуальный ASP.NET Core 10; best practices — короткий чеклист производительности |
| [EF Core docs](https://learn.microsoft.com/en-us/ef/core/) и [EF Core performance](https://learn.microsoft.com/en-us/ef/core/performance/) | Microsoft, живой | бесплатно | средний | Всё, что вышло после EF Core 5: `ExecuteUpdate`/`ExecuteDelete`, compiled models, JSON-колонки, complex types |
| [Unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices) | Microsoft, живой | бесплатно | начальный → средний | Именование тестов, AAA, заглушки против моков |
| [Microbenchmark design guidelines](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md) | команда .NET performance | бесплатно | средний | Как сама команда .NET пишет бенчмарки. Краткая выжимка Pro .NET Benchmarking |
| [Threading in C#](https://www.albahari.com/threading/) | Joseph Albahari, C# 4/5-эпоха | бесплатно | средний | Примитивы синхронизации, `Monitor`, `volatile`, memory barriers объяснены лучше всех. Части про асинхронность [устарело] |
| [SharpLab](https://sharplab.io/) | Andrey Shchekin | бесплатно, инструмент | средний → продвинутый | Не книга: смотреть, во что опускается C#-код, IL и ассемблер JIT. Лучший спутник любой главы о внутренностях |
| [.NET release notes](https://github.com/dotnet/core/blob/main/release-notes/README.md) | Microsoft | бесплатно | средний | Что вышло и когда, поддержка, breaking changes |

## Полка «по ситуации»

### Ключевые выводы

- Берите эти книги под задачу: веб, данные, DDD, функциональный стиль, мобильная разработка, рефакторинг легаси.
- По ASP.NET Core и EF Core книги отстают на 3–5 версий. Архитектура и принципы из них верны, API сверяйте с документацией.
- The Art of Unit Testing 3e написана на JavaScript/TypeScript, а не на C#. Для C# лучше Khorikov.
- Четыре книги Price образуют серию под .NET 10: основы, контроллерный веб, приложения и сервисы, инструменты и навыки.
- Книги Packt по чистому коду и рефакторингу полезны средним разработчикам, но менее каноничны, чем Seemann и Khorikov.

| Ресурс | Автор, год | Издание, доступ | Уровень | Зачем читать |
|---|---|---|---|---|
| [ASP.NET Core in Action, 3rd ed.](https://www.manning.com/books/asp-net-core-in-action-third-edition) | Andrew Lock, июль 2023 | Manning, 984 стр., ASP.NET Core 7; платно | средний | Лучшая книга по устройству ASP.NET Core: Minimal API в первую очередь, маршрутизация, middleware, DI, конфигурация, логирование, аутентификация, тестирование. Для .NET 8–10 дополняйте серией постов автора на [andrewlock.net](https://andrewlock.net/). 4-го издания нет |
| [Entity Framework Core in Action, 2nd ed.](https://www.manning.com/books/entity-framework-core-in-action-second-edition) | Jon P Smith, 2021 | Manning, 575 стр., EF Core 5; платно | средний | Производительность запросов, DDD-сущности в EF Core, тестирование с БД, миграции. Блог автора — [The Reformed Programmer](https://www.thereformedprogrammer.net/). Возможности EF Core 6–10 отсутствуют |
| [Real-World Web Development with .NET 10, 2nd ed.](https://www.oreilly.com/library/view/-/9781835888926) | Mark J. Price, декабрь 2025 | Packt; платно | средний | Контроллерный ASP.NET Core MVC и Web API, проверенные временем подходы к сопровождаемым сайтам |
| [Apps and Services with .NET 10, 3rd ed.](https://www.packtpub.com/product/AppsandServiceswith.NET10/9781835462201) | Mark J. Price, февраль 2026 | Packt, 772 стр.; платно | средний | Ширина экосистемы: Avalonia, Blazor, gRPC, GraphQL, SignalR, кэширование, Aspire |
| [Tools and Skills for .NET 10, 2nd ed.](https://github.com/markjprice/tools-skills-net10) ([1-е издание под .NET 8](https://packtpub.com/en-gb/product/tools-and-skills-for-net-8-9781837635207/chapter/preface-pref/section/what-this-book-covers-preflvl1sec03)) | Mark J. Price, январь 2026 (1-е изд. июль 2024) | Packt, ISBN 9781835882511; 2-е изд. вышло 7 января 2026 (по репозиторию автора), 1-е изд. 778 стр.; платно | средний | Отладка, анализ памяти, Git, тестирование (включая производительность), Docker, Aspire, паттерны и принципы, подготовка к собеседованию |
| [Functional Programming in C#, 2nd ed.](https://www.manning.com/books/functional-programming-in-c-sharp-second-edition) | Enrico Buonanno, январь 2022 | Manning, 448 стр.; платно | средний → продвинутый | Функции высшего порядка, чистота, `Option`/`Either`, валидация, records и pattern matching, неизменяемые данные, async-потоки, агенты. Меняет взгляд на обработку ошибок без исключений |
| [Microservices in .NET, 2nd ed.](https://www.manning.com/books/microservices-in-net-second-edition) | Christian Horsdal Gammelgaard, январь 2022 | Manning; платно | средний | Границы сервисов, событийное взаимодействие, устойчивость, деплой в Kubernetes на Azure, мониторинг |
| [.NET MAUI in Action](https://www.manning.com/books/dot-net-maui-in-action) | Matt Goldman, июль 2023 | Manning, 456 стр.; платно | средний | Кроссплатформенные приложения: layouts, MVVM, кастомные контролы, корпоративные паттерны, CI/CD в GitHub Actions |
| [Domain-Driven Design](https://www.domainlanguage.com/ddd/) | Eric Evans, 2003 | Addison-Wesley; платно | продвинутый | Единый язык, ограниченные контексты, агрегаты, стратегическое проектирование. Не про C#, но основа всего DDD в .NET |
| [Implementing Domain-Driven Design](https://www.oreilly.com/library/view/implementing-domain-driven-design/9780133039900/) | Vaughn Vernon, 2013 | Addison-Wesley; платно | продвинутый | Тактика DDD на практике: агрегаты и их размер, доменные события, интеграция контекстов. Примеры на Java, легко переносятся |
| [Patterns, Principles, and Practices of Domain-Driven Design](https://www.wiley.com/en-us/Patterns%2C+Principles%2C+and+Practices+of+Domain+Driven+Design-p-9781118714706) | Scott Millett, Nick Tune, 2015 | Wrox (Wiley); платно | средний → продвинутый | DDD с примерами на C#: стратегия, тактика, интеграция через сообщения, CQRS, event sourcing. Технологии примеров (NServiceBus, ASP.NET MVC 5) [устарело] |
| [Learning Domain-Driven Design](https://www.oreilly.com/library/view/learning-domain-driven-design/9781098100124/) | Vlad Khononov, 2021 | O'Reilly; платно | средний | Самое современное и короткое введение в DDD: поддомены, выбор паттерна реализации под сложность, event storming |
| [The Art of Unit Testing, 3rd ed.](https://www.manning.com/books/the-art-of-unit-testing-third-edition) | Roy Osherove, Vladimir Khorikov, 2024 | Manning; примеры на JavaScript/TypeScript | начальный → средний | Читаемые и надёжные тесты, фейки, стабы, моки. 2-е издание (2013) было на C#, но [устарело] |
| [Clean Code with C#, 2nd ed.](https://www.packtpub.com/en-us/product/clean-code-with-c-second-edition-9781837635191) | Jason Alls, декабрь 2023 | Packt, 492 стр.; платно | начальный → средний | Стандарты кода, ревью, TDD, обработка исключений, сквозные задачи, рефакторинг легаси |
| [Refactoring with C#](https://www.packtpub.com/en-us/product/refactoring-with-c-9781835089989) | Matt Eland, ноябрь 2023 | Packt, 434 стр., .NET 8 / C# 12; платно | средний | Технический долг, безопасный рефакторинг под тестами, SOLID, собственные анализаторы Roslyn, Copilot |
| [C# Data Structures and Algorithms, 2nd ed.](https://www.packtpub.com/en-us/product/c-data-structures-and-algorithms-9781803248271) | Marcin Jamro, февраль 2024 | Packt, 372 стр.; платно | начальный → средний | Массивы, списки, стеки, очереди, словари, множества, деревья, графы и алгоритмы на них на C# |
| [Effective C#, 3rd ed.](https://www.oreilly.com/library/view/effective-c-50/9780134579290) / [More Effective C#, 2nd ed.](https://www.oreilly.com/library/view/more-effective-c/9780134579306) | Bill Wagner, 2016 / 2017 | Addison-Wesley; платно | средний | 50 + 50 пунктов про C# 6/7: `readonly`, boxing, `IDisposable`, отложенное выполнение LINQ, ограничения generics, async. Часть советов покрыта анализаторами, новых возможностей языка нет |
| [Writing High-Performance .NET Code, 2nd ed.](https://writinghighperf.net/) | Ben Watson, апрель 2018 | самиздат, 519 стр. ([Goodreads](https://www.goodreads.com/work/editions/60936655-writing-high-performance-net-code)); платно | средний → продвинутый | Методика измерения, GC, JIT, async, коллекции, PerfView и ETW. Модель мышления верна, инструменты и API эпохи .NET Framework/ранних Core [устарело] частично |
| [CLR via C#, 4th ed.](https://www.oreilly.com/library/view/clr-via-c/9780735668737/) | Jeffrey Richter, 2012 | Microsoft Press; платно | продвинутый | Типы значений и ссылок, boxing, generics в CLR, делегаты, события, исключения, основы GC. Много .NET Framework-специфики [устарело] |

## Что устарело в старых книгах

### Ключевые выводы

- Всё, что связано с AppDomain, Code Access Security, .NET Remoting, WCF-сервером, `web.config` и `System.Web`, в современном .NET отсутствует или заменено.
- Асинхронность до 2019 года: советы про `ConfigureAwait(false)` «везде» и про deadlock из-за `SynchronizationContext` относятся к UI и классическому ASP.NET. В ASP.NET Core контекста синхронизации нет, но в библиотеках `ConfigureAwait(false)` по-прежнему уместен.
- Производительность до .NET Core 2.1: не было `Span<T>`, `Memory<T>`, `ArrayPool<T>`, `ValueTask`, `System.Text.Json`, tiered compilation и Dynamic PGO (по умолчанию с .NET 8), Native AOT. Микросоветы вроде «`StringBuilder` всегда быстрее» или «избегайте LINQ любой ценой» проверяйте бенчмарком: JIT и BCL с тех пор сильно ускорились.
- Язык до C# 8: без nullable reference types, records, init-свойств, switch-выражений и расширенного pattern matching, file-scoped namespaces, top-level statements, primary constructors, collection expressions, `required`, `field`, extension members. Код в таких книгах корректен, но выглядит многословно.

| Тема в старой книге | Что было | Что теперь | Источник актуального |
|---|---|---|---|
| Асинхронность | APM (`BeginX`/`EndX`), EAP, `Task.Wait()`, «async in C# 5.0» | TAP, `ValueTask`, `IAsyncEnumerable`, `await foreach`, `Channel<T>`, `Parallel.ForEachAsync`, `TimeProvider` | [Toub 2023](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/), [AsyncGuidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md) |
| Async in C# 5.0 (Alex Davies, O'Reilly 2012) | Хорошее введение для своего времени | [устарело] целиком, кроме идеи конечного автомата | то же |
| Конфигурация и хостинг | `app.config`, `web.config`, IIS, `Global.asax` | Generic Host, `IOptions<T>`, `appsettings.json`, Kestrel, контейнеры, Aspire | [ASP.NET Core docs](https://learn.microsoft.com/en-us/aspnet/core/) |
| DI | Сторонние контейнеры как обязательная часть, Service Locator в старых фреймворках | Встроенный `Microsoft.Extensions.DependencyInjection`, keyed services (.NET 8) | DI PP&P (принципы), docs (API) |
| Память и GC | Настройки через `app.config`, LOH-фрагментация как неизбежность | `runtimeconfig.json`, POH, Regions (.NET 7), DATAS (по умолчанию для Server GC в .NET 9) | [Pro .NET Memory Management 2e](https://link.springer.com/book/9798868804526), [GC docs](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/) |
| Сериализация | `BinaryFormatter`, `DataContractSerializer`, Newtonsoft.Json по умолчанию | `BinaryFormatter` удалён (.NET 9), `System.Text.Json` с source generators | docs |
| Рефлексия и кодогенерация | Reflection.Emit, T4 | Source generators, Native AOT-совместимость, trimming | docs, [andrewlock.net](https://andrewlock.net/) |
| Тестовые фреймворки | MSTest v1, NUnit 2 | xUnit v3, NUnit 4, MSTest 3+ и Microsoft.Testing.Platform | docs |
| Микросервисы | eShopOnContainers, ручной Docker Compose | [dotnet/eShop](https://github.com/dotnet/eShop) на .NET Aspire | e-книги Microsoft частично |

## Блоги и люди

### Ключевые выводы

- Для производительности обязательны Stephen Toub, Konrad Kokosa, Adam Sitnik, Maoni Stephens и Kevin Gosse. Их тексты — первоисточник, на который ссылаются книги.
- Andrew Lock к каждому релизу .NET выпускает подробную серию постов, разбирая новые API изнутри. Это лучший «апдейт» к его книге.
- Для архитектуры и дизайна — Mark Seemann, Vladimir Khorikov, Jimmy Bogard, Oskar Dudycz, Derek Comartin. Мнения у них расходятся, это полезно.
- YouTube-каналы (Nick Chapsas, Milan Jovanović, Zoran Horvat, Raw Coding, Tim Corey) хороши для обзора новинок. Код из коротких видео не переносите в production без проверки.
- Gérald Barré и Meziantou — один и тот же человек: блог [meziantou.net](https://www.meziantou.net/), автор анализатора Meziantou.Analyzer.

| Автор | Где | О чём |
|---|---|---|
| Stephen Toub | [devblogs: Toub](https://devblogs.microsoft.com/dotnet/author/toub/) | Ежегодные «Performance Improvements», async изнутри, Regex, Channels |
| Stephen Cleary | [blog.stephencleary.com](https://blog.stephencleary.com/) | async и конкурентность: «There Is No Thread», «Don't Block on Async Code», отмена |
| Andrew Lock | [andrewlock.net](https://andrewlock.net/) | ASP.NET Core изнутри, source generators, серии к каждому релизу .NET |
| Steve Gordon | [stevejgordon.co.uk](https://www.stevejgordon.co.uk/) | Высокопроизводительный C#, `HttpClient`, `Span<T>`, пайплайны |
| Konrad Kokosa | [prodotnetmemory.com](https://prodotnetmemory.com/), блог [tooslowexception.com](https://web.archive.org/web/2026/https://tooslowexception.com/) (архив: с октября 2026 сайт отвечает HTTP 500) | GC и память, автор Pro .NET Memory Management |
| Adam Sitnik | [adamsitnik.com](https://adamsitnik.com/), [awesome-dot-net-performance](https://github.com/adamsitnik/awesome-dot-net-performance) | BenchmarkDotNet, производительность BCL |
| Maoni Stephens | [devblogs: Maoni](https://devblogs.microsoft.com/dotnet/author/maoni/), [mem-doc](https://github.com/Maoni0/mem-doc) | Архитектор GC .NET: Regions, DATAS, диагностика памяти |
| Kevin Gosse | [minidump.net](https://minidump.net/) | Внутренности рантайма, отладка дампов, соавтор Pro .NET Memory Management 2e |
| Marc Gravell | [blog.marcgravell.com](https://blog.marcgravell.com/) | Dapper, protobuf-net, низкоуровневая производительность. Пишет редко |
| Matt Warren | [mattwarren.org](https://mattwarren.org/) | Внутренности CLR, архив 2016–2020: ценен, но часть деталей [устарело] |
| Mark Seemann | [blog.ploeh.dk](https://blog.ploeh.dk/) | Дизайн, DI, функциональное программирование, тесты |
| Vladimir Khorikov | [enterprisecraftsmanship.com](https://enterprisecraftsmanship.com/) | DDD, тестирование, функциональная обработка ошибок |
| Jimmy Bogard | [jimmybogard.com](https://www.jimmybogard.com/) | Vertical slice architecture, распределённые системы; автор MediatR и AutoMapper (с июля 2025 года, MediatR 13+ и AutoMapper 15+, — двойная лицензия RPL-1.5 / коммерческая через Lucky Penny Software, бесплатно при выручке до $5 млн) |
| Milan Jovanović | [milanjovanovic.tech](https://www.milanjovanovic.tech/), [YouTube](https://www.youtube.com/@MilanJovanovicTech) | Clean и модульная архитектура, рассылка The .NET Weekly (~66 тыс. подписчиков) |
| Nick Chapsas | [YouTube](https://www.youtube.com/@nickchapsas), [Dometrain](https://dometrain.com/) | Новинки C#/.NET, производительность, курсы |
| Oskar Dudycz | [event-driven.io](https://event-driven.io/en/) | Event sourcing, Marten, проектирование событийных систем |
| Jon Skeet | [codeblog.jonskeet.uk](https://codeblog.jonskeet.uk/) | Тёмные углы языка, даты и время (NodaTime) |
| David Fowler | [GitHub davidfowl](https://github.com/davidfowl) | Архитектор ASP.NET Core и Aspire, AspNetCoreDiagnosticScenarios |
| Damian Edwards | [blog.damianedwards.com](https://blog.damianedwards.com), [ASP.NET Community Standup](https://dotnet.microsoft.com/en-us/live) | ASP.NET Core, Aspire (архитектор продукта в Microsoft) |
| Khalid Abuhakmeh | [khalidabuhakmeh.com](https://khalidabuhakmeh.com/) | Практичные советы по ASP.NET Core, Rider |
| Gérald Barré (Meziantou) | [meziantou.net](https://www.meziantou.net/) | Короткие точные посты про C#, анализаторы, тонкости BCL |
| Tim Corey | [iamtimcorey.com](https://www.iamtimcorey.com/), [YouTube](https://www.youtube.com/@IAmTimCorey) | Курсы для начинающих и средних |
| Raw Coding | [YouTube](https://www.youtube.com/@RawCoding) | ASP.NET Core изнутри, аутентификация |
| Zoran Horvat | [YouTube](https://www.youtube.com/@zoran-horvat) | Объектный и функциональный дизайн на C# |
| Derek Comartin | [codeopinion.com](https://codeopinion.com/), [YouTube](https://www.youtube.com/@CodeOpinion) | Архитектура, сообщения, границы сервисов |
| Steve Smith (Ardalis) | [ardalis.com](https://ardalis.com/), [CleanArchitecture](https://github.com/ardalis/CleanArchitecture) | Clean Architecture, шаблон решения |
| Jason Taylor | [CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) | Популярный шаблон Clean Architecture |
| Блоги компаний | [JetBrains .NET](https://blog.jetbrains.com/dotnet/), [NDepend](https://blog.ndepend.com/), [Code Maze](https://code-maze.com/) | Инструменты, качество кода, туториалы |

## Доклады, подкасты и рассылки

### Ключевые выводы

- .NET Conf (ноябрь, вместе с релизом .NET) — официальный обзор нового. Записи — на [YouTube-канале dotnet](https://www.youtube.com/@dotnet).
- NDC (Oslo, London, Sydney и др.) — главная конференция с глубокими докладами по .NET, архитектуре и тестированию. Почти всё выкладывается бесплатно.
- Для ежедневного потока ссылок достаточно одной рассылки: Morning Dew (ежедневно) или The .NET Weekly (раз в неделю).

| Ресурс | Формат | Зачем |
|---|---|---|
| [.NET Conf](https://www.dotnetconf.net/) | ежегодная онлайн-конференция | Запуск каждой версии .NET, доклады команд Microsoft |
| [.NET Live / Community Standups](https://dotnet.microsoft.com/en-us/live) | регулярные трансляции | ASP.NET Core, языковой дизайн, рантайм — от самих разработчиков |
| [Microsoft Learn Shows](https://learn.microsoft.com/en-us/shows/) | видео | Официальные серии, включая обучающие |
| [NDC Conferences](https://ndcconferences.com/), [YouTube NDC](https://www.youtube.com/@NDC) | конференции, записи | Глубокие доклады Toub, Fowler, Seemann, Lock, Kokosa и других |
| [.NET Rocks!](https://www.dotnetrocks.com/) | подкаст с 2002 года | Интервью с авторами книг и инструментов |
| [The Unhandled Exception Podcast](https://unhandledexceptionpodcast.com/) | подкаст (Dan Clarke) | Практика .NET-разработки, интервью |
| [Hanselminutes](https://www.hanselminutes.com/) | подкаст (Scott Hanselman) | Шире .NET: культура разработки, технологии |
| [Morning Dew](https://www.alvinashcraft.com/) | ежедневная подборка ссылок (Alvin Ashcraft) | Самый полный поток новостей .NET и Windows |
| [The .NET Weekly](https://www.milanjovanovic.tech/) | еженедельная рассылка (Milan Jovanović) | Один практический разбор в неделю, архитектура |
| [CODE Magazine](https://www.codemag.com/), [DotNetCurry](https://www.dotnetcurry.com/) | журналы | Длинные статьи; DotNetCurry в основном архив |

## Маршрут чтения по уровням

### Ключевые выводы

- Не перепрыгивайте ступени: Pro .NET Memory Management без уверенного понимания типов значений, ссылок и `IDisposable` читается тяжело.
- На каждом уровне сочетайте книгу-основу с чеклистом, который сразу применяется к своему коду: coding conventions, Framework Design Guidelines, анализаторы, AsyncGuidance.
- Производительностью занимайтесь после того, как код стал идиоматичным и покрытым тестами. Первый шаг всегда измерение (BenchmarkDotNet, `dotnet-counters`, `dotnet-trace`), а не книга советов.
- Специализацию выбирайте по задаче, параллельно со ступенями 3–4.

| Ступень | Что читать | Зачем |
|---|---|---|
| 1. Начальный: фундамент | [C# 14 and .NET 10 (Price)](https://www.packtpub.com/en-us/product/c-14-and-net-10-modern-cross-platform-development-fundamentals-9781836206620) или [Tour of C#](https://learn.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/) + [Microsoft Learn](https://learn.microsoft.com/en-us/training/dotnet/); практика — [Exercism C#](https://exercism.org/tracks/csharp); алгоритмы — [Jamro](https://www.packtpub.com/en-us/product/c-data-structures-and-algorithms-9781803248271) | Язык, ООП, коллекции, LINQ, базовый async, первый веб-сервис и тесты |
| 2. Средний: идиоматичность и тесты | [C# 12 in a Nutshell](https://www.albahari.com/nutshell/) как справочник, [coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions), [Unit Testing PP&P](https://www.manning.com/books/unit-testing), [DI PP&P](https://www.manning.com/books/dependency-injection-principles-practices-patterns), [How Async/Await Really Works](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/), [AsyncGuidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md) | Писать код, который выглядит как в хороших библиотеках: NRT, records, DI, тесты на поведение, корректный async |
| 3. Старший: глубина и дизайн | [C# in Depth](https://www.manning.com/books/c-sharp-in-depth-fourth-edition), [Framework Design Guidelines 3e](https://www.pearson.com/en-us/subject-catalog/p/framework-design-guidelines-conventions-idioms-and-patterns-for-reusable-net-libraries/P200000000204/9780135896327), [Code That Fits in Your Head](https://www.pearson.com/en-gb/subject-catalog/p/code-that-fits-in-your-head-heuristics-for-software-engineering/P200000009593/9780137464357), [Functional Programming in C#](https://www.manning.com/books/functional-programming-in-c-sharp-second-edition), [SharpLab](https://sharplab.io/) для проверки | Почему язык устроен так, дизайн публичных API, управление сложностью, ошибки без исключений |
| 4. Старший: производительность и память | [Pro .NET Benchmarking](https://link.springer.com/book/10.1007/978-1-4842-4941-3) → [Pro .NET Memory Management 2e](https://link.springer.com/book/9798868804526) → [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/); затем [mem-doc](https://github.com/Maoni0/mem-doc) и [Writing High-Performance .NET Code](https://writinghighperf.net/) ради методики | Порядок: измерить, убрать лишнюю работу, сократить аллокации, затем низкоуровневые приёмы (`Span<T>`, пулы, SIMD) |
| Трек: конкурентность | [Concurrency in C# Cookbook 2e](https://www.oreilly.com/library/view/concurrency-in-c/9781492054498/), [блог Cleary](https://blog.stephencleary.com/), [Threading in C#](https://www.albahari.com/threading/) (примитивы), [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/), [threading docs](https://learn.microsoft.com/en-us/dotnet/standard/threading/) | async без deadlock и sync-over-async, отмена, конвейеры, примитивы синхронизации |
| Трек: веб и данные | [ASP.NET Core in Action 3e](https://www.manning.com/books/asp-net-core-in-action-third-edition) + [andrewlock.net](https://andrewlock.net/), [EF Core in Action 2e](https://www.manning.com/books/entity-framework-core-in-action-second-edition) + [EF Core performance](https://learn.microsoft.com/en-us/ef/core/performance/), [ASP.NET Core best practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) | Устройство конвейера запросов, доступ к данным без N+1, производительность сервиса |
| Трек: архитектура | [Learning DDD](https://www.oreilly.com/library/view/learning-domain-driven-design/9781098100124/) → [Evans](https://www.domainlanguage.com/ddd/) → [Vernon](https://www.oreilly.com/library/view/implementing-domain-driven-design/9780133039900/) или [Millett](https://www.wiley.com/en-us/Patterns%2C+Principles%2C+and+Practices+of+Domain+Driven+Design-p-9781118714706) (C#); [Microservices in .NET 2e](https://www.manning.com/books/microservices-in-net-second-edition), [e-книги Microsoft](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/), эталон [dotnet/eShop](https://github.com/dotnet/eShop), блоги [Bogard](https://www.jimmybogard.com/), [Dudycz](https://event-driven.io/en/), [CodeOpinion](https://codeopinion.com/) | Границы контекстов, агрегаты, модульный монолит против микросервисов, события |
| Трек: внутренности рантайма | [BOTR](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/README.md), [CLR via C#](https://www.oreilly.com/library/view/clr-via-c/9780735668737/) (выборочно), [спецификация C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/readme), [csharplang proposals](https://github.com/dotnet/csharplang/tree/main/proposals), [minidump.net](https://minidump.net/) | Типовая система, JIT, GC, исключения изнутри; нормативная семантика языка |
| Трек: UI и мобильные | [.NET MAUI in Action](https://www.manning.com/books/dot-net-maui-in-action), [Enterprise Patterns with MAUI](https://learn.microsoft.com/en-us/dotnet/architecture/maui/), [Apps and Services with .NET 10](https://www.packtpub.com/product/AppsandServiceswith.NET10/9781835462201) (Avalonia, Blazor) | MVVM, DI и навигация в клиентских приложениях |
| Трек: легаси и рефакторинг | [Refactoring with C#](https://www.packtpub.com/en-us/product/refactoring-with-c-9781835089989), [Clean Code with C#](https://www.packtpub.com/en-us/product/clean-code-with-c-second-edition-9781837635191), [Blazor for Web Forms developers](https://learn.microsoft.com/en-us/dotnet/architecture/blazor-for-web-forms-developers/) | Перевод старого кода на .NET 10 под защитой тестов и анализаторов |

## Каталоги и навигаторы

### Ключевые выводы

- Для поиска библиотеки под задачу начинайте с awesome-dotnet (quozd); awesome-dotnet-core дополняет его примерами и шаблонами.
- Списки «лучших книг» проверяйте по странице издателя: агрегаторы часто путают издания и даты, а Packt выпускает похожие названия ежегодно.

| Ресурс | Автор, год | Зачем |
|---|---|---|
| [awesome-dotnet](https://github.com/quozd/awesome-dotnet) | quozd и сообщество, живой | Каталог библиотек и инструментов .NET по категориям |
| [awesome-dotnet-core](https://github.com/thangchung/awesome-dotnet-core) | thangchung, живой | Библиотеки, фреймворки, примеры приложений и шаблоны на .NET (Core) |
| [awesome-dot-net-performance](https://github.com/adamsitnik/awesome-dot-net-performance) | Adam Sitnik | Книги, доклады, статьи и инструменты по производительности |
| [awesome-software-architecture](https://github.com/mehdihadeli/awesome-software-architecture) | mehdihadeli | Архитектура, DDD, микросервисы, много ссылок на .NET |
| [.NET Foundation](https://dotnetfoundation.org/) | .NET Foundation | Каталог проектов фонда |
| [NuGet](https://www.nuget.org/) | Microsoft | Поиск пакетов: смотрите загрузки, дату релиза, целевые фреймворки |

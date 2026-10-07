# Производительность C# и .NET

Справочник для ускорения кода на C#/.NET и настройки сборки и рантайма в любой области: сервисы, CLI, библиотеки, десктоп, обработка данных. Здесь есть порядок работы, таблица «симптом → техника → источник» для быстрого поиска, разделы про бенчмарки, профилирование, GC, аллокации, коллекции, JIT и AOT, SIMD, I/O, EF Core и ASP.NET Core, настройки `.csproj`, разборы реальных случаев и антипаттерны. Опорные источники — документация [learn.microsoft.com](https://learn.microsoft.com/en-us/dotnet/) и ежегодные статьи Стивена Тауба «Performance Improvements in .NET N». Актуально на октябрь 2026 года: .NET 10 (LTS, 10.0.12), .NET 11 в стадии RC1 с go-live-лицензией (GA ожидается в ноябре 2026), C# 14 (с .NET 10) и C# 15 (с .NET 11), BenchmarkDotNet 0.15.8 (стабильная) и 0.16.0-preview.2. .NET 8 и .NET 9 выходят из поддержки 10 ноября 2026 года ([политика поддержки](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)).

- [Метод: убрать работу → измерить → тюнить](#метод-убрать-работу--измерить--тюнить)
- [Симптом → техника → источник](#симптом--техника--источник)
- [Бенчмарки: BenchmarkDotNet](#бенчмарки-benchmarkdotnet)
- [Профилирование и диагностика](#профилирование-и-диагностика)
- [GC и память](#gc-и-память)
- [Аллокации: Span, пулы, строки](#аллокации-span-пулы-строки)
- [Коллекции и поиск](#коллекции-и-поиск)
- [JIT, PGO, ReadyToRun и Native AOT](#jit-pgo-readytorun-и-native-aot)
- [SIMD и векторизация](#simd-и-векторизация)
- [I/O, сериализация, regex, логирование](#io-сериализация-regex-логирование)
- [EF Core и ASP.NET Core](#ef-core-и-aspnet-core)
- [Конфигурация сборки и рантайма](#конфигурация-сборки-и-рантайма)
- [Обзоры Performance Improvements in .NET](#обзоры-performance-improvements-in-net)
- [Разборы реальных случаев](#разборы-реальных-случаев)
- [Антипаттерны](#антипаттерны)

---

## Метод: убрать работу → измерить → тюнить

Сначала профиль или счётчики, потом правки. Примерный порядок по убыванию отдачи:

1. убрать лишнюю работу: другой алгоритм, кэш, ранний выход, меньше обращений к БД и сети;
2. обновить рантайм: каждая версия .NET даёт бесплатный прирост, а Bing при переходе на .NET 8 получил минус 13% CPU-циклов на запрос без правок кода;
3. сократить аллокации в горячем пути (меньше работы для GC);
4. подобрать структуры данных (`FrozenDictionary`, `SearchValues`, массивы вместо списков связей);
5. убрать блокировки и sync-over-async, проверить пул потоков;
6. довести код до формы, которую JIT хорошо оптимизирует (без лишних bounds checks, боксинга и виртуальных вызовов), затем явный SIMD;
7. настройки сборки и рантайма: режим GC, ReadyToRun, Native AOT, `InvariantGlobalization`.

### Ключевые выводы

- Не оптимизируйте без замера. Горячая точка почти всегда не там, где кажется. Мэони Стивенс: «Don't guess, measure».
- Меряйте только Release-сборку без отладчика. По документации BenchmarkDotNet, Debug-код может быть медленнее в 10–100 раз.
- Отличайте пропускную способность, задержку (p99) и память. Оптимизация одного часто ухудшает другое: DATAS снижает рабочий набор более чем на 80% ценой 2–3% RPS.
- Аллокация стоит денег, даже если не вызвала GC. Мэони: «does an allocation have a cost when it doesn't trigger a GC? The answer is absolutely». Работу GC определяет то, что выживает.
- После каждой правки сравнивайте с сохранённым baseline. Разница меньше шума не считается улучшением.
- Флаги рантайма и `unsafe` — в последнюю очередь, и только с бенчмарком. По документации GC, значения по умолчанию оптимальны для большинства приложений.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [.NET Memory Performance Analysis](https://github.com/Maoni0/mem-doc/blob/master/doc/.NETMemoryPerformanceAnalysis.md) | Maoni Stephens (архитектор GC), живой документ | Как думать о памяти и GC: что мерить, как читать паузы и бюджет аллокаций. Лучший текст по теме |
| [Writing High-Performance .NET Code, 2nd ed.](https://www.writinghighperf.net/) | Ben Watson, 2018 | Методология и инструменты от инженера Bing. Частично [устарело]: нет Span-эпохи, DATAS, Native AOT. Метод по-прежнему верен |
| [Pro .NET Memory Management, 2nd ed.](https://prodotnetmemory.com/) | Konrad Kokosa, Kevin Gosse, Christophe Nasarre, 2024 | Более 1000 страниц про GC и память, обновлено под .NET 8. 1-е издание (2018) [устарело] → 2-е |
| [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) | Mike Rousos, Microsoft, обновляется | Короткий чеклист главных серверных ошибок, полезен и вне веба |

---

## Симптом → техника → источник

Основная таблица для поиска. Начинайте с симптома, который показал профайлер, счётчик или ревью.

| Симптом | Техника | Источник |
|---|---|---|
| Непонятно, где тратится время | `dotnet-trace collect` (профили по умолчанию `dotnet-common` + `dotnet-sampled-thread-time`), открыть в PerfView, VS или speedscope; на Windows/macOS — Ultra | [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace), [Ultra](https://github.com/xoofx/ultra) |
| Нужна быстрая картина живого процесса | `dotnet-counters monitor`: CPU, % времени в GC, аллокации/с, очередь пула потоков, исключения | [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters), [runtime metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime) |
| Бенчмарк «улучшился», но непонятно, не шум ли это | BenchmarkDotNet с `[Benchmark(Baseline = true)]`, сравнение распределений, `scripts/compare-bdn.cs` с порогом по full JSON | [Baselines](https://benchmarkdotnet.org/articles/features/baselines.html), [compare-bdn.cs](../scripts/compare-bdn.cs) |
| Неизвестно, сколько аллоцирует метод | `[MemoryDiagnoser]` в бенчмарке; в тесте — `GC.GetAllocatedBytesForCurrentThread()` до и после | [Diagnosers](https://benchmarkdotnet.org/articles/configs/diagnosers.html), [API](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getallocatedbytesforcurrentthread) |
| Высокий % времени в GC, частые gen0 | Убрать аллокации в горячем пути: `Span<T>`, пулы, структуры, без LINQ и замыканий | [mem-doc](https://github.com/Maoni0/mem-doc/blob/master/doc/.NETMemoryPerformanceAnalysis.md), [GC и производительность](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/performance) |
| Частые gen2 и паузы из-за больших массивов (≥ 85 000 байт) | `ArrayPool<T>.Shared` для буферов, потоковая обработка вместо чтения целиком | [LOH](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap), [Array Pool](https://adamsitnik.com/Array-Pool/) |
| Рабочий набор сервиса раздут на многоядерной машине | Server GC с DATAS (по умолчанию с .NET 9), `HeapHardLimit`/`ConserveMemory` для контейнеров | [DATAS](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/datas), [GC config](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector) |
| Пропускная способность упала после перехода на .NET 9+ | Проверить влияние DATAS: `GarbageCollectionAdaptationMode=0` или `System.GC.DTargetTCP` | [GC config: DATAS](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector) |
| Утечка памяти, куча растёт | `dotnet-gcdump collect`, сравнение двух снимков; dotMemory или VS | [dotnet-gcdump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump), [debug-memory-leak](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-memory-leak) |
| Много промежуточных `string` (`Substring`, `Split`, конкатенация) | `ReadOnlySpan<char>`, `AsSpan()`, `MemoryExtensions.Split`, `string.Create` | [Memory<T> guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines), [string.Create](https://learn.microsoft.com/en-us/dotnet/api/system.string.create) |
| Конкатенация строк в цикле | `StringBuilder` (переиспользуемый или из `ObjectPool`), `string.Join` | [ObjectPool](https://learn.microsoft.com/en-us/aspnet/core/performance/objectpool) |
| Форматирование в буфер аллоцирует | Интерполяция в `Span<char>` через `TryWrite`, свои interpolated string handlers, `ISpanFormattable` | [Interpolated string handler](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/interpolated-string-handler), [Toub: C# 10 interpolation](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/) |
| Константные байтовые строки (HTTP, протоколы) | UTF-8 литералы `"OK"u8` → `ReadOnlySpan<byte>` без аллокации | [reference types: UTF-8 literals](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types) |
| Временный небольшой буфер в методе | `stackalloc` в `Span<T>` (небольшой размер, общая практика — до ~1 КБ), иначе `ArrayPool` | [stackalloc](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc) |
| `params T[]` аллоцирует массив на каждый вызов | `params ReadOnlySpan<T>` (params collections, C# 13) | [params](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/params) |
| Боксинг в профиле (`struct` → `object`/интерфейс) | Generic-ограничения `where T : IFoo` вместо параметра-интерфейса, `IEquatable<T>`, свой компаратор | [C# perf: reduce allocations](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/), [CA18xx](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/performance-warnings) |
| Лямбда захватывает локальные переменные в горячем цикле | `static`-лямбды, передача состояния аргументом (`state`-перегрузки), вынос делегата в поле | [.NET 10 runtime: escape analysis](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime) |
| LINQ в горячем пути (`Where().Select().ToList()` на каждый запрос) | Обычный цикл по `Span`/массиву или одна LINQ-цепочка без промежуточных `ToList`; обновить рантайм | [Toub: .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/) |
| Справочник строится один раз, читается миллионы раз | `ToFrozenDictionary()`/`ToFrozenSet()` | [FrozenDictionary](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2) |
| Поиск любого символа или подстроки из набора | `static readonly SearchValues<char>` + `IndexOfAny` | [SearchValues](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.searchvalues-1) |
| Двойной поиск в словаре (`ContainsKey` + индексатор, `TryGetValue` + запись) | `CollectionsMarshal.GetValueRefOrAddDefault`, `TryAdd` | [CollectionsMarshal](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal) |
| Ключ словаря — `string`, а на входе `ReadOnlySpan<char>` | `GetAlternateLookup<ReadOnlySpan<char>>()` (.NET 9+) без создания строки | [GetAlternateLookup](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.getalternatelookup) |
| Числовой цикл по массиву медленный | `TensorPrimitives` для готовых операций, затем `Vector128/256/512<T>` с проверкой `IsHardwareAccelerated` | [TensorPrimitives](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.tensors.tensorprimitives), [SIMD](https://learn.microsoft.com/en-us/dotnet/standard/simd) |
| Медленный старт, много JIT в профиле | ReadyToRun (`PublishReadyToRun`), для облака и CLI — Native AOT | [ReadyToRun](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run), [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) |
| Первые запросы медленные, потом быстро | Это tiering: прогрев перед вводом в балансировщик, R2R; не отключать `TieredPGO` | [Compilation config](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation) |
| Сериализация JSON горячая или нужен AOT | `System.Text.Json` с source generator (`JsonSerializerContext`) | [STJ source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) |
| Regex в горячем пути или долгий старт из-за `RegexOptions.Compiled` | `[GeneratedRegex]` на partial-методе | [Regex source generators](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-source-generators) |
| Логирование аллоцирует даже при выключенном уровне | `[LoggerMessage]` partial-методы, без интерполяции в шаблоне (CA1848) | [LoggerMessage generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) |
| Разбор потокового протокола со своим буфером | `System.IO.Pipelines`: `PipeReader`/`PipeWriter`, `ReadOnlySequence<byte>` | [Pipelines](https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines) |
| Параллельное чтение большого файла | `RandomAccess.Read` по смещениям или memory-mapped файл, разбиение по границам строк | [RandomAccess](https://learn.microsoft.com/en-us/dotnet/api/system.io.randomaccess), [fast1brc](https://github.com/xoofx/fast1brc) |
| `MemoryStream` растёт и попадает в LOH | `RecyclableMemoryStream` | [RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream) |
| Растёт очередь пула потоков, задержки скачут | Убрать `.Result`/`.Wait()`, не делать `Task.Run` + `await` в ASP.NET Core | [Best practices: avoid blocking](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) |
| Метод почти всегда завершается синхронно, но аллоцирует `Task` | `ValueTask<T>`; никогда не `await` его дважды | [Understanding ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/) |
| Глубокие async-цепочки, много исключений через `await` | Попробовать runtime async в .NET 11 (`runtime-async=on`, preview) | [.NET 11 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime) |
| Исключения в профиле | Проверка условий заранее (`TryParse`, `TryGetValue`), исключения только для исключительного | [Best practices: minimize exceptions](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) |
| EF Core: много SQL-запросов на страницу | Устранить N+1: проекция `Select`, `Include` или split queries, пакетная загрузка | [Efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) |
| EF Core: чтение медленное и жрёт память | `AsNoTracking()`, проекция только нужных столбцов, пагинация | [Efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) |
| EF Core: накладные расходы на создание контекста и компиляцию запроса | `AddDbContextPool`, `EF.CompileAsyncQuery` для горячих запросов | [Advanced performance](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics) |
| EF Core: медленный первый запрос на большой модели | Compiled model (`dotnet ef dbcontext optimize`) | [Advanced performance](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics) |
| EF Core: план кэша запросов не прогревается | Параметры вместо констант в динамических деревьях выражений | [Advanced performance](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics) |
| Нужен контроль регрессий в CI | Бенчмарки с full JSON-экспортом + `compare-bdn.cs` с порогом (история — github-action-benchmark или Bencher); для веба — crank | [dotnet/performance](https://github.com/dotnet/performance), [crank](https://github.com/dotnet/crank) |
| Приложение не стартует на старом сервере после перехода на .NET 11 | Базовый уровень x86-64-v2: проверить парк железа | [.NET 11 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime) |
| Размер бинарника Native AOT велик | `OptimizationPreference=Size`, `InvariantGlobalization`, опции тримминга | [Optimizing AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing), [Trimming options](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options) |

---

## Бенчмарки: BenchmarkDotNet

### Ключевые выводы

- BenchmarkDotNet — стандарт де-факто. На октябрь 2026 года стабильная версия 0.15.8 (30 ноября 2025, добавлен экспорт OpenMetrics), есть 0.16.0-preview.2 ([NuGet](https://www.nuget.org/packages/BenchmarkDotNet)).
- Запуск только `dotnet run -c Release` без отладчика. Возвращайте результат из метода, иначе JIT может выбросить вычисление.
- Всегда добавляйте `[MemoryDiagnoser]`: столбец `Allocated` часто важнее `Mean`.
- `Baseline = true` даёт столбец `Ratio`. Это среднее распределения отношений, а не отношение средних. Смотрите также `RatioSD`.
- Сравнивайте версии рантайма одним запуском: `--runtimes net10.0 net11.0`. Так сделаны все примеры в статьях Тауба.
- Для сравнения «до/после» экспортируйте полный JSON (`--exporters fulljson` или `[JsonExporterAttribute.Full]`) и сравнивайте скриптом скилла [scripts/compare-bdn.cs](../scripts/compare-bdn.cs): `dotnet run compare-bdn.cs -- before after 5`. ResultsComparer из dotnet/performance делает то же, но вне своего репозитория не собирается (нужен внутренний Arcade SDK).
- Если SDK стоит не в стандартном месте (например, `.dotnet/` в репозитории), BDN 0.15.8 падает с `NullReferenceException` в `DotNetSdkValidator`. Передайте путь явно: `-- --cli <путь к dotnet>`.
- Диагносты: `DisassemblyDiagnoser` (ассемблер), `ThreadingDiagnoser` (contention, work items), `ExceptionDiagnoser`, `EventPipeProfiler` (кроссплатформенный профиль прямо из бенчмарка), `InliningDiagnoser`, `HardwareCounters` (Windows), диагносты dotTrace и dotMemory в отдельных пакетах.
- На шумных CI-раннерах разницу в 1–3% не считайте сигналом. Нужны повторные прогоны, фиксированное железо или порог с запасом.

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
// dotnet run -c Release -- --filter "*Parse*" --runtimes net10.0 net11.0

[MemoryDiagnoser]
public class ParseBench
{
    private readonly string _line = "station;12.3";

    [Benchmark(Baseline = true)]
    public double Split() => double.Parse(_line.Split(';')[1]);

    [Benchmark]
    public double Span()
    {
        ReadOnlySpan<char> s = _line;
        return double.Parse(s[(s.IndexOf(';') + 1)..]);
    }
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) / [releases](https://github.com/dotnet/BenchmarkDotNet/releases) | Andrey Akinshin и мейнтейнеры, .NET Foundation | Исходники, изменения версий |
| [Good practices](https://benchmarkdotnet.org/articles/guides/good-practices.html) | BenchmarkDotNet docs | Release, отладчик, dead code elimination, питание ноутбука |
| [Diagnosers](https://benchmarkdotnet.org/articles/configs/diagnosers.html) | BenchmarkDotNet docs | Полный список диагностов |
| [Baselines](https://benchmarkdotnet.org/articles/features/baselines.html) / [Jobs](https://benchmarkdotnet.org/articles/configs/jobs.html) / [Exporters](https://benchmarkdotnet.org/articles/configs/exporters.html) / [Console args](https://benchmarkdotnet.org/articles/guides/console-args.html) | BenchmarkDotNet docs | Сравнение, конфигурации, экспорт, фильтры и `--runtimes` |
| [Microbenchmark Design Guidelines](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md) | команда .NET | Как сама Microsoft пишет микробенчмарки |
| [ResultsComparer](https://github.com/dotnet/performance/blob/main/src/tools/ResultsComparer/README.md) | dotnet/performance | Статистическое сравнение двух прогонов; вне репозитория dotnet/performance не собирается (Arcade SDK), для своих проектов — `compare-bdn.cs` |
| [Profiling with BenchmarkDotNet in Visual Studio](https://learn.microsoft.com/en-us/visualstudio/profiling/profiling-with-benchmark-dotnet) | Microsoft | Диагносты BDN, открываемые в профайлере VS |
| [crank](https://github.com/dotnet/crank) / [aspnet/Benchmarks](https://github.com/aspnet/Benchmarks) | команда ASP.NET Core | Нагрузочные end-to-end бенчмарки, инфраструктура TechEmpower-прогонов |

---

## Профилирование и диагностика

### Ключевые выводы

- Начинайте с `dotnet-counters`: он без перезапуска покажет, что болит — CPU, GC, пул потоков, исключения или блокировки. Потом снимайте трассу.
- `dotnet-trace` работает везде, без нативного профайлера, на EventPipe. По умолчанию `collect` включает `dotnet-common` и `dotnet-sampled-thread-time`. Профиль `cpu-sampling` для `collect` [устарело] → `--profile dotnet-sampled-thread-time,dotnet-common`. Для памяти есть `gc-verbose` (сэмплы аллокаций), для GC с малыми накладными расходами — `gc-collect`, для запросов к БД — `database`.
- `dotnet-trace collect-linux` (preview, .NET 10+, ядро 6.4+, root) пишет нативные кадры и события ядра, может трассировать все процессы машины. Открывается свежим PerfView.
- Трассу в speedscope получают через `--format Speedscope` или `dotnet-trace convert`. Конвертация необратима, исходный `.nettrace` сохраняется.
- Утечки ищите сравнением двух `dotnet-gcdump`, в dotMemory или в VS. Рост памяти без роста кучи GC — признак нативной утечки, это отдельная история.
- PerfView остаётся самым глубоким инструментом для GC и JIT на Windows. Ultra (xoofx) даёт сэмплирование ETW до 8190 Гц и интерфейс Firefox Profiler на Windows и macOS (Apple Silicon); требует .NET 10 SDK.
- Чтобы проверить инлайнинг, bounds checks и векторизацию, смотрите ассемблер: Disasmo, `DisassemblyDiagnoser`, SharpLab.

```bash
dotnet-counters monitor -n MyService --counters System.Runtime
dotnet-trace collect -n MyService --duration 00:00:00:30            # CPU + базовые события
dotnet-trace collect -n MyService --profile gc-verbose -o allocs.nettrace
dotnet-trace convert allocs.nettrace --format Speedscope
dotnet-gcdump collect -n MyService
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace) | Microsoft, июнь 2026 | Профили, `collect-linux`, форматы, трассировка со старта |
| [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters) / [Built-in runtime metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime) / [dotnet-gcdump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump) | Microsoft | Что значит каждый счётчик; снимок управляемой кучи без полного дампа |
| [Debug high CPU](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-highcpu) / [Debug memory leak](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-memory-leak) | Microsoft | Пошаговые туториалы |
| [PerfView](https://github.com/microsoft/perfview) | Vance Morrison, Microsoft | GC Stats, JIT Stats, стеки аллокаций |
| [Ultra](https://github.com/xoofx/ultra) | Alexandre Mutel (xoofx), 2024–2026 | Сэмплирующий профайлер с UI Firefox Profiler |
| [speedscope](https://www.speedscope.app/) ([repo](https://github.com/jlfwong/speedscope)) | Jamie Wong | Просмотр flamegraph в браузере, в том числе на Linux |
| [Visual Studio profiling tour](https://learn.microsoft.com/en-us/visualstudio/profiling/profiling-feature-tour) / [.NET Object Allocation tool](https://learn.microsoft.com/en-us/visualstudio/profiling/dotnet-alloc-tool) | Microsoft | CPU Usage, аллокации, async-инструменты в VS |
| [dotTrace](https://www.jetbrains.com/profiler/) / [dotMemory](https://www.jetbrains.com/dotmemory/) | JetBrains | Коммерческие профайлеры CPU и памяти, интеграция с Rider и BDN |
| [Disasmo](https://github.com/EgorBo/Disasmo) / [SharpLab](https://sharplab.io/) / [Compiler Explorer](https://godbolt.org/) | Egor Bogatov / Andrey Shchekin / Matt Godbolt | Ассемблер JIT для метода |
| [Viewing JIT dumps](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md) | dotnet/runtime | Как получить дамп и дизассемблер JIT |

---

## GC и память

### Ключевые выводы

- Два режима: workstation (по умолчанию для консольных и десктопных приложений) и server (по умолчанию у ASP.NET Core). Фоновый (concurrent) GC включён по умолчанию.
- DATAS (Dynamic Adaptation To Application Sizes) появился в .NET 8 как опция и включён по умолчанию с .NET 9 для server GC. Он начинает с одной кучи и меняет их число по нагрузке, а бюджет gen0 привязывает к размеру живых данных. На TechEmpower на 48 ядрах это дало минус 80% рабочего набора и минус 2–3% максимального RPS. Если пропускная способность важнее памяти, проверьте с `GarbageCollectionAdaptationMode=0`.
- С .NET 7 куча устроена регионами (по 4 МБ для SOH по умолчанию), а не сегментами. Резервируется 256 ГБ виртуального адреса — это не потребление памяти.
- Объекты от 85 000 байт попадают в LOH и собираются только в gen2. Пулы буферов и потоковая обработка лучше, чем поднятие `LOHThreshold`.
- Долгоживущие закреплённые буферы (I/O, interop) выделяйте через `GC.AllocateArray<T>(n, pinned: true)` в POH, чтобы не фрагментировать обычную кучу.
- В контейнере GC видит лимит памяти контейнера; `HeapHardLimitPercent` по умолчанию 75%. `ConserveMemory` (0–9, документация советует начинать с 5–7) уменьшает кучу ценой более частых GC.
- `GC.Collect()` в продакшн-коде почти всегда ошибка. `GCSettings.LatencyMode = SustainedLowLatency` применяйте только к коротким критичным окнам.
- В .NET 10 новый write barrier на Arm64 сократил паузы GC на 8–20% и больше. В .NET 11 GC по-прежнему ограничен 1024 кучами, но рантайм запускается на машинах с более чем 1024 CPU.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Garbage collector config settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector) | Microsoft, обновлено 2026 | Все ручки GC: MSBuild, runtimeconfig, переменные `DOTNET_*` |
| [DATAS](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/datas) | Microsoft | Как работает адаптация, цифры, отключение |
| [Fundamentals of GC](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals) / [Workstation vs server](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/workstation-server-gc) / [Background GC](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/background-gc) / [LOH](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap) / [Latency modes](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/latency) / [GC and performance](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/performance) | Microsoft | Поколения, режимы, LOH, режимы задержки, анализ в WinDbg/SOS |
| [Put a DPAD on that GC!](https://devblogs.microsoft.com/dotnet/put-a-dpad-on-that-gc/) | .NET Blog, 2023 | Введение в DATAS (автор не перепроверен) |
| [Middle ground between server and workstation GC](https://devblogs.microsoft.com/dotnet/middle-ground-between-server-and-workstation-gc/) | Maoni Stephens | `GCHeapCount`, привязка к ядрам. Частично [устарело]: для типичных случаев теперь есть DATAS |
| [How segments and regions differ in decommitting memory](https://itnext.io/how-segments-and-regions-differ-in-decommitting-memory-in-the-net-7-gc-68c58465ab5a) | Maoni Stephens, 2022 | Регионы против сегментов |
| [BOTR: Garbage Collection Design](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/garbage-collection.md) | dotnet/runtime | Внутреннее устройство GC |

---

## Аллокации: Span, пулы, строки

### Ключевые выводы

- Для синхронных API принимайте `ReadOnlySpan<T>`/`Span<T>`, для асинхронных и полей — `ReadOnlyMemory<T>`/`Memory<T>`. Это правило №1 и №2 официального гайда. Span нельзя держать в поле класса и через `await`.
- `stackalloc` — для небольших буферов известного верхнего размера; большие и переменные размеры берите из `ArrayPool<T>.Shared.Rent` и возвращайте в `finally`. Арендованный массив может быть длиннее запрошенного, а без `clearArray: true` содержит старые данные.
- Типичные ошибки с `ArrayPool`: работать с `buffer.Length` вместо запрошенной длины; пользоваться массивом после `Return`; вернуть один массив дважды (документация называет оба случая «high-severity security issue»: массив уже может принадлежать другому коду); возвращать в пул массив, полученный не через `Rent`; забыть `clearArray: true` для секретов. Буфер, который уходит за пределы метода, оборачивайте во владельца (`IMemoryOwner<T>`) с одним явным местом возврата.
- `IMemoryOwner<T>` либо освобождают (`Dispose`), либо передают владение — но не то и другое сразу.
- `ObjectPool<T>` (Microsoft.Extensions.ObjectPool) помогает только для дорогих в создании объектов. По документации, если инициализация дешёвая, взять объект из пула обычно медленнее. Пул ограничивает число удерживаемых объектов, а не создаваемых.
- Строки: `string.Create` пишет в строку без промежуточного буфера; `StringBuilder` для сборки в цикле; интерполяция с .NET 6 идёт через `DefaultInterpolatedStringHandler` и почти не аллоцирует лишнего; `"..."u8` даёт UTF-8 байты без аллокации.
- Боксинг: структура, переданная как `object` или интерфейс, аллоцирует. Generic-метод с ограничением `where T : struct, IFoo` не боксит. Анализаторы CA18xx ловят часть случаев.
- Замыкания аллоцируют объект для захваченных переменных и делегат. С .NET 10 JIT может разместить делегат на стеке, но сам объект замыкания пока остаётся в куче. Используйте `static`-лямбды и перегрузки с `state`.
- JIT с .NET 9/10 сам размещает на стеке некоторые объекты, которые не покидают метод: боксы, небольшие массивы, делегаты. Это бонус, а не замена ручной работе: проверяйте `Allocated` в бенчмарке.
- `ValueTask<T>` полезен, когда результат обычно готов синхронно. Его нельзя ожидать дважды и нельзя хранить.

```csharp
// Буфер из пула вместо new byte[] на каждый вызов
byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
try
{
    int read = await stream.ReadAsync(buffer.AsMemory(0, 64 * 1024), ct);
    Process(buffer.AsSpan(0, read));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Memory<T> and Span<T> usage guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines) | Microsoft, 2025 | 10 правил владения и аренды буферов |
| [Reduce memory allocations using new C# features](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/) | Microsoft | `ref struct`, `in`, `ref readonly`, `scoped` на практике |
| [ref struct](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct) / [struct](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct) / [method parameters](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/method-parameters) | Microsoft | `readonly struct`, `in`-параметры и защитные копии |
| [ArrayPool<T>](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) / [Array Pool](https://adamsitnik.com/Array-Pool/) | Microsoft / Adam Sitnik, 2017 | Пулы и почему LOH дорог |
| [Span](https://adamsitnik.com/Span/) / [Value Types vs Reference Types](https://adamsitnik.com/Value-Types-vs-Reference-Types/) | Adam Sitnik, 2017 | Устройство Span и цена ссылочных типов. Детали рантайма частично [устарело], выводы верны |
| [ObjectPool in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/objectpool) | Günther Foidl, Steve Gordon, Samson Amaugo | Когда пул помогает и когда вредит |
| [String Interpolation in C# 10 and .NET 6](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/) | Stephen Toub, 2021 | Как работают interpolated string handlers |
| [Understanding the Whys, Whats, and Whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/) / [How Async/Await Really Works](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/) | Stephen Toub, 2018 / 2023 | Цена async и когда `ValueTask` оправдан |
| [ZString](https://github.com/Cysharp/ZString) / [MemoryPack](https://github.com/Cysharp/MemoryPack) | Cysharp (Yoshifumi Kawai) | Сторонние zero-allocation-строки и бинарная сериализация |
| [CA18xx performance rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/performance-warnings) | Microsoft | Анализаторы: включите в CI как предупреждения |

---

## Коллекции и поиск

### Ключевые выводы

- `FrozenDictionary`/`FrozenSet` (.NET 8+): дорогое создание, очень быстрый поиск. Строить один раз при старте, использовать долго. Для данных, которые часто перестраиваются, не подходят.
- `SearchValues<T>` (.NET 8+): набор символов, байтов или строк для `IndexOfAny`/`ContainsAny`. Создаётся один раз в `static readonly` поле; внутри векторизованные алгоритмы.
- `CollectionsMarshal`: `AsSpan(List<T>)` даёт Span над внутренним массивом списка; `GetValueRefOrAddDefault` — запись в словарь за один поиск. Не меняйте коллекцию, пока держите Span или ref.
- Alternate lookup (.NET 9+): поиск в `Dictionary<string, T>`, `HashSet<string>` и frozen-коллекциях по `ReadOnlySpan<char>` без создания строки.
- `ImmutableArray<T>` — обёртка над массивом с быстрым чтением; `ImmutableList<T>` — дерево, дешевле при частых изменениях, но медленнее при чтении и переборе.
- Задавайте ёмкость, если размер известен: `new List<T>(n)`, `new Dictionary<K,V>(n)`; в C# 15 — `[with(capacity: n), ..items]` в collection expression.
- Свои ключи-структуры: реализуйте `IEquatable<T>` и нормальный `GetHashCode`, иначе сравнение пойдёт через `object.Equals` с боксингом.
- В .NET 11 `EqualityComparer<T>.Default` и `Comparer<T>.Default` специализированы в ReadyToRun-образах: по документации, до 20 раз быстрее до перехода на tier-1.

```csharp
private static readonly SearchValues<char> s_separators = SearchValues.Create(",;|\t");
private static readonly FrozenDictionary<string, int> s_codes =
    LoadCodes().ToFrozenDictionary(StringComparer.Ordinal);

int idx = line.AsSpan().IndexOfAny(s_separators);

// Счётчик слов без создания строки на каждый токен
var counts = new Dictionary<string, int>(StringComparer.Ordinal);
var lookup = counts.GetAlternateLookup<ReadOnlySpan<char>>();
foreach (Range r in text.AsSpan().Split(' '))
{
    ReadOnlySpan<char> word = text.AsSpan()[r];
    if (lookup.TryGetValue(word, out int c)) lookup[word] = c + 1;
    else lookup[word] = 1;   // строка создаётся только для нового ключа
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [FrozenDictionary<TKey,TValue>](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2) | Microsoft | Компромисс «создание против поиска» |
| [SearchValues<T>](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.searchvalues-1) | Microsoft | Кэшируемые наборы для поиска |
| [CollectionsMarshal](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal) | Microsoft | `AsSpan`, `GetValueRefOrAddDefault`, `SetCount` |
| [Dictionary.GetAlternateLookup](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.getalternatelookup) | Microsoft | Поиск по Span-ключу |
| [ImmutableArray<T>](https://learn.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablearray-1) | Microsoft | Когда выбирать вместо `ImmutableList<T>` |

---

## JIT, PGO, ReadyToRun и Native AOT

### Ключевые выводы

- Tiered compilation: сначала быстрый неоптимизированный код (tier-0) или готовый код ReadyToRun, потом горячие методы перекомпилируются с оптимизациями (tier-1). Циклы переходят на оптимизированный код через OSR (On-Stack Replacement); с .NET 7 это включено по умолчанию на x64 и Arm64.
- Dynamic PGO включён по умолчанию с .NET 8: tier-0 собирает профиль типов и ветвлений, tier-1 делает guarded devirtualization и раскладку кода по нему. Для Bing это был главный источник выигрыша .NET 8. Не отключайте `TieredPGO` без замера.
- Следствие tiering: первые секунды после старта код медленнее. Делайте прогрев до приёма трафика и учитывайте это в бенчмарках (BDN прогревает сам).
- JIT в .NET 10 и 11 убирает многие «налоги на абстракции»: девиртуализация интерфейсов массивов и generic virtual methods, инлайнинг методов с `try/finally`, escape analysis, удаление bounds checks для `(uint)i < span.Length`, `i + c < len`, `span[^1]`, после проверки `!span.IsEmpty`. Новая версия рантайма часто ускоряет абстрактный код сильнее ручных правок.
- ReadyToRun (`PublishReadyToRun`) уменьшает JIT при старте и первом вызове. Сборки в 2–3 раза больше; горячие методы всё равно переходят на tier-1. Composite R2R нужен редко.
- Native AOT (`PublishAot`) даёт быстрый старт, меньший рабочий набор и работу без установленного рантайма. Ограничения: нет `Assembly.LoadFile` и `Reflection.Emit`, обязателен тримминг, `System.Linq.Expressions` только интерпретируются, generic-инстанциации на структурах увеличивают размер. Пиковая пропускная способность без Dynamic PGO может быть ниже, чем у JIT; меряйте на своей нагрузке.
- `OptimizationPreference` (`Size` или `Speed`) меняет баланс Native AOT; по умолчанию смешанный режим.
- Для библиотек: `IsAotCompatible=true` включает анализаторы тримминга и AOT; `VerifyReferenceAotCompatibility` (метаданные с .NET 10) проверяет зависимости.
- Предупреждения тримминга и AOT (IL2026 — вызов метода с `[RequiresUnreferencedCode]`, IL3050 — `[RequiresDynamicCode]`, IL2070–IL2075 — рефлексия без аннотаций) не глушите: опубликованное приложение упадёт в рантайме там, где компилятор предупреждал. Порядок исправления по документации: убрать рефлексию (source generators для JSON, regex, логирования, DI), если типы известны — `[DynamicallyAccessedMembers]` с минимальным набором членов, если код действительно динамический — пометить его `[RequiresUnreferencedCode]` и поднять требование к вызывающим. `[UnconditionalSuppressMessage]` — только с доказательством, что код безопасен; `#pragma warning disable` и `[SuppressMessage]` тут не работают, тример их не видит. Цель — ноль предупреждений при `dotnet publish` ([Fixing trim warnings](https://github.com/dotnet/docs/blob/main/docs/core/deploying/trimming/fixing-warnings.md), [Fixing AOT warnings](https://github.com/dotnet/docs/blob/main/docs/core/deploying/native-aot/fixing-warnings.md)).
- С .NET 11 минимальный x86-уровень — x86-64-v2, а R2R-код для Windows и Linux собирается под x86-64-v3 (AVX2). На железе старше рантайм не запустится, а на машинах без AVX2 R2R-код будет перекомпилирован JIT.
- Runtime async в .NET 11 — preview: async-машина состояний переносится из компилятора в рантайм. По Таубу: async-код вдвое меньше, синхронное завершение двухуровневой цепочки быстрее в 3 раза, исключения в глубоких цепочках — примерно в 5 раз. Библиотеки .NET 11 уже собраны с ним.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Compilation config settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation) | Microsoft, 2026 | `TieredCompilation`, `TieredPGO`, QuickJit, ReadyToRun. Фраза про `QuickJitForLoops` «по умолчанию выключено» [устарело]: с .NET 7 включено вместе с OSR на x64/Arm64 |
| [Tiered compilation](https://github.com/dotnet/runtime/blob/main/docs/design/features/tiered-compilation.md) / [Dynamic PGO](https://github.com/dotnet/runtime/blob/main/docs/design/features/DynamicPgo.md) / [OSR](https://github.com/dotnet/runtime/blob/main/docs/design/features/OnStackReplacement.md) | dotnet/runtime | Дизайн-документы JIT |
| [Object stack allocation](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/object-stack-allocation.md) | dotnet/runtime | Что и когда JIT кладёт на стек |
| [Conversation about PGO](https://devblogs.microsoft.com/dotnet/conversation-about-pgo/) / [Conversation about ReadyToRun](https://devblogs.microsoft.com/dotnet/conversation-about-ready-to-run/) | .NET Blog, 2021–2022 | Интервью с разработчиками JIT. Цифры [устарело], модель верна |
| [What's new in .NET 10 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime) | Microsoft, 2025 | Стековые массивы, escape analysis для делегатов, AVX10.2 (выключен по умолчанию), write barrier на Arm64 |
| [What's new in .NET 11 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime) | Microsoft, RC1, сентябрь 2026 | Runtime async, новые требования к железу, девиртуализация, R2R-компараторы |
| [ReadyToRun deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run) | David Wrighton, Microsoft | Компромиссы R2R и composite |
| [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) / [Optimizing AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing) / [ASP.NET Core + Native AOT](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot) | Microsoft | Ограничения, анализаторы, режимы оптимизации, поддержка в вебе |

---

## SIMD и векторизация

### Ключевые выводы

- Первый шаг — готовые векторизованные API: `MemoryExtensions` (`IndexOf`, `SequenceEqual`, `Count`), `SearchValues`, `TensorPrimitives` (Sum, Dot, CosineSimilarity, Exp, Asin и сотни других для `Span<T>`). Они уже выбирают AVX-512, AVX2 или NEON.
- Явный SIMD: кроссплатформенные `Vector128<T>`, `Vector256<T>`, `Vector512<T>` (с .NET 8) и `Vector<T>` переменной ширины. Ветвитесь по `Vector256.IsHardwareAccelerated` и пишите хвостовой скалярный цикл.
- Платформенные intrinsics (`System.Runtime.Intrinsics.X86.Avx2`, `Arm.AdvSimd`, `Arm.Sve`) — только когда кроссплатформенного API не хватает. Arm SVE в .NET 9 помечен `[Experimental]`; AVX10.2 в .NET 10 выключен по умолчанию.
- .NET 11 добавил API раскладки дорожек (`Zip`, `Unzip`, `Concat*`, `CreateGeometricSequence`) для `Vector64/128/256/512<T>` и `Vector<T>`.
- JIT сам объединяет соседние записи в одну SIMD-запись (.NET 9) и убирает bounds checks в простых циклах. Проверяйте результат в дизассемблере.
- Для дистрибуции не завязывайтесь на AVX-512 без проверки `IsSupported`: такой код упадёт на другом CPU.

```csharp
float sim = TensorPrimitives.CosineSimilarity(a, b);   // сначала готовые векторизованные API

static int CountGreater(ReadOnlySpan<int> data, int threshold)
{
    int i = 0, count = 0;
    if (Vector256.IsHardwareAccelerated)
        for (var t = Vector256.Create(threshold); i <= data.Length - Vector256<int>.Count; i += Vector256<int>.Count)
            count += BitOperations.PopCount(Vector256.GreaterThan(Vector256.Create(data.Slice(i)), t).ExtractMostSignificantBits());
    for (; i < data.Length; i++) if (data[i] > threshold) count++;   // скалярный хвост
    return count;
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [SIMD-accelerated types in .NET](https://learn.microsoft.com/en-us/dotnet/standard/simd) | Microsoft | `Vector<T>`, векторные типы `System.Numerics` |
| [Vector512<T>](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.vector512) | Microsoft | API кроссплатформенных векторов |
| [TensorPrimitives](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.tensors.tensorprimitives) | Microsoft | Готовые векторизованные операции над Span (пакет System.Numerics.Tensors) |
| [.NET 8 hardware intrinsics](https://devblogs.microsoft.com/dotnet/dotnet-8-hardware-intrinsics/) | .NET Blog, 2023 | AVX-512, `Vector512`, кроссплатформенные паттерны (автор не перепроверен) |
| Разделы «Vectorization» в [Performance Improvements in .NET 9](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/) | Stephen Toub, 2024 | SVE, AVX10.1, слияние записей |

---

## I/O, сериализация, regex, логирование

### Ключевые выводы

- `System.IO.Pipelines` берёт на себя буферы, пулы и backpressure при разборе потоковых данных. Всегда вызывайте `AdvanceTo` после `ReadAsync`, не используйте буфер после `AdvanceTo`, завершайте reader и writer. На нём построен Kestrel.
- Файлы: `RandomAccess.Read/Write` по смещению без общего состояния позиции — удобно для параллельного чтения. `FileStream` переписан в .NET 6 и заметно быстрее; для асинхронных операций открывайте его с `FileOptions.Asynchronous`.
- `System.Text.Json` с source generator (`[JsonSerializable]` + `JsonSerializerContext`) убирает рефлексию на старте, нужен для Native AOT и тримминга, ускоряет сериализацию. Newtonsoft.Json как выбор по умолчанию для новых сервисов [устарело] → System.Text.Json.
- Regex: `[GeneratedRegex]` генерирует код при компиляции — нет затрат на старте, как у `RegexOptions.Compiled`, и выше скорость, чем у интерпретатора. Для недоверенных шаблонов и входа — `RegexOptions.NonBacktracking`.
- Логирование: `[LoggerMessage]` на partial-методах проверяет уровень до форматирования и не боксит аргументы. Интерполяция в `logger.LogInformation($"...")` аллоцирует строку всегда (анализаторы CA1848, CA2254). Ручной `LoggerMessage.Define` [устарело] → генератор.

```csharp
[JsonSerializable(typeof(Order))]
internal partial class AppJsonContext : JsonSerializerContext;

internal static partial class Patterns
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant)]
    internal static partial Regex IsoDate();
}

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} processed in {Ms} ms")]
    internal static partial void OrderProcessed(ILogger logger, long orderId, double ms);
}

string json = JsonSerializer.Serialize(order, AppJsonContext.Default.Order);
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [System.IO.Pipelines](https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines) | Microsoft, 2026 | Шаблоны чтения и типичные ошибки (зависания, OOM, use-after-free) |
| [System.IO.Pipelines: High performance IO in .NET](https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/) | David Fowler, 2018 | Зачем появились Pipelines |
| [RandomAccess](https://learn.microsoft.com/en-us/dotnet/api/system.io.randomaccess) / [File IO improvements in .NET 6](https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/) | Microsoft / Adam Sitnik, 2021 | Новый `FileStream`, `RandomAccess`, буферизация |
| [STJ source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) | Microsoft | Режимы metadata и fast-path, настройка контекста |
| [Regular expression source generators](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-source-generators) / [Regex improvements in .NET 7](https://devblogs.microsoft.com/dotnet/regular-expression-improvements-in-dotnet-7/) | Microsoft / Stephen Toub, 2022 | Генератор, NonBacktracking, устройство движков |
| [Compile-time logging source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) / [High-performance logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/high-performance-logging) | Microsoft | `[LoggerMessage]` и почему он быстрее |
| [RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream) | Microsoft (Bing) | Пулы для `MemoryStream`, без LOH |

---

## EF Core и ASP.NET Core

### Ключевые выводы

- В приложениях с БД время почти всегда уходит на запросы и сеть, а не на C#. Сначала смотрите сгенерированный SQL, число запросов на операцию и индексы.
- EF Core: `AsNoTracking()` для чтения; проекция `Select` только нужных полей; пагинация; без N+1 (ленивая загрузка в цикле — главный источник); split queries против «картезианского взрыва» при нескольких `Include`.
- Накладные расходы самого EF: `AddDbContextPool` (в документации: 701.6 → 350.1 мкс и 50 → 4.6 КБ на простой запрос), `EF.CompileAsyncQuery` для горячих запросов, compiled model (`dotnet ef dbcontext optimize`) — только для больших моделей с медленным первым запросом.
- Не стройте динамические деревья выражений с константами: каждый вызов перекомпилирует запрос и засоряет кэш планов (в документации 1665.8 мкс против 757.1 мкс). Метрика Query Cache Hit Rate должна быстро подниматься до 100%.
- `EnableThreadSafetyChecks=false` даёт немного, но отключает защиту от конкурентного использования контекста — только после тестов.
- Для горячих чтений, где EF не нужен, есть Dapper (родом из Stack Overflow).
- ASP.NET Core: всё асинхронно по всему стеку, без `.Result`/`.Wait()`; не делать `Task.Run` + немедленный `await`; не читать большие тела запросов целиком (LOH и риск OOM); `IHttpClientFactory`; сжатие ответов; кэширование; исключения не для управления потоком.
- Каждая новая версия ASP.NET Core быстрее предыдущей; обновление — дешёвая оптимизация.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [EF Core performance](https://learn.microsoft.com/en-us/ef/core/performance/) / [Efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) / [Performance diagnosis](https://learn.microsoft.com/en-us/ef/core/performance/performance-diagnosis) | Microsoft | Индексы, проекции, N+1, логирование SQL, метрики |
| [Advanced performance topics](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics) | Microsoft, 2025 | Пулинг, compiled queries и models, параметризация, кэш |
| [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) | Mike Rousos, Microsoft | Блокирующие вызовы, LOH, HttpClient, HttpContext в фоне |
| [ASP.NET Core performance overview](https://learn.microsoft.com/en-us/aspnet/core/performance/overview) | Microsoft | Кэширование, сжатие, rate limiting, нагрузочное тестирование |
| [Performance improvements in ASP.NET Core 8](https://devblogs.microsoft.com/dotnet/performance-improvements-in-aspnet-core-8/) | .NET Blog, 2023 | Что ускорилось в Kestrel, маршрутизации, JSON |
| [Dapper](https://github.com/DapperLib/Dapper) | Stack Exchange | Микро-ORM для горячих путей |

---

## Конфигурация сборки и рантайма

### Ключевые выводы

- Большинство настроек GC и JIT задаются свойствами MSBuild в `.csproj` и попадают в `runtimeconfig.json`. Для настроек без своего свойства используйте `RuntimeHostConfigurationOption`. Переменные окружения начинаются с `DOTNET_`; префикс `COMPlus_` [устарело] (работает, но не рекомендуется).
- Значения по умолчанию в .NET 10 уже хорошие: `TieredCompilation=true`, `TieredPGO=true`, фоновый GC, DATAS для server GC. Меняйте только то, что подтвердил замер.
- Настройки GC читаются при старте процесса; поменять их для работающего процесса нельзя.
- `InvariantGlobalization=true` убирает загрузку ICU: меньше размер и быстрее старт, особенно в контейнерах и с Native AOT. Но культурно-зависимое форматирование и сравнение станут инвариантными — проверьте, что приложению это подходит.
- `Release` — обязательная конфигурация для всего, что измеряется. `Optimize=true` в Debug ломает отладку и не делает сборку эквивалентной Release.

```xml
<!-- Сервис: server GC, быстрый старт, без ICU -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <ServerGarbageCollection>true</ServerGarbageCollection>      <!-- по умолчанию true в Web SDK -->
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
  <!-- <GarbageCollectionAdaptationMode>0</GarbageCollectionAdaptationMode>  отключить DATAS, если упал RPS -->
  <TieredPGO>true</TieredPGO>                                    <!-- по умолчанию с .NET 8 -->
  <InvariantGlobalization>true</InvariantGlobalization>
  <PublishReadyToRun>true</PublishReadyToRun>                    <!-- нужен RID при publish -->
</PropertyGroup>

<!-- Ручки без MSBuild-свойства -->
<ItemGroup>
  <RuntimeHostConfigurationOption Include="System.GC.ConserveMemory" Value="5" />
  <RuntimeHostConfigurationOption Include="System.GC.HeapHardLimitPercent" Value="70" />
</ItemGroup>

<!-- CLI или функция: Native AOT (dotnet publish -c Release -r linux-x64) -->
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <OptimizationPreference>Speed</OptimizationPreference>         <!-- или Size -->
  <IsAotCompatible>true</IsAotCompatible>                        <!-- для библиотек: анализаторы AOT -->
  <Features>$(Features);runtime-async=on</Features>              <!-- .NET 11, preview -->
</PropertyGroup>
```

| Свойство | Что даёт | Цена / риск |
|---|---|---|
| `ServerGarbageCollection` | Куча на ядро, высокая пропускная способность | Больше памяти; в .NET 9+ смягчено DATAS |
| `ConcurrentGarbageCollection=false` | Чуть меньше накладных расходов GC | Длиннее паузы gen2 |
| `GarbageCollectionAdaptationMode=0` | Отключает DATAS, максимум RPS | Рабочий набор растёт с числом ядер |
| `RetainVMGarbageCollection` | Не возвращать память ОС, меньше системных вызовов | Процесс держит память |
| `System.GC.ConserveMemory` (1–9) | Меньше куча, компактизация LOH | Чаще GC, длиннее паузы |
| `System.GC.HeapHardLimit(Percent)` | Жёсткий лимит кучи | `OutOfMemoryException` при недооценке |
| `TieredPGO=false` | Предсказуемый код без профиля | Обычно медленнее в установившемся режиме |
| `TieredCompilation=false` | Сразу оптимизированный JIT-код | Медленный старт, нет Dynamic PGO |
| `PublishReadyToRun` | Быстрее старт и первый вызов | Сборки в 2–3 раза больше |
| `PublishAot` | Самый быстрый старт, малый рабочий набор, нет зависимости от рантайма | Нет динамической загрузки и Emit, обязателен тримминг, нет Dynamic PGO |
| `PublishTrimmed` | Меньше размер | Ломает рефлексию без аннотаций |
| `InvariantGlobalization` | Нет ICU, меньше и быстрее | Инвариантная культура везде |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Runtime configuration options](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/) | Microsoft | Индекс всех настроек рантайма |
| [GC settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector) / [Compilation settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation) / [Threading settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/threading) / [Globalization settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization) | Microsoft | Точные имена свойств и значения по умолчанию |
| [MSBuild properties for .NET SDK](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props) | Microsoft | `PublishAot`, `PublishReadyToRun`, `InvariantGlobalization` и другие |
| [Trimming options](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options) | Microsoft | Отключение возможностей фреймворка ради размера |

---

## Обзоры Performance Improvements in .NET

Ежегодная статья Стивена Тауба выходит перед релизом и содержит сотни самодостаточных бенчмарков на BenchmarkDotNet. Читайте последнюю, а предыдущие — для понимания, когда появилась возможность.

### Ключевые выводы

- Эти статьи — лучший источник «что в рантайме уже оптимизировано за вас», чтобы не писать ручных обходов.
- Примеры в них — готовые шаблоны бенчмарков (`--runtimes net10.0 net11.0`).
- Совет «обновите рантайм» в каждом выпуске подтверждается цифрами: от бесплатного удаления боксинга `Nullable<T>` (.NET 11: 9.6 → 2.0 нс) до девиртуализации (.NET 10: индексатор `ReadOnlyCollection` 1960 → 625 нс).

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Performance Improvements in .NET 11](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-11/) | Stephen Toub, 15 сентября 2026 | Runtime async, девиртуализация generic virtual methods, bounds checks, переписанный `BigInteger` (умножение на 56% быстрее), ковариантные записи в массивы |
| [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/) | Stephen Toub, 10 сентября 2025 | Escape analysis, стековые массивы, девиртуализация, клонирование циклов по Span, инлайнинг `try/finally` |
| [Performance Improvements in .NET 9](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/) | Stephen Toub, 12 сентября 2024 | PGO для приведений типов, оптимизации циклов, Arm SVE, AVX10.1, LINQ |
| [Performance Improvements in .NET 8](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/) | Stephen Toub, 13 сентября 2023 | Dynamic PGO по умолчанию, AVX-512, Frozen collections, `SearchValues` |
| [Performance Improvements in .NET 7](https://devblogs.microsoft.com/dotnet/performance_improvements_in_net_7/) | Stephen Toub, 2022 | OSR, regex. Частично [устарело] → выпуски .NET 8–11 |
| [Process API improvements in .NET 11](https://devblogs.microsoft.com/dotnet/process-api-improvements-in-dotnet-11/) | Adam Sitnik, май 2026 | Ускорение и новые возможности `System.Diagnostics.Process` |

---

## Разборы реальных случаев

### Ключевые выводы

- **Bing на .NET 8** (Ben Watson, март 2024). Минус 13% CPU-циклов на запрос, на 20% меньше запросов, задетых gen0/gen1 GC, задержка лучше примерно на 8% (внутренние — более 25%). Главная причина — Dynamic PGO, хотя для 2 ГБ кода и 2 млн методов его пришлось настраивать. По словам автора, это самый заметный прирост с момента перехода с .NET Framework на .NET 5.
- **Microsoft Teams на .NET 6** (2022). Свыше 600 микросервисов; в среднем 29% экономии на Azure Compute, улучшение P99 CPU и P99 задержки на 30–50%; в звонках минус 67% ядер. Метод — side-by-side сравнение («Q-factor») старой и новой версии под одинаковой нагрузкой.
- **Copilot Studio на .NET 10 в WebAssembly** (май 2026). Примерно на 20% быстрее холодный путь и на 5% — горячий; пакет вырос на 15% из-за нового значения по умолчанию `WasmStripILAfterAOT`. Урок: обновление даёт скорость, но проверяйте размер.
- **Dependabot NuGet updater** (август 2025). Переписан с Ruby и ручного разбора XML на нативный .NET с официальными NuGet и MSBuild API: тесты с 26 до 9 минут (минус 65%), доля успешных обновлений с 82% до 94%. Скорость дало устранение лишней работы и правильные инструменты.
- **1BRC на .NET** (январь 2024). Решения nietras, xoofx и buybackoff сошлись на одном рецепте: чтение файла чанками (mmap или `RandomAccess` с отдельным хэндлом на поток), разбор байтов, а не строк, поиск `;` через `Vector256`, свой хэш-словарь под ключи до 32 байт с выравниванием по 64 байта, слияние локальных результатов потоков. Быстрые решения на C# оказались в одной лиге с лучшими на Java и C++.
- **Stack Overflow**. Dapper вырос из проблем масштабирования Linq-2-SQL; архитектура держится на агрессивном кэшировании. Переход на .NET Core, по словам Ника Крейвера (твит, август 2018), делался не ради производительности: главными были сопровождение, open source, SDK-проекты и контейнеры, а скорость — «100% in the bonus category» ([пересказ твита](https://wiert.me/2018/08/21/nick-craver-on-twitter-this-is-worth-repeating-were-migrating-stack-overflow-to-net-core-its-not-because-of-performance-there-are-enough-major-wins-without-even-factoring-performance-fo)).
- Общий урок: крупные выигрыши структурные (меньше работы, меньше аллокаций, правильный алгоритм) плюс бесплатный прирост от свежего рантайма. Флаги дают проценты сверху.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Bing on .NET 8: The Impact of Dynamic PGO](https://devblogs.microsoft.com/dotnet/bing-on-dotnet-8-the-impact-of-dynamic-pgo/) | Ben Watson, 2024 | Цифры и настройка PGO на огромной кодовой базе. Предыдущий шаг: [Bing from .NET 5 to .NET 7](https://devblogs.microsoft.com/dotnet/dotnet-performance-delivers-again-for-bing-from-dotnet-5-to-dotnet-7/) (2023) |
| [Microsoft Teams Infrastructure and ACS journey to .NET 6](https://devblogs.microsoft.com/dotnet/microsoft-teams-infrastructure-and-azure-communication-services-journey-to-dotnet-6/) | Siavash Fathi и др., 2022 | Методика Q-factor и цифры по доменам |
| [Modernizing push notification API for Teams](https://devblogs.microsoft.com/dotnet/modernizing-push-notification-api-for-teams/) / [One Service journey to .NET 6](https://devblogs.microsoft.com/dotnet/one-service-journey-to-dotnet-6/) / [Teams MiddleTier](https://dotnet.microsoft.com/en-us/platform/customers/microsoft-teams-middletier) | .NET Blog / Microsoft, 2022–2025 | Ещё отчёты о миграциях с замерами. MiddleTier после перехода на .NET Core 3.1: 25% выигрыша по CPU и около 25% снижения затрат на инфраструктуру (по странице кейса) |
| [Copilot Studio gets faster with .NET 10 on WebAssembly](https://devblogs.microsoft.com/dotnet/copilot-studio-dotnet-10-migration/) | Daniel Roth, 2026 | Скорость против размера в WASM. Предыстория: [How Copilot Studio uses .NET and WebAssembly](https://devblogs.microsoft.com/dotnet/copilot-studio-dotnet-wasm/) |
| [The new Dependabot NuGet updater: 65% faster with native .NET](https://devblogs.microsoft.com/dotnet/the-new-dependabot-nuget-updater/) | Jamie Magee, Brett Forsgren, 2025 | Переписывание ради правильных инструментов |
| [1brc.cs](https://github.com/nietras/1brc.cs/blob/main/README.md) / [fast1brc](https://github.com/xoofx/fast1brc) / [buybackoff/1brc](https://github.com/buybackoff/1brc) | nietras / Alexandre Mutel / Victor Baybekov, 2024 | Три лучших .NET-решения 1BRC с объяснениями |
| [1BRC results are in](https://www.morling.dev/blog/1brc-results-are-in/) | Gunnar Morling, 2024 | Условия и итоги челленджа |
| [Stack Overflow: The Architecture, 2016 Edition](https://nickcraver.com/blog/2016/02/17/stack-overflow-the-architecture-2016-edition/) / [How We Do App Caching](https://nickcraver.com/blog/2019/08/06/stack-overflow-how-we-do-app-caching/) | Nick Craver, 2016 / 2019 | Архитектура и многоуровневый кэш. Детали инфраструктуры [устарело], принципы верны |

---

## Антипаттерны

### Ключевые выводы

- Большинство ловится профилем, `dotnet-counters`, анализаторами CA18xx и чтением чеклиста ASP.NET Core Best Practices.
- Вне горячего пути антипаттерн часто можно не трогать: читаемость тоже стоит денег.

| Антипаттерн | Почему плохо | Замена |
|---|---|---|
| Бенчмарк в Debug, под отладчиком или без использования результата | В 10–100 раз медленнее или JIT выбрасывает вычисление | `dotnet run -c Release`, BenchmarkDotNet, вернуть значение |
| `Stopwatch` вокруг одного вызова | Нет прогрева и tiering, шум, нет статистики | BenchmarkDotNet с baseline |
| `.Result`, `.Wait()`, `GetAwaiter().GetResult()` в сервере | Голодание пула потоков, скачки задержки | `async`/`await` по всему стеку |
| `Task.Run(...)` + сразу `await` в ASP.NET Core | Лишняя постановка в пул, ничего не даёт | Вызвать метод напрямую |
| `new HttpClient()` на каждый запрос | Исчерпание сокетов (`TIME_WAIT`) | `IHttpClientFactory` или общий экземпляр |
| Исключения для управления потоком | Бросок и раскрутка стека дороги | `TryParse`, `TryGetValue`, проверка условий |
| Чтение тела запроса или файла целиком в `string`/`byte[]` | LOH, gen2, риск OOM | Потоковая обработка, Pipelines, `ArrayPool` |
| `string +=` в цикле | Квадратичное копирование | `StringBuilder`, `string.Join`, `string.Create` |
| `Substring`/`Split` в разборе | Аллокация на каждый фрагмент | `ReadOnlySpan<char>`, `MemoryExtensions.Split` |
| Интерполяция в вызове логгера | Строка создаётся даже при выключенном уровне | `[LoggerMessage]` |
| `RegexOptions.Compiled` или `new Regex(...)` на каждый вызов | Дорогой старт или повторная компиляция | `[GeneratedRegex]` в статическом partial-методе |
| `SearchValues.Create`/`ToFrozenDictionary` на каждый вызов | Дорогое создание съедает выигрыш | `static readonly` поле |
| LINQ и лямбды с захватом в tight loop | Аллокации перечислителей и замыканий | Цикл по Span, `static`-лямбды |
| `struct` через интерфейс или `object` | Боксинг на каждый вызов | Generic с ограничением, `IEquatable<T>` |
| `GC.Collect()` в продакшн-коде | Полная сборка, ломает эвристики GC | Убрать; разбираться с причиной роста |
| Server GC без лимитов в маленьком контейнере | Раздутая куча, OOM-kill | DATAS (по умолчанию в .NET 9+), `HeapHardLimitPercent`, `ConserveMemory` |
| EF Core: tracking-запросы для чтения | Лишняя память и CPU на отслеживание | `AsNoTracking()` |
| EF Core: ленивая загрузка в цикле | N+1 запросов | Проекция, `Include`, split queries |
| EF Core: константы в динамических деревьях выражений | Перекомпиляция запроса, засорение кэша планов | Параметры (захват переменной) |
| Native AOT «чтобы было быстрее» для долгоживущего сервиса | Нет Dynamic PGO, пиковая пропускная способность может быть ниже | Меряйте; для пропускной способности — JIT + R2R |
| Старый рантайм ради «стабильности» | Теряются годы бесплатных оптимизаций | .NET 10 LTS; .NET 8 и 9 поддерживаются только до 10.11.2026 |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) | Mike Rousos, Microsoft | Каталог серверных антипаттернов с кодом «так нельзя / так можно» |
| [Good practices](https://benchmarkdotnet.org/articles/guides/good-practices.html) | BenchmarkDotNet | Антипаттерны измерений |
| [CA18xx performance rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/performance-warnings) | Microsoft | Автоматическое обнаружение многих пунктов таблицы |
| [Advanced performance topics (EF Core)](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics) | Microsoft | Антипаттерны динамических запросов и пулинга |
| [.NET Memory Performance Analysis](https://github.com/Maoni0/mem-doc/blob/master/doc/.NETMemoryPerformanceAnalysis.md) | Maoni Stephens | Ошибки при анализе памяти и GC |

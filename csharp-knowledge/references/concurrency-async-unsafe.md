# Конкурентность, async, unsafe и нативный interop в .NET

Справочник для выбора модели конкурентности и примитивов синхронизации, для обхода ловушек async (sync-over-async, `async void`, голодание пула потоков, отмена) и для безопасной работы с `unsafe`, указателями и нативным кодом. Актуально на октябрь 2026 года: .NET 10 LTS и C# 14 — рабочая база, .NET 11 (STS, релиз запланирован на 10 ноября 2026) и C# 15 в RC1 с go-live-лицензией. Две главные новости последних лет — runtime async (машину состояний async строит рантайм, а не компилятор) и переопределение `unsafe` по образцу Rust (preview в .NET 11, релиз ожидается в .NET 12–13).

- [Канонические источники](#канонические-источники)
- [Потоки, задачи или async](#потоки-задачи-или-async)
- [Async/await: правила](#asyncawait-правила)
- [Task и ValueTask](#task-и-valuetask)
- [Отмена, таймауты, TimeProvider](#отмена-таймауты-timeprovider)
- [SynchronizationContext и ConfigureAwait](#synchronizationcontext-и-configureawait)
- [Голодание пула потоков](#голодание-пула-потоков)
- [Каналы, Dataflow, Parallel, PLINQ](#каналы-dataflow-parallel-plinq)
- [Блокировки](#блокировки)
- [Interlocked, volatile и модель памяти](#interlocked-volatile-и-модель-памяти)
- [Конкурентные и неизменяемые коллекции](#конкурентные-и-неизменяемые-коллекции)
- [Акторы](#акторы)
- [Runtime async (.NET 10–11)](#runtime-async-net-1011)
- [Unsafe: практика](#unsafe-практика)
- [Эволюция unsafe (preview в .NET 11)](#эволюция-unsafe-preview-в-net-11)
- [Нативный interop](#нативный-interop)
- [Что выбрать](#что-выбрать)
- [Чеклист review: конкурентный и unsafe-код](#чеклист-review-конкурентный-и-unsafe-код)

## Канонические источники

### Ключевые выводы
- Минимальный набор: статья Stephen Toub о том, как устроен async/await, AsyncGuidance Дэвида Фаулера, ConfigureAwait FAQ, статья о ValueTask, спецификация модели памяти .NET и «Native interoperability best practices».
- Stephen Cleary объясняет async понятнее всех: «There Is No Thread» — лучшее введение, книга «Concurrency in C# Cookbook» — сборник рецептов.
- «Threading in C#» Джо Албахари по-прежнему хорошо объясняет потоки, `Monitor`, `Wait/Pulse`, но части про APM/EAP и `Thread.Abort` [устарело].
- Советы до 2019 года про `ConfigureAwait(false)` «везде» и про `Task.Run` как «асинхронизацию» синхронного кода в серверном коде [устарело] или неверны.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [How Async/Await Really Works in C#](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/) | Stephen Toub, 2023 | История APM → Task, машина состояний, awaiter, ExecutionContext против SynchronizationContext |
| [AsyncGuidance.md](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md) | David Fowler, живой | Свод правил с примерами «плохо/хорошо»: async void, sync-over-async, TCS, CTS, таймеры |
| [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) | Stephen Toub, 2019 | Когда нужен `ConfigureAwait(false)` и почему |
| [There Is No Thread](https://blog.stephencleary.com/2013/11/there-is-no-thread.html) | Stephen Cleary, 2013 | Почему асинхронный I/O не занимает поток |
| [Concurrency in C# Cookbook, 2nd ed.](https://www.oreilly.com/library/view/concurrency-in-c/9781492054498/) | Stephen Cleary, 2019 | Рецепты: async, Dataflow, Rx, Channels, отмена, тестирование |
| [Threading in C#](https://www.albahari.com/threading/) | Joseph Albahari, ~2011 | Потоки, `Monitor`, сигналы, модель памяти «на пальцах»; частично [устарело] |
| [Memory-model.md](https://github.com/dotnet/runtime/blob/main/docs/design/specs/Memory-model.md) | dotnet/runtime, живой | Формальные гарантии модели памяти CoreCLR |
| [Asynchronous programming](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/) | Microsoft Learn, живой | Официальный обзор async/await |

## Потоки, задачи или async

### Ключевые выводы
- Async — для ожидания ввода-вывода (сеть, диск, БД). Пока операция идёт, поток не занят. Для CPU-работы async ничего не даёт.
- CPU-работа над коллекцией — `Parallel.For/ForEach` или PLINQ. Асинхронный fan-out с ограничением параллелизма — `Parallel.ForEachAsync`.
- `Task.Run` в серверном коде не делает синхронный код асинхронным: он только переносит его на другой поток пула. В UI-приложении `Task.Run` нужен, чтобы увести CPU-работу с UI-потока.
- Долгий блокирующий цикл (опрос устройства, consumer старой библиотеки) — отдельный `Thread` или `TaskCreationOptions.LongRunning`, а не поток пула.

| Задача | Выбор |
|---|---|
| Много одновременных I/O-операций | `async`/`await`, `Task.WhenAll` |
| CPU-параллелизм по коллекции | `Parallel.ForEach`, PLINQ |
| Асинхронная обработка списка с лимитом | `Parallel.ForEachAsync` с `MaxDegreeOfParallelism` |
| Тяжёлое вычисление из UI-обработчика | `await Task.Run(...)` |
| Бесконечный блокирующий цикл | `new Thread` или `LongRunning` + канал |
| Конвейер «производитель/потребитель» | `System.Threading.Channels` |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Task-based Asynchronous Pattern](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap) | Microsoft Learn | Контракт TAP: именование, отмена, прогресс |
| [Managed threading best practices](https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices) | Microsoft Learn | Общие правила для потоков |

## Async/await: правила

### Ключевые выводы
- Async «заразен»: если внизу стека async, все вызывающие тоже должны быть async. Частичная асинхронность хуже полностью синхронного кода.
- `async void` — только для обработчиков событий. Исключение из `async void` уходит в SynchronizationContext или пул потоков и может уронить процесс.
- Не блокировать на задачах: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` вызывают дедлок в UI и голодание пула на сервере. Единственное законное место — `Main` без async (с C# 7.1 есть `async Task Main`).
- Предпочитать `await` прямому `return task`: так сохраняются стек исключения, область `using` и `try`. Возвращать задачу напрямую можно в тривиальных обёртках без `using`/`try`.
- `TaskCompletionSource` создавать с `TaskCreationOptions.RunContinuationsAsynchronously`, иначе продолжения выполнятся синхронно внутри `SetResult` и могут вызвать дедлок или повреждение состояния.
- Асинхронную инициализацию не делать в конструкторе: статическая фабрика `static async Task<T> CreateAsync()`.
- `ContinueWith` заменять на `await`: он не учитывает контекст и путает обработку исключений.
- Результат параллельных задач собирать через `Task.WhenAll`; по мере завершения — через `Task.WhenEach` (.NET 9), а не цикл с `Task.WhenAny`.

**Sync-over-async и async void.** Источник: [AsyncGuidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md).
```csharp
// Плохо: блокирует поток пула, в UI — дедлок
public string GetData() => _client.GetStringAsync(url).Result;

// Плохо: исключение уронит процесс, вызывающий не может дождаться
public async void Save() => await _repo.SaveAsync();

// Хорошо
public Task<string> GetDataAsync(CancellationToken ct) => _client.GetStringAsync(url, ct);
public async Task SaveAsync(CancellationToken ct) => await _repo.SaveAsync(ct);
```

**TaskCompletionSource и таймер.**
```csharp
var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

// Колбэк таймера: не async void, а явный discard с обработкой ошибок внутри
_timer = new Timer(_ => _ = PingSafeAsync(), null, 0, 10_000);
async Task PingSafeAsync()
{
    try { await PingAsync(); }
    catch (Exception ex) { _log.LogError(ex, "ping failed"); }
}
```

**PeriodicTimer** (.NET 6) — альтернатива `Timer` с fire-and-forget для фоновой периодической работы: цикл `while (await timer.WaitForNextTickAsync(ct))` не перекрывает тики (пропущенные склеиваются в один), исключения идут в обычный `try/catch`, остановка — через токен или `Dispose`. Ожидать `WaitForNextTickAsync` может только один потребитель одновременно ([PeriodicTimer](https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer)).

**IAsyncEnumerable.** Для потоковой выдачи (C# 8). Токен отмены передаётся через `[EnumeratorCancellation]`, снаружи — `WithCancellation`. В .NET 10 LINQ для `IAsyncEnumerable` (`System.Linq.AsyncEnumerable`) вошёл в BCL; раньше нужен был пакет `System.Linq.Async`. Старая ссылка на `System.Linq.Async` даёт ошибки неоднозначности: удалите её или обновите до 7.0, а при мультитаргетинге на старые TFM ставьте пакет `System.Linq.AsyncEnumerable` ([breaking change](https://github.com/dotnet/docs/blob/main/docs/core/compatibility/core-libraries/10.0/asyncenumerable.md)).
```csharp
async IAsyncEnumerable<Row> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var row in _db.QueryAsync(ct)) yield return row;
}
await foreach (var r in ReadAsync().WithCancellation(ct)) { /* ... */ }
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Don't Block on Async Code](https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html) | Stephen Cleary, 2012 | Механика дедлока `.Result` с SynchronizationContext |
| [Async streams tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream) | Microsoft Learn | `IAsyncEnumerable`, `await foreach`, отмена |
| [Task.WhenEach](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.wheneach) | Microsoft Learn, .NET 9 | Обработка задач по мере завершения |
| [ASP.NET Core best practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) | Microsoft Learn | Sync-over-async, `HttpContext` вне запроса |
| [AsyncEx](https://github.com/StephenCleary/AsyncEx) | Stephen Cleary | `AsyncLock`, `AsyncLazy`, `AsyncContext` для консольных тестов |
| [Blog](https://blog.stephencleary.com/) | Stephen Cleary | Серии «Async and Scheduled Concurrency», «Cancellation» |

## Task и ValueTask

### Ключевые выводы
- По умолчанию возвращать `Task`/`Task<T>`. `ValueTask` — когда синхронный путь горячий (кэш, буфер) и выигрыш измерен, либо для реализации `IValueTaskSource` с пулингом.
- `ValueTask` можно ожидать ровно один раз. Нельзя ожидать дважды, ожидать конкурентно, вызывать `.Result` до завершения. Если нужно что-то из этого — `.AsTask()`.
- Не хранить `ValueTask` в полях и коллекциях; не передавать в `Task.WhenAll` без `.AsTask()`.
- Анализатор CA2012 ловит большую часть ошибок использования `ValueTask`.
- `Task.FromResult`/`Task.CompletedTask` для готовых значений, а не `Task.Run(() => value)`.

```csharp
// Неверно: ValueTask ожидается дважды — неопределённое поведение при пулинге
ValueTask<int> vt = stream.ReadAsync(buf, ct);
int a = await vt;
int b = await vt;

// Верно
int n = await stream.ReadAsync(buf, ct);
Task<int> t = stream.ReadAsync(buf, ct).AsTask(); // если нужно хранить/комбинировать
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Understanding the Whys, Whats, and Whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/) | Stephen Toub, 2018 | Канонические правила `ValueTask` |
| [CA2012: Use ValueTasks correctly](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2012) | Microsoft Learn | Анализатор ошибок |
| [Be careful when mixing ValueTask and Task.Run](https://www.meziantou.net/be-careful-when-mixing-valuetask-and-task-run.htm) | Gérald Barré | Неочевидная перегрузка `Task.Run` |
| [Understanding how to use Task and ValueTask](https://learn.microsoft.com/en-us/shows/on-net/understanding-how-to-use-task-and-valuetask) | On .NET (видео) | Тот же материал в видео |

## Отмена, таймауты, TimeProvider

### Ключевые выводы
- Каждый публичный async-метод с I/O принимает `CancellationToken` последним параметром и передаёт его во все вызовы, которые его принимают.
- Отмена кооперативная: токен ничего не прерывает сам, код должен проверять `ThrowIfCancellationRequested` или передавать токен дальше.
- `CancellationTokenSource` с `CancelAfter` или из `CreateLinkedTokenSource` освобождать через `using`: иначе остаются таймеры и регистрации.
- Таймаут поверх задачи без поддержки отмены — `task.WaitAsync(timeout, ct)` (.NET 6). Сама операция при этом продолжает работать.
- `CancelAsync()` (.NET 8) не выполняет колбэки регистраций синхронно в вызывающем потоке. `TryReset()` (.NET 6) позволяет переиспользовать CTS, если отмены не было.
- `OperationCanceledException` при отменённом токене — нормальный путь, не ошибка: не логировать как сбой и не глотать молча.
- Время брать из `TimeProvider` (.NET 8), а не `DateTime.UtcNow`/`Task.Delay` напрямую: в тестах подставляется `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).

```csharp
public async Task<Order> GetAsync(int id, CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(5));             // таймаут операции
    return await _http.GetFromJsonAsync<Order>($"/orders/{id}", cts.Token)
        ?? throw new InvalidOperationException("пустой ответ");
}

// Тестируемая задержка
await Task.Delay(TimeSpan.FromMinutes(1), _timeProvider, ct);
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Cancellation in managed threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) | Microsoft Learn | Модель кооперативной отмены, linked tokens |
| [TimeProvider overview](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview) | Microsoft Learn, .NET 8 | Абстракция времени и `FakeTimeProvider` |

## SynchronizationContext и ConfigureAwait

### Ключевые выводы
- `await` по умолчанию захватывает `SynchronizationContext` (UI-поток WPF/WinForms/MAUI, классический ASP.NET) или текущий `TaskScheduler` и возвращается в него.
- ASP.NET Core и консольные приложения не имеют `SynchronizationContext`: дедлок `.Result` там не возникает, но голодание пула — возникает.
- Правило Toub: в коде приложения `ConfigureAwait(false)` не нужен; в библиотеке общего назначения — нужен на каждом `await`, потому что библиотека не знает, где её вызовут.
- `ConfigureAwait(false)` не защищает от дедлока, если первая же задача завершилась синхронно, а дальше код блокирует. Лечение — не блокировать.
- `ConfigureAwaitOptions` (.NET 8): `SuppressThrowing` — дождаться без исключения (только `Task` без результата), `ForceYielding` — гарантированно уйти с текущего потока.
- В UI после `ConfigureAwait(false)` нельзя трогать элементы управления. Обновлять UI — через `IProgress<T>` (`Progress<T>` захватывает контекст при создании).
- `ConfigureAwait` не влияет на `ExecutionContext`: `AsyncLocal<T>` (корреляционный id, `Activity.Current`, культура) течёт через `await` всегда. Значение, присвоенное внутри async-метода, видно ниже по цепочке, но не вызывающему после возврата. Не храните в `AsyncLocal` изменяемые объекты, общие для параллельных веток, и не используйте его как скрытый канал передачи зависимостей ([AsyncLocal<T>](https://learn.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1)).

```csharp
// Библиотека
public async Task<byte[]> LoadAsync(string path, CancellationToken ct)
{
    var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
    return Transform(bytes);
}

// Дождаться фоновой задачи при остановке, не выбрасывая её исключение
await _worker.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) | Stephen Toub, 2019 | Полный разбор контекстов |
| [ConfigureAwaitOptions](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.configureawaitoptions) | Microsoft Learn, .NET 8 | Флаги `SuppressThrowing`, `ForceYielding` |
| [vs-threading](https://github.com/microsoft/vs-threading) | Microsoft | `JoinableTaskFactory` и анализаторы VSTHRD для UI-приложений и плагинов VS |

## Голодание пула потоков

### Ключевые выводы
- Симптом: сервис отвечает всё медленнее, CPU низкий, число потоков пула растёт. При голодании пул добавляет потоки быстро до 2–3× числа ядер, а дальше по 1–2 в секунду.
- Причина почти всегда — блокировка потоков пула: `.Result`, `.Wait()`, синхронный I/O, `lock` вокруг долгих операций, `Thread.Sleep`.
- Диагностика: `dotnet-counters monitor` (счётчики `dotnet.thread_pool.thread.count`, `dotnet.thread_pool.queue.length`), затем `dotnet-stack report` или дамп (`dotnet-dump`, `clrstack -all`) — ищут множество потоков в `Task.Wait`/`Monitor.Enter`.
- Поднимать `ThreadPool.SetMinThreads` — временная мера, а не лечение.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Debug ThreadPool starvation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-threadpool-starvation) | Microsoft Learn, .NET 9+ | Пошаговый туториал с dotnet-counters/stack/trace |
| [Investigating thread starvation issues](https://microsoft.github.io/vs-threading/docs/threadpool_starvation.html) | Microsoft vs-threading | Как распознать и устранить |
| [.NET ThreadPool starvation, and how queuing makes it worse](https://minidump.net/net-threadpool-starvation-and-how-queuing-makes-it-worse-512c8d570527/) | Kevin Gosse, 2018 (Criteo Labs, перенесено в блог автора) | Расследование с продакшена: почему всплеск запросов с sync-over-async «топит» пул и как очереди это усугубляют |
| [AsyncFixer](https://github.com/semihokur/AsyncFixer) | Semih Okur | Анализатор: блокировки в async, `async void`, лишние async |

## Каналы, Dataflow, Parallel, PLINQ

### Ключевые выводы
- `System.Threading.Channels` — основной примитив «производитель/потребитель» в async-коде. Каналы делать ограниченными (`CreateBounded`): ёмкость даёт backpressure.
- `BoundedChannelFullMode`: `Wait` (по умолчанию, производитель ждёт), `DropOldest`/`DropNewest`/`DropWrite` — для телеметрии, где потеря допустима.
- Подсказки `SingleReader`/`SingleWriter` включают более быстрые реализации; ставить только если это правда.
- Завершение: `writer.Complete()`, потребитель читает `await foreach (var x in reader.ReadAllAsync(ct))`.
- TPL Dataflow (`System.Threading.Tasks.Dataflow`) — для графов с ветвлением, батчингом и `BoundedCapacity` на каждом блоке. Для линейной цепочки достаточно каналов.
- `Parallel.ForEachAsync` (.NET 6) — асинхронный fan-out с лимитом; по умолчанию степень параллелизма = число ядер, для I/O задавать явно.
- PLINQ (`AsParallel`) — для CPU-тяжёлых запросов над большими коллекциями; на мелких операциях медленнее последовательного LINQ. Измерять.

```csharp
var ch = Channel.CreateBounded<Job>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait, SingleReader = true
});

var consumer = Task.Run(async () =>
{
    await foreach (var job in ch.Reader.ReadAllAsync(ct)) await HandleAsync(job, ct);
});

foreach (var job in jobs) await ch.Writer.WriteAsync(job, ct); // ждёт при заполнении
ch.Writer.Complete();
await consumer;

await Parallel.ForEachAsync(urls, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
    async (url, token) => await FetchAsync(url, token));
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [An Introduction to System.Threading.Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) | Stephen Toub, 2019 | Устройство и паттерны каналов |
| [Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) | Microsoft Learn | Справка по API и режимам |
| [Working with Channels in .NET](https://learn.microsoft.com/en-us/shows/on-dotnet/working-with-channels-in-net) | On .NET (видео) | Демонстрация |
| [Dataflow (TPL)](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library) | Microsoft Learn | Блоки, связи, `BoundedCapacity` |
| [Parallel.ForEachAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.parallel.foreachasync) | Microsoft Learn, .NET 6 | Асинхронный параллельный цикл |
| [Introduction to PLINQ](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/introduction-to-plinq) | Microsoft Learn | Когда PLINQ ускоряет, а когда нет |

## Блокировки

### Ключевые выводы
- По умолчанию — `lock` на приватном выделенном объекте. С .NET 9 и C# 13 этим объектом должен быть `System.Threading.Lock`: компилятор превращает `lock` в `using (x.EnterScope())`, это быстрее `Monitor`.
- Не приводить `Lock` к `object` и не передавать как `object`: тогда `lock` откатится на `Monitor` (компилятор предупреждает).
- Не блокировать на `this`, `typeof(T)`, строках и публичных объектах.
- Внутри `lock` нельзя `await` (ошибка компиляции). Для взаимного исключения через `await` — `SemaphoreSlim(1, 1)` с `WaitAsync` и `Release` в `finally`.
- `SemaphoreSlim` не реентерабелен и не привязан к потоку: повторный вход из той же логической операции — дедлок.
- `ReaderWriterLockSlim` — только при явном преобладании чтений с долгими читающими секциями; часто проще неизменяемый снимок с атомарной заменой ссылки.
- `SpinLock` — структура, не копировать (передавать по ссылке), только для очень коротких секций после замеров. `Mutex`/`Semaphore` (ядро ОС) — только для межпроцессной синхронизации.
- Порядок захвата нескольких блокировок фиксировать, иначе дедлок.

```csharp
private readonly Lock _gate = new();          // .NET 9+
private int _count;
public void Increment() { lock (_gate) _count++; }

private readonly SemaphoreSlim _mutex = new(1, 1);
public async Task UpdateAsync(CancellationToken ct)
{
    await _mutex.WaitAsync(ct);
    try { await _store.WriteAsync(ct); }
    finally { _mutex.Release(); }
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [The lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock) | Microsoft Learn | Правила `lock` и `System.Threading.Lock` |
| [How to use the new Lock object in C# 13](https://www.infoworld.com/article/3632180/how-to-use-the-new-lock-object-in-c-13.html) | Joydip Kanjilal, 2024 | API `Lock`: `Enter`, `TryEnter`, `EnterScope` |
| [Overview of synchronization primitives](https://learn.microsoft.com/en-us/dotnet/standard/threading/overview-of-synchronization-primitives) | Microsoft Learn | Сравнение `Monitor`, `SemaphoreSlim`, `ReaderWriterLockSlim`, событий |

## Interlocked, volatile и модель памяти

### Ключевые выводы
- Модель памяти CoreCLR сильнее ECMA-335: публикация ссылки на объект работает как release — все записи, достижимые через ссылку, видны прочитавшему потоку. Зависимые по данным чтения упорядочены.
- Volatile-чтение — acquire, volatile-запись — release. `Interlocked.*` и вход/выход из `lock` — полные барьеры.
- Атомарны выровненные чтения и записи примитивов размером не больше указателя. `long`/`double` на 32-битной платформе — не атомарны: `Interlocked.Read`/`Exchange`.
- Счётчики и флаги — `Interlocked.Increment`, `Interlocked.CompareExchange`, `Volatile.Read/Write`. Ключевое слово `volatile` на поле допустимо, но явные `Volatile.Read/Write` нагляднее.
- Ленивую инициализацию не писать вручную через double-checked locking: `Lazy<T>` или `LazyInitializer.EnsureInitialized`.
- Lock-free структуры писать только после профилирования, показавшего конкуренцию за lock; иначе готовые `Concurrent*`.

```csharp
private int _state; // 0 = idle, 1 = running
public bool TryStart() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

private Config _config = Config.Default;                 // неизменяемый снимок
public Config Current => Volatile.Read(ref _config);
public void Reload(Config next) => Volatile.Write(ref _config, next);
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [.NET memory model](https://github.com/dotnet/runtime/blob/main/docs/design/specs/Memory-model.md) | dotnet/runtime | Формальные гарантии, в том числе на ARM64 |
| [Interlocked](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked) | Microsoft Learn | Атомарные операции |
| [Volatile](https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile) | Microsoft Learn | Acquire/release-доступ |

## Конкурентные и неизменяемые коллекции

### Ключевые выводы
- `ConcurrentDictionary`: составные операции делать одним вызовом (`GetOrAdd`, `AddOrUpdate`). Фабрика в `GetOrAdd` может выполниться несколько раз — для дорогих значений хранить `Lazy<T>` или `Task<T>`.
- `ConcurrentQueue`/`ConcurrentBag` — без ограничения ёмкости; для очередей с backpressure брать каналы. `BlockingCollection` в async-коде [устарело] — заменять каналом.
- Неизменяемые коллекции (`ImmutableArray`, `ImmutableDictionary`) — для снимков, которые читают много потоков, а меняют редко. `FrozenDictionary`/`FrozenSet` (.NET 8) — для данных, собранных один раз при старте.
- Обновление неизменяемой коллекции из нескольких потоков — `ImmutableInterlocked.Update`.

```csharp
private readonly ConcurrentDictionary<string, Lazy<Task<User>>> _cache = new();
public Task<User> GetUserAsync(string id) =>
    _cache.GetOrAdd(id, k => new Lazy<Task<User>>(() => LoadUserAsync(k))).Value;
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Thread-safe collections](https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/) | Microsoft Learn | Обзор `Concurrent*` |
| [System.Collections.Immutable](https://learn.microsoft.com/en-us/dotnet/api/system.collections.immutable) | Microsoft Learn | Неизменяемые коллекции и `ImmutableInterlocked` |

## Акторы

### Ключевые выводы
- Внутри процесса актор строится на канале: одна задача владеет состоянием и читает сообщения, ответы идут через `TaskCompletionSource`. Это заменяет `lock` вокруг большого состояния.
- Orleans — «виртуальные акторы» (grains): рантайм сам активирует, размещает по кластеру и деактивирует. Выбор для распределённого состояния в стеке Microsoft; актуальная линия — 10.x.
- Akka.NET — порт Akka: иерархии супервизоров, кластер, persistence, streams. Стабильная линия 1.5.x (1.5.73 от 5 октября 2026), 1.6 в бете с поддержкой Native AOT.
- Proto.Actor — лёгкий кросс-платформенный фреймворк на gRPC, Apache 2.0.
- Фреймворк акторов брать, когда нужны распределение, supervision или persistence. Для одного процесса хватит канала.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Orleans documentation](https://learn.microsoft.com/en-us/dotnet/orleans/) | Microsoft, живой | Grains, silos, persistence, streams |
| [dotnet/orleans](https://github.com/dotnet/orleans) | Microsoft | Исходники и [релизы](https://github.com/dotnet/orleans/releases) (последний — v10.4.0 от 3 октября 2026) |
| [Akka.NET releases](https://github.com/akkadotnet/akka.net/releases) | Petabridge и сообщество, 2026 | Изменения 1.5.x и 1.6 beta |
| [Proto.Actor .NET](https://github.com/asynkron/protoactor-dotnet) | Asynkron | Лёгкие распределённые акторы |

## Runtime async (.NET 10–11)

### Ключевые выводы
- Runtime async переносит машину состояний async из компилятора C# в рантайм. Семантика `async`/`await` для кода приложения не меняется; правила этого файла остаются в силе.
- .NET 10 — preview за флагом. .NET 11: поддержка в CoreCLR включена по умолчанию, Native AOT компилирует такой код, с Preview 4 библиотеки самого .NET собраны с `runtime-async=on`. Mono не поддерживается.
- Для своего проекта в .NET 11 (на RC1) это preview с opt-in: `<Features>runtime-async=on</Features>`, на `net11.0` без `EnablePreviewFeatures`. Отключить для проекта — `<UseRuntimeAsync>false</UseRuntimeAsync>`; переменные окружения `DOTNET_RuntimeAsync`/`UNSUPPORTED_RuntimeAsync` удалены ([What's new in .NET 11 runtime](https://github.com/dotnet/docs/blob/main/docs/core/whats-new/dotnet-11/runtime.md)). Изменится ли умолчание к GA, не объявлено.
- Ожидаемый эффект — выше пропускная способность глубоких async-цепочек и меньше размер сборок. Код, который опирается на внутренности машины состояний (рефлексия по `<>d__` типам, ручные builders), проверить.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [dotnet/runtime#109632](https://github.com/dotnet/runtime/issues/109632) | dotnet/runtime, 2024–2026 | Трекинг runtime async и оставшиеся работы |
| [.NET 11 Preview 4: runtime](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview4/runtime.md) | Microsoft, 2026 | BCL на runtime async, свойство `UseRuntimeAsync` |
| [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/) | Stephen Toub, 2025 | Изменения threading/async в .NET 10 |

## Unsafe: практика

### Ключевые выводы
- Сначала безопасные инструменты: `Span<T>`, `Memory<T>`, `stackalloc` в `Span<T>`, `[InlineArray]` (C# 12) вместо `fixed`-буферов. Они дают ту же производительность без указателей.
- `unsafe` держать в маленьком внутреннем типе с безопасным API, проверяющим границы. В комментарии у каждого `unsafe` блока писать, почему операция корректна.
- `fixed` пинит объект только на время блока. Указатель, сохранённый после блока, указывает в перемещаемую память. Для долгого пиннинга — `GCHandle.Alloc(obj, Pinned)` с `Free` в `finally` или `GC.AllocateArray<T>(n, pinned: true)` (.NET 5, POH).
- `Unsafe.As`, `Unsafe.Add`, `MemoryMarshal.GetReference`, `MemoryMarshal.Cast`, `CollectionsMarshal.AsSpan` не требуют ключевого слова `unsafe`, но так же опасны: обходят проверки типов и границ. Ревьюить их как `unsafe`.
- `stackalloc` — только с маленьким фиксированным пределом (обычно до ~1 КБ), иначе `ArrayPool<T>`. С `[SkipLocalsInit]` память не обнулена.
- `ref struct` (`Span<T>`) не попадает в кучу. C# 11: `ref`-поля и `scoped`. C# 13: `ref struct` реализует интерфейсы, параметр-ограничение `allows ref struct`, `ref`-локали в async и итераторах (но не через `await`/`yield`).
- Указатель или `Span` на stackalloc-память не должны пережить `await`.

```csharp
// Без unsafe: stackalloc с пределом и пулом в запасном пути
const int MaxStack = 256;
byte[]? rented = null;
Span<byte> buf = len <= MaxStack ? stackalloc byte[MaxStack] : (rented = ArrayPool<byte>.Shared.Rent(len));
try { Fill(buf[..len]); }
finally { if (rented is not null) ArrayPool<byte>.Shared.Return(rented); }

// Неверно: указатель пережил fixed
unsafe byte* Leak(byte[] a) { fixed (byte* p = a) return p; } // GC может сдвинуть массив
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Unsafe code, pointer types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/unsafe-code) | Microsoft Learn | Указатели, `fixed`, `sizeof`, fixed-буферы |
| [fixed statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/fixed) | Microsoft Learn | Пиннинг на время блока |
| [stackalloc](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc) | Microsoft Learn | Безопасный stackalloc в `Span` и предел размера |
| [Unsafe class](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafe) | Microsoft Learn | Низкоуровневые операции без проверок |
| [MemoryMarshal](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.memorymarshal) | Microsoft Learn | Переинтерпретация `Span`, `GetReference`, `CreateSpan` |
| [ref struct types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct) | Microsoft Learn | Ограничения `ref struct` и изменения C# 13 |
| [Low-level struct improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md) | C# 11 spec | `ref`-поля, `scoped`, правила escape |

## Эволюция unsafe (preview в .NET 11)

### Ключевые выводы
- Смысл `unsafe` меняется: небезопасно не существование указателя, а разыменование памяти, которой не управляет рантайм. Модель по признанию самих авторов ближе всего к Rust.
- Создание указателя, `&x`, `stackalloc` в указатель, объявление fixed-буфера станут безопасными. Небезопасны: разыменование, `->`, индексирование указателя, вызов function pointer, доступ к fixed-буферу. Ослабления действуют без opt-in.
- `unsafe` на методе означает «requires-unsafe»: вызывающий должен быть в `unsafe`-контексте. Тело метода при этом не становится `unsafe`-контекстом — внутри нужны явные `unsafe { }` блоки (как `unsafe_op_in_unsafe_fn` в Rust).
- Новое контекстное слово `safe`: `extern`-методы и поля в `LayoutKind.Explicit` обязаны явно помечаться `safe` или `unsafe`. Форма выражения `unsafe(expr)`.
- BCL получит разметку: `Unsafe`, `Marshal`, часть рефлексии станут requires-unsafe. В метаданных — `RequiresUnsafeAttribute` и `MemorySafetyRulesAttribute`.
- `stackalloc` в `Span` без инициализатора под `[SkipLocalsInit]` станет небезопасным (только при opt-in). `await` в `unsafe`-контексте разрешён (RC1), внутри `fixed` — нет. `unsafe` на делегатах, статических конструкторах, деструкторах и объявлениях типов в RC1 стал ошибкой: он больше не создаёт осмысленного unsafe-контекста.
- Сроки: в .NET 11 — preview, выключено по умолчанию, включается `<LangVersion>preview</LangVersion>` и `<Features>$(Features);updated-memory-safety-rules</Features>` (в file-based apps — `#:property Features=...`). Эта возможность не входит в C# 15 и после GA .NET 11 останется preview. Свойство `<MemorySafetyRules>2</MemorySafetyRules>` по дизайн-документу SDK появится в .NET 12 или 13 как opt-in, позже планируется включение по умолчанию ([RC1: C#](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/rc1/csharp.md), [SDK memory safety enforcement](https://github.com/dotnet/designs/blob/main/accepted/2025/memory-safety/sdk-memory-safety-enforcement.md)).
- Что делать уже сейчас: держать `unsafe` узким и локальным, документировать контракт методов с указателями, не прятать опасные операции (`Unsafe.As`, `Marshal.*`) за «безопасным» API без проверок.

```csharp
// По новым правилам (preview)
void M()
{
    int i = 1;
    int* p = &i;               // разрешено: создание указателя безопасно
    unsafe { Console.WriteLine(*p); } // разыменование — только в unsafe-блоке
}
unsafe static int ReadAt(int* p, int i) { unsafe { return p[i]; } } // вызывающий обязан быть в unsafe
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Memory safety (unsafe evolution)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/unsafe-evolution) | C# LDT, обновлено 2026 | Спецификация preview |
| [csharplang#9704](https://github.com/dotnet/csharplang/issues/9704) | dotnet/csharplang | Champion issue |
| [caller-unsafe design](https://github.com/dotnet/designs/blob/main/accepted/2025/memory-safety/caller-unsafe.md) | dotnet/designs, 2025 | Мотивация и изменения в BCL |
| [SDK memory safety enforcement](https://github.com/dotnet/designs/blob/main/accepted/2025/memory-safety/sdk-memory-safety-enforcement.md) | dotnet/designs | Свойство `MemorySafetyRules`, план .NET 11/12 |
| [LDM 2026-04-29](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-04-29.md) | C# LDM, 2026 | Решения о постепенном opt-in |
| [Microsoft Extends Rust-Influenced Memory Safety Push to C#](https://visualstudiomagazine.com/articles/2026/05/27/microsoft-extends-rust-influenced-memory-safety-push-to-csharp.aspx) | Visual Studio Magazine, 2026 | Обзор и сроки |
| [Microsoft wants safer C# without turning it into Rust](https://www.theregister.com/devops/2026/05/26/microsoft-wants-safer-c-without-turning-it-into-rust/5246359) | The Register, 2026 | Взгляд со стороны |

## Нативный interop

### Ключевые выводы
- P/Invoke на .NET 7+ — `[LibraryImport]` (source generator) вместо `[DllImport]`: маршалинг генерируется при компиляции, совместим с trimming и Native AOT. Анализатор SYSLIB1054 подсказывает миграцию.
- Типы на границе — blittable: примитивы, указатели, структуры с `Sequential`/`Explicit` из blittable-полей. `bool` не blittable: .NET `bool` по умолчанию маршалится как 4-байтовый Win32 `BOOL`, а C `bool` — 1 байт.
- C `long` ≠ C# `long` (на Windows 32 бита, на Linux x64 — 64): использовать `CLong`/`CULong` (.NET 6).
- Нативные ресурсы — `SafeHandle`-наследники, а не `IntPtr` с финализатором.
- Колбэки — `delegate* unmanaged[Cdecl]<...>` + `[UnmanagedCallersOnly]` вместо делегатов. Если делегат всё же передан, держать его живым (`GC.KeepAlive` или статическое поле) всё время, пока натив может его вызвать.
- Исключение не должно выйти из `[UnmanagedCallersOnly]` метода: процесс завершится. Ловить всё и возвращать код ошибки.
- Освобождать память тем же аллокатором, которым её выделили: `free` из той же библиотеки, `Marshal.FreeHGlobal` для `AllocHGlobal`, `CoTaskMemFree` для COM.
- `SetLastError = true` и `Marshal.GetLastPInvokeError()` сразу после вызова.
- COM на .NET 8+ — `[GeneratedComInterface]`/`[GeneratedComClass]` (ComWrappers source generator), совместимо с AOT. Встроенный COM interop генерирует IL-заглушки во время выполнения и несовместим с AOT.
- Экспорт C ABI из C#: Native AOT библиотека (`PublishAot`, `NativeLib=Shared|Static`) с `[UnmanagedCallersOnly(EntryPoint = "name")]`. Без AOT — DNNE.
- Генераторы привязок: CsWin32 (Win32 API), ClangSharp (C-заголовки), csbindgen (Rust `extern "C"` → C#).
- C++/CLI на .NET работает только на Windows, только как DLL, только в `.vcxproj`. Для нового кода — C ABI + P/Invoke.
- WebAssembly (браузер): `[JSImport]`/`[JSExport]` из `System.Runtime.InteropServices.JavaScript` (.NET 7+).

```csharp
internal static partial class Native
{
    [LibraryImport("mylib", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial MyHandle my_open(string path);

    [LibraryImport("mylib")]
    [return: MarshalAs(UnmanagedType.U1)]          // C bool — 1 байт
    internal static partial bool my_close(IntPtr h);
}

internal sealed class MyHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public MyHandle() : base(ownsHandle: true) { }
    protected override bool ReleaseHandle() => Native.my_close(handle);
}

// Экспорт для C/Rust при публикации Native AOT библиотеки
public static unsafe class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "add_numbers")]
    public static int Add(int a, int b)
    {
        try { return checked(a + b); }
        catch { return int.MinValue; } // исключение не должно пересечь границу
    }
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Native interoperability best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) | Jeremy Koritzinsky (Microsoft), обновлено 2026 | Главный документ: типы, строки, `bool`, `SafeHandle`, таблица неисправностей |
| [P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation) | Microsoft Learn, .NET 7 | `LibraryImport` и отличия от `DllImport` |
| [Custom marshaller tutorial](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/tutorial-custom-marshaller) | Microsoft Learn | `[CustomMarshaller]` для своих типов |
| [Disabled runtime marshalling](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/disabled-marshalling) | Microsoft Learn | `DisableRuntimeMarshalling`: только `unmanaged`-типы |
| [SafeHandle](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle) | Microsoft Learn | Владение нативным хэндлом |
| [Cleaning up unmanaged resources](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/unmanaged) | Microsoft Learn | Dispose-паттерн и `SafeHandle` |
| [Function pointers (C# 9)](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/function-pointers.md) | C# spec | `delegate* unmanaged[...]` |
| [UnmanagedCallersOnlyAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute) | Microsoft Learn | Методы, вызываемые из натива; `EntryPoint` |
| [ComWrappers source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/comwrappers-source-generation) | Microsoft Learn, .NET 8 | COM без IL-заглушек времени выполнения |
| [SYSLIB1090–1099](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib1090-1099) | Microsoft Learn | Диагностики COM-генератора |
| [Native AOT interop](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop) | Microsoft Learn | Прямые P/Invoke, статическая линковка |
| [Native AOT libraries](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries) | Microsoft Learn | Экспорт C ABI из C# |
| [DNNE](https://github.com/AaronRobinsonMSFT/DNNE) | Aaron Robinson | Нативные экспорты без AOT |
| [Migrate C++/CLI projects to .NET](https://learn.microsoft.com/en-us/dotnet/core/porting/cpp-cli) | Microsoft Learn, 2024 | Ограничения C++/CLI на .NET |
| [CsWin32](https://github.com/microsoft/CsWin32) | Microsoft | Генерация Win32 P/Invoke по списку имён |
| [ClangSharp](https://github.com/dotnet/ClangSharp) | dotnet | Генерация привязок из C/C++ заголовков |
| [csbindgen](https://github.com/Cysharp/csbindgen) | Cysharp (Yoshifumi Kawai), 2023 | Rust → C# привязки; [статья автора](https://neuecc.medium.com/csbindgen-generate-c-native-code-bridge-automatically-or-modern-approaches-to-native-code-78d9f9a616fb) |
| [SourceGear.Bridge.NativeAOT.Rust](https://www.nuget.org/packages/SourceGear.Bridge.NativeAOT.Rust) | SourceGear (Eric Sink) | Обратное направление: Rust вызывает .NET AOT; [контекст](https://ericsink.com/native_aot/looking_ahead_questpdf_rust.html) |
| [JSImport/JSExport interop](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop) | Microsoft Learn | .NET в WebAssembly ↔ JavaScript; [вариант для Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/import-export-interop) |

## Что выбрать

| Задача | Примитив |
|---|---|
| Ожидание I/O | `async`/`await` + `CancellationToken` |
| Несколько независимых I/O-операций | `Task.WhenAll`; по мере готовности — `Task.WhenEach` |
| Асинхронный fan-out с лимитом | `Parallel.ForEachAsync` или `SemaphoreSlim` + `WhenAll` |
| CPU-параллелизм по коллекции | `Parallel.For/ForEach`, PLINQ |
| Производитель/потребитель | `Channel.CreateBounded` |
| Граф обработки с батчингом | TPL Dataflow |
| Короткая критическая секция без `await` | `lock` на `System.Threading.Lock` |
| Взаимное исключение через `await` | `SemaphoreSlim(1, 1)` |
| Счётчик, флаг, CAS | `Interlocked`, `Volatile` |
| Конфигурация: часто читают, редко меняют | неизменяемый объект + `Volatile.Write`/`Interlocked.Exchange` |
| Ленивая инициализация | `Lazy<T>`, `LazyInitializer`, `AsyncLazy` |
| Конкурентный словарь-кэш | `ConcurrentDictionary` с `Lazy<Task<T>>` |
| Межпроцессная блокировка | именованный `Mutex`/`Semaphore` |
| Ответ на запрос из колбэка | `TaskCompletionSource` с `RunContinuationsAsynchronously` |
| Таймаут | linked `CancellationTokenSource` + `CancelAfter`, или `WaitAsync` |
| Время в тестируемом коде | `TimeProvider` / `FakeTimeProvider` |
| Состояние, к которому обращается много задач | актор на канале; распределённо — Orleans/Akka.NET |
| Временный буфер | `stackalloc` в `Span` (маленький) или `ArrayPool<T>` |
| Вызов C-функции | `[LibraryImport]` + `SafeHandle` |
| Колбэк из натива | `delegate* unmanaged` + `[UnmanagedCallersOnly]` |
| Библиотека для C/Rust/Python | Native AOT shared library |
| Win32 API | CsWin32 |
| COM | `[GeneratedComInterface]` |

## Чеклист review: конкурентный и unsafe-код

1. Модель выбрана осознанно: async для I/O, `Parallel`/PLINQ для CPU, отдельный поток для бесконечных блокирующих циклов.
2. Нет `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` на незавершённых задачах вне точки входа.
3. Нет `async void`, кроме обработчиков событий; в обработчиках есть `try/catch`.
4. Нет задач «выстрелил и забыл»: каждая задача ожидается, сохраняется или явно отброшена (`_ =`) с обработкой исключений внутри.
5. Каждый публичный async-метод с I/O принимает `CancellationToken` и передаёт его дальше.
6. `CancellationTokenSource` (особенно с `CancelAfter` и linked) освобождается через `using`.
7. `OperationCanceledException` не логируется как ошибка и не глотается молча.
8. У внешних вызовов (HTTP, БД, очереди) есть таймауты.
9. `TaskCompletionSource` создан с `RunContinuationsAsynchronously`.
10. `ValueTask` ожидается ровно один раз, не хранится в полях; CA2012 не подавлен.
11. Библиотечный код использует `ConfigureAwait(false)`; UI-код не трогает элементы управления после него.
12. Нет `ContinueWith` там, где подходит `await`.
13. Внутри `lock` нет долгих операций, I/O и вызовов чужого кода (колбэков, событий).
14. Новые блокировки — `System.Threading.Lock` (на .NET 9+), объект приватный и не приводится к `object`.
15. Нет блокировок на `this`, `typeof`, строках, публичных объектах.
16. Порядок захвата нескольких блокировок одинаков везде.
17. `SemaphoreSlim.Release` в `finally`; нет повторного входа в тот же семафор.
18. Все очереди и каналы ограничены; число параллельных операций ограничено.
19. Составные операции над `ConcurrentDictionary` атомарны (`GetOrAdd`/`AddOrUpdate`); дорогие фабрики обёрнуты в `Lazy`.
20. Нет самописного double-checked locking; используется `Lazy<T>`/`LazyInitializer`.
21. Разделяемые поля читаются/пишутся через `Interlocked`/`Volatile` или под lock; `long` на 32-битных платформах — через `Interlocked`.
22. Код, чувствительный ко времени, использует `TimeProvider` и протестирован с `FakeTimeProvider`.
23. Нет признаков голодания пула под нагрузкой (проверено `dotnet-counters`); `SetMinThreads` не используется как лечение.
24. `unsafe` изолирован в маленьком внутреннем типе с безопасным API и проверками границ; у блоков есть комментарии об инвариантах.
25. `Unsafe.*`, `MemoryMarshal.*`, `CollectionsMarshal.*` ревьюятся так же строго, как `unsafe`.
26. Указатели из `fixed` не переживают блок; долгий пиннинг — `GCHandle` с `Free` в `finally` или POH.
27. `stackalloc` ограничен константой; большой буфер берётся из `ArrayPool` и возвращается в `finally`.
28. Указатели и stackalloc-буферы не живут через `await`.
29. P/Invoke на .NET 7+ — `[LibraryImport]`; предупреждения SYSLIB1054 исправлены.
30. Сигнатуры сверены с заголовками: размеры типов, `bool` (1 или 4 байта), C `long` → `CLong`, строки с явной кодировкой.
31. Нативные ресурсы обёрнуты в `SafeHandle`; память освобождается тем же аллокатором.
32. Делегаты, переданные в натив, удерживаются от сборки всё время использования; предпочтительно `delegate* unmanaged` + `[UnmanagedCallersOnly]`.
33. Исключения не пересекают границу `[UnmanagedCallersOnly]`.
34. COM в коде под AOT/trimming — через `[GeneratedComInterface]`.
35. Для кода, который переживёт .NET 12, учтены новые правила `unsafe`: явные `unsafe { }` внутри `unsafe`-методов, `safe`/`unsafe` на `extern`.

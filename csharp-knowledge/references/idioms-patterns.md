# Идиомы и паттерны C#

Справочник по идиоматичному C# для любых проектов (библиотеки, веб-бэкенды, сервисы, CLI, десктоп): новые возможности языка, каталог паттернов и антипаттернов с условиями применения и короткими эскизами кода, спор «исключения или Result», DI и Options, чеклист для code review. Общая идея: инварианты живут в типах (records, value objects, закрытые иерархии), а зависимости — в конструкторе. Актуально на октябрь 2026 года: стабильный C# 14 / .NET 10 (LTS), C# 15 / .NET 11 в RC1 с go-live-лицензией (GA 10 ноября 2026).

- [Канонические источники](#канонические-источники)
- [Версии языка: C# 12–15](#версии-языка-c-1215)
- [Типы как носители инвариантов](#типы-как-носители-инвариантов)
- [Pattern matching и nullable](#pattern-matching-и-nullable)
- [Ошибки: исключения и Result](#ошибки-исключения-и-result)
- [Generics, интерфейсы и расширения](#generics-интерфейсы-и-расширения)
- [Ресурсы, async и синхронизация](#ресурсы-async-и-синхронизация)
- [DI и Options](#di-и-options)
- [Source generators как идиома](#source-generators-как-идиома)
- [Мини-каталог паттернов](#мини-каталог-паттернов)
- [Лицензии библиотек: изменения 2025–2026](#лицензии-библиотек-изменения-20252026)
- [Антипаттерны](#антипаттерны)
- [Именование и анализаторы](#именование-и-анализаторы)
- [Чеклист code review: идиоматичный C#](#чеклист-code-review-идиоматичный-c)
- [Влиятельные статьи и книги](#влиятельные-статьи-и-книги)

## Канонические источники

### Ключевые выводы
- Дизайн публичного API — по Framework Design Guidelines: онлайн-версия на learn.microsoft.com и книга 3-го издания (2020). Именование и стиль кода — по .NET coding conventions.
- Новинки языка смотреть на страницах «What's new in C# N» и в спецификациях фич (`proposals/csharp-N.0/...`): там же перечислены ограничения и breaking changes.
- Рекомендации по исключениям, DI и Options на learn.microsoft.com обновлялись в 2025–2026 годах. Это первичный источник, блоги вторичны.
- Для функционального стиля — Enrico Buonanno (книга, 2022) и блог Mark Seemann (ploeh.dk). Для DDD-приёмов (value objects, Result) — Vladimir Khorikov.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) | Microsoft, живой | Правила DO/CONSIDER/AVOID/DO NOT для именования, типов, членов, исключений |
| [Framework Design Guidelines, 3rd ed.](https://www.informit.com/store/framework-design-guidelines-conventions-idioms-and-9780135896464) | Cwalina, Barton, Abrams, 2020 | Книжная версия с комментариями авторов BCL |
| [.NET coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) | Microsoft, живой | Стиль кода: `var`, строки, LINQ, исключения, делегаты |
| [Identifier names](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names) | Microsoft, живой | `PascalCase`, `_camelCase`, префикс `I`, суффикс `Async` |
| [Best practices for exceptions](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions) | Microsoft, 2025 | Что ловить, как перебрасывать, throw-хелперы |
| [DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines) | Microsoft, 2026 | Lifetimes, dispose, антипаттерны контейнера |
| [Functional Programming in C#, 2nd ed.](https://www.manning.com/books/functional-programming-in-c-sharp-second-edition) | Enrico Buonanno, 2022 | Чистые функции, Option/Either, валидация, композиция; примеры на C# 10 |
| [Refactoring.Guru: паттерны на C#](https://refactoring.guru/design-patterns/csharp) | Alexander Shvets, живой | GoF с примерами на C# |
| [dofactory: .NET Design Patterns](https://www.dofactory.com/net/design-patterns) | dofactory, живой | GoF в «структурном» и «реальном» вариантах |

## Версии языка: C# 12–15

### Ключевые выводы
- Целевая платформа для нового кода — `net10.0` (LTS), язык C# 14. C# 15 — язык по умолчанию для `net11.0` начиная с RC1 (`LangVersion preview` больше не нужен), но .NET 11 — STS, поэтому для продакшена до GA и без осознанного выбора STS оставайтесь на `net10.0`.
- Primary constructors в обычных классах **не** создают свойств и не делают параметры `readonly`: это захваченные изменяемые переменные. Для DI это удобно, для доменных инвариантов — нет.
- Collection expressions (`[]`, `[..a, ..b]`) — стиль по умолчанию для инициализации массивов, списков и спанов.
- `field` в C# 14 убирает ручные backing-поля для валидации и ленивой инициализации. Конфликт с полем, названным `field`, решается через `@field`.
- Extension members (C# 14) дают extension-свойства и статические расширения типа. Старый синтаксис `this T` остаётся рабочим и совместимым.
- Для `lock` объявлять поле типа `System.Threading.Lock` (C# 13/.NET 9), а не `object`.

| Версия | Платформа | Главное для повседневного кода | Источник |
|---|---|---|---|
| C# 12 | .NET 8 (LTS) | Primary constructors для всех классов, collection expressions, alias any type, `ref readonly`-параметры, default-параметры лямбд, inline arrays, `[Experimental]` | [What's new in C# 12](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12) |
| C# 13 | .NET 9 | `params` коллекции (`params ReadOnlySpan<T>`), новый тип `Lock`, `\e`, partial-свойства, `allows ref struct`, `OverloadResolutionPriority` | [What's new in C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13) |
| C# 14 | .NET 10 (LTS) | Extension members, `field`, null-conditional присваивание `a?.B = x`, `nameof(List<>)`, неявные конверсии спанов, модификаторы у параметров простых лямбд, partial-конструкторы и события, пользовательские `+=` | [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) |
| C# 15 (RC1, стабилизирован) | .NET 11 RC1, GA 10 ноября 2026 | `union`-типы, `closed`-иерархии, аргументы collection expressions `[with(capacity: n), ..xs]`, extension-индексаторы, `break label`/`continue label`, невиртуальные статические члены интерфейсов. Новая модель `unsafe` в C# 15 не входит и остаётся preview | [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15), [Explore C# 15](https://devblogs.microsoft.com/dotnet/explore-csharp-15/), [RC1: C#](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/rc1/csharp.md) |

**Primary constructor в сервисе.** Когда применять: классы с DI-зависимостями, где параметры только читаются. Если нужен инвариант или защита от переприсваивания, присвоить параметр в `private readonly`-поле. Источник: [tutorial: primary constructors](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/primary-constructors).
```csharp
public sealed class OrderService(IOrderRepository repo, ILogger<OrderService> log)
{
    public async Task<Order?> Find(OrderId id, CancellationToken ct) =>
        await repo.FindAsync(id, ct);   // repo — захваченный параметр, не readonly
}
```

**`field` и null-conditional присваивание (C# 14).** Источники: [`field` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/field), [collection expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions).
```csharp
public string Name
{
    get;
    set => field = string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("Имя пустое", nameof(value))
        : value.Trim();
}

customer?.LastSeen = DateTimeOffset.UtcNow;   // правая часть не вычисляется, если customer == null
int[] all = [.. first, .. second, 42];
```

**Extension members (C# 14).** Когда применять: вспомогательные свойства и статические фабрики для чужих типов. Источники: [`extension` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/extension), [спецификация](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/extensions).
```csharp
public static class EnumerableExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        public bool IsEmpty => !source.Any();                 // extension-свойство
    }
    extension<T>(IEnumerable<T>)
    {
        public static IEnumerable<T> Empty => [];             // статический член «на типе»
    }
}
// if (orders.IsEmpty) ...
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [C# 15 union types](https://devblogs.microsoft.com/dotnet/csharp-15-union-types/) | Bill Wagner, 2026 | Первичный разбор unions: синтаксис, исчерпываемость, boxing |
| [Proposal: Unions (csharplang #9662)](https://github.com/dotnet/csharplang/issues/9662) | LDT, 2025–2026 | Статус «Implemented, Needs ECMA Spec», заметки дизайн-встреч |
| [Language Feature Status](https://github.com/dotnet/roslyn/blob/main/docs/Language%20Feature%20Status.md) | Roslyn, живой | Что уже влито в main и в какую версию |

## Типы как носители инвариантов

### Ключевые выводы
- DTO и неизменяемые значения — `record` (или `readonly record struct` для маленьких значений). Equality по значению и `with` бесплатно.
- Обязательные свойства — `required` + `init`. Конструктор с десятью параметрами не нужен, а компилятор не даст забыть поле.
- Любой примитив с доменным смыслом (ID, email, деньги) оборачивать в value object или strongly-typed ID. Генераторы Vogen и StronglyTypedId убирают шаблонный код и дают конвертеры для System.Text.Json и EF Core.
- «Parse, don't validate»: на границе (HTTP, конфиг, файл) превращать ввод в тип-доказательство через фабрику `TryCreate`/`From`, дальше не проверять повторно.
- Закрытый набор вариантов в C# 14 — абстрактный record с приватным конструктором и вложенными `sealed record`-наследниками, либо OneOf/Dunet. После .NET 11 GA — `union` или `closed`.
- `record` с изменяемыми коллекциями внутри неизменяем лишь поверхностно: использовать `IReadOnlyList<T>`/`ImmutableArray<T>`.

**Record + required + init.** Источники: [records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record), [`required`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required).
```csharp
public sealed record CreateUser
{
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}
var renamed = cmd with { DisplayName = "Анна" };   // копия, оригинал не меняется
```

**Value object / parse, don't validate.** Когда применять: значение с правилами корректности приходит извне. Конструктор приватный, создать объект можно только через фабрику. Источники: [Alexis King](https://lexi-lambda.github.io/blog/2019/11/05/parse-don-t-validate/), [Khorikov: Value Objects explained](https://enterprisecraftsmanship.com/posts/value-objects-explained/).
```csharp
public readonly record struct Email
{
    public string Value { get; }
    private Email(string value) => Value = value;

    public static bool TryCreate(string? raw, out Email email)
    {
        email = default;
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains('@')) return false;
        email = new Email(raw.Trim().ToLowerInvariant());
        return true;
    }
}
void Send(Email to) { /* корректность доказана типом */ }
```

**Strongly-typed IDs через генератор.** Когда применять: несколько сущностей с `Guid`/`int`-ключами, которые легко перепутать. Источники: [Vogen](https://github.com/SteveDunn/Vogen), [StronglyTypedId](https://github.com/andrewlock/StronglyTypedId), [серия Andrew Lock](https://andrewlock.net/series/using-strongly-typed-entity-ids-to-avoid-primitive-obsession/).
```csharp
[ValueObject<int>]                    // Vogen: генерирует From, equality, конвертеры
public partial struct CustomerId;

[StronglyTypedId]                     // StronglyTypedId: по умолчанию Guid
public partial struct OrderId;

void Cancel(OrderId order, CustomerId by) { /* перепутать аргументы нельзя */ }
```

**Закрытая иерархия сегодня (C# 14).** Приватный конструктор базового record не даёт наследоваться снаружи. Компилятор не проверяет полноту `switch` по иерархии, поэтому нужна ветка `_` с исключением. Источник: [pattern matching](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching).
```csharp
public abstract record PaymentResult
{
    private PaymentResult() { }
    public sealed record Paid(string TransactionId) : PaymentResult;
    public sealed record Declined(string Reason) : PaymentResult;
    public sealed record RequiresAction(Uri RedirectUrl) : PaymentResult;
}

string Describe(PaymentResult r) => r switch
{
    PaymentResult.Paid p => $"ok {p.TransactionId}",
    PaymentResult.Declined d => $"отказ: {d.Reason}",
    PaymentResult.RequiresAction a => $"нужен 3DS: {a.RedirectUrl}",
    _ => throw new UnreachableException(),
};
```

**Union и closed (C# 15).** Компилятор проверяет полноту `switch` без `_`. Значимые типы в union боксятся («stores its contents as a single `object?` reference»), для горячего пути нужен свой тип с `[Union]` и `TryGetValue`. В RC1 возможности стабилизированы: `public union Result(Success, Failure);` работает без `LangVersion preview`, а анализаторам доступен `ITypeSymbol.UnionCaseTypes` ([RC1: C#](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/rc1/csharp.md)). Источники: [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15), [Bill Wagner](https://devblogs.microsoft.com/dotnet/csharp-15-union-types/), [Maarten Balliauw](https://blog.maartenballiauw.be/posts/2026-06-16-discriminated-unions-in-csharp-for-real-this-time/).
```csharp
public union Pet(Cat, Dog, Bird);                 // типы-случаи не обязаны быть родственными
public closed record class JobStatus;             // наследники только в этой сборке
public record class Queued : JobStatus;
public record class Running(int Percent) : JobStatus;

string Name(Pet pet) => pet switch { Cat c => c.Name, Dog d => d.Name, Bird b => b.Name };
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [OneOf](https://github.com/mcintyre321/OneOf) | Harry McIntyre, живой | `OneOf<T0..Tn>` с `Match`/`Switch`, генератор `[GenerateOneOf]`. После C# 15 GA — кандидат на замену встроенными unions |
| [Dunet](https://github.com/domn1995/dunet) | domn1995, живой | Генератор DU на `[Union] partial record` с `Match` и поддержкой JSON |
| [Union Types Are Finally Coming to C#](https://milanjovanovic.tech/blog/union-types-are-finally-coming-to-csharp) | Milan Jovanović, 2026 | Практика unions; важно: пока это type unions, не DU с именованными кейсами |
| [Choosing between class and struct](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct) | Microsoft | Когда struct: маленький, неизменяемый, логически одно значение |
| [Functional C#: Primitive obsession](https://enterprisecraftsmanship.com/posts/functional-c-primitive-obsession/) | Vladimir Khorikov | Почему примитивы вместо доменных типов множат проверки |

## Pattern matching и nullable

### Ключевые выводы
- `switch`-выражения с property-, relational- и list-паттернами заменяют цепочки `if`/`else if` и приведения `as`+`null`-проверку.
- Проверять `null` через `is null` / `is not null`: это не зависит от перегруженного `==`.
- Nullable reference types включать во всех проектах (`<Nullable>enable</Nullable>`) и поднимать предупреждения до ошибок (`<WarningsAsErrors>nullable</WarningsAsErrors>`).
- Оператор `!` (null-forgiving) — признак, что контракт не выражен в типах. Лучше атрибуты `[NotNullWhen]`, `[MemberNotNull]`, `[MaybeNullWhen]`.
- Старый код мигрировать по файлам (`#nullable enable`) или по проектам, начиная с листьев графа зависимостей.

Источники: [patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns), [nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references), [атрибуты nullable-анализа](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/nullable-analysis), [стратегии миграции](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-migration-strategies).
```csharp
decimal Discount(Order o) => o switch
{
    { Customer.IsVip: true, Total: > 1000m } => 0.15m,
    { Items: [] } => 0m,                                  // list pattern: пустой
    { Items: [var only] } when only.Qty > 10 => 0.05m,   // ровно один элемент
    { Total: >= 100m and < 1000m } => 0.02m,
    _ => 0m,
};

public bool TryGetUser(UserId id, [NotNullWhen(true)] out User? user) { /* ... */ }
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Nullable warnings](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/nullable-warnings) | Microsoft | Что означает каждое CS86xx и как исправлять без `!` |
| [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching) | Microsoft | Сценарии: проверка типов, дискретные значения, кортежи, list patterns |

## Ошибки: исключения и Result

### Ключевые выводы
- Позиция Microsoft: о сбое выполнения сообщают исключением, а не кодом ошибки. Цитата: «a crashed app is more reliable and diagnosable than an app with undefined behavior».
- Ожидаемый частый исход (не распарсилось, не найдено, не прошло валидацию) — `Try*`-метод, `null`/`Option` или Result-тип. Исключение для потока управления в горячем пути — антипаттерн.
- Классификация Эрика Липперта: fatal (не ловить), boneheaded (баг, исправлять код), vexing (избегать через `Try*`), exogenous (внешние сбои, обрабатывать).
- Ловить только то, что можно обработать. Перебрасывать `throw;`, а не `throw ex;` (CA2200). Вне `catch` — `ExceptionDispatchInfo`.
- Аргументы проверять throw-хелперами: `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIfNegative` (CA1510–CA1513). В async-методах проверять до первого `await`.
- Ловить `OperationCanceledException`, а не `TaskCanceledException`.
- Result-библиотеки (ErrorOr, FluentResults, LanguageExt) уместны для бизнес-ошибок на уровне домена и API. Сбои ввода-вывода и баги оставлять исключениями: это аргумент самого автора ROP Скотта Влашина.
- Нельзя смешивать стили в одном слое: либо метод возвращает `ErrorOr<T>`, либо бросает, но не то и другое для одного и того же исхода.

```csharp
// Ожидаемый бизнес-исход — Result (ErrorOr)
public ErrorOr<Order> Place(Cart cart)
{
    if (cart.Items.Count == 0) return Error.Validation("Cart.Empty", "Корзина пуста");
    return new Order(cart);
}

// Внешний сбой — исключение с контекстом
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
{
    throw new PaymentGatewayUnavailableException("Шлюз недоступен", ex);
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Design guidelines: Exceptions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions) | Microsoft | «DO NOT return error codes», выбор типа исключения |
| [Vexing exceptions](https://ericlippert.com/2008/09/10/vexing-exceptions/) | Eric Lippert, 2008 | Четыре категории исключений |
| [Exception Types](https://blog.stephencleary.com/2011/03/exception-types.html) | Stephen Cleary, 2011 | Та же классификация с практическими выводами |
| [Error handling: Exception or Result?](https://enterprisecraftsmanship.com/posts/error-handling-exception-or-result/) | Vladimir Khorikov | Где граница между ожидаемыми и неожиданными ошибками |
| [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/) и [Against ROP](https://fsharpforfunandprofit.com/posts/against-railway-oriented-programming/) | Scott Wlaschin, 2014/2019 | Подход и его границы от самого автора |
| [ErrorOr](https://github.com/amantinband/error-or) | Amichai Mantinband, живой | `ErrorOr<T>`, встроенные Validation/NotFound, `Then`/`Match` |
| [FluentResults](https://github.com/altmann/FluentResults) | Michael Altmann, живой | `Result<T>` с цепочкой причин и метаданными |
| [language-ext](https://github.com/louthy/language-ext) | Paul Louth, живой | Полноценное FP: `Option`, `Either`, `Fin`, traits. v5 — большой рефакторинг, на октябрь 2026 года всё ещё бета (последняя `5.0.0-beta-77`, декабрь 2025), стабильная — 4.4.9; порог входа высокий |

## Generics, интерфейсы и расширения

### Ключевые выводы
- Интерфейс — контракт для подмены и тестов, абстрактный класс — общий код и состояние для семейства типов. Интерфейс с единственной реализацией и без тестовых двойников не нужен.
- Default interface methods придуманы для эволюции API без breaking change, а не для множественного наследования поведения.
- Static abstract/virtual члены интерфейсов (C# 11) и generic math (`INumber<T>`) позволяют писать алгоритмы один раз для всех числовых типов и требовать статические фабрики (`T.Parse`, `T.Zero`).
- Ограничения generics (`where T : notnull`, `struct`, `allows ref struct`) делают контракт явным и иногда убирают boxing.
- LINQ — для читаемых преобразований коллекций. На горячем пути проверять аллокации; не перечислять `IEnumerable<T>` дважды (CA1851); `Any()` вместо `Count() > 0`; материализовать (`ToList()`) осознанно.
- Методы-расширения — для удобного API поверх чужих типов, не для сокрытия бизнес-логики со скрытыми зависимостями.

Источники: [static virtual members tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/static-virtual-interface-members), [generic math](https://learn.microsoft.com/en-us/dotnet/standard/generics/math), [default interface methods](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interface-implementation/default-interface-methods-versions), [LINQ](https://learn.microsoft.com/en-us/dotnet/csharp/linq/).
```csharp
public static T Sum<T>(ReadOnlySpan<T> values) where T : INumber<T>
{
    T total = T.Zero;
    foreach (var v in values) total += v;
    return total;
}

public interface IParsableId<TSelf> where TSelf : IParsableId<TSelf>
{
    static abstract TSelf Parse(string raw);       // статическая фабрика в контракте
}
```

## Ресурсы, async и синхронизация

### Ключевые выводы
- Владелец `IDisposable` освобождает его через `using`/`await using`. Полный dispose pattern с финализатором нужен только при прямом владении неуправляемым ресурсом; обычно хватает `sealed`-класса с простым `Dispose()` или `SafeHandle`.
- Если класс владеет `IAsyncDisposable`-ресурсом (поток, соединение), реализовать `IAsyncDisposable` и освобождать через `await using`.
- Объекты, полученные из DI, не освобождать вручную: это делает контейнер.
- `async void` — только для обработчиков событий UI. В остальных случаях исключение роняет процесс.
- Не блокироваться на async-коде (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`): дедлоки и голодание пула потоков.
- `CancellationToken` передавать по всей цепочке вызовов. `CancellationTokenSource` с таймаутом — освобождать.
- `ConfigureAwait(false)` — в коде библиотек общего назначения. В ASP.NET Core нет `SynchronizationContext`, там это шум.
- Для `lock` — поле `private readonly Lock _gate = new();` (C# 13): компилятор генерирует `Lock.EnterScope()` вместо `Monitor`. Не приводить `Lock` к `object`, иначе снова будет `Monitor`.

Источники: [Implement Dispose](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose), [Implement DisposeAsync](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync), [System.Threading.Lock](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock), [lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock), [AsyncGuidance (David Fowler)](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md).
```csharp
public sealed class ReportWriter(Stream output) : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private int _written;

    public void Count() { lock (_gate) { _written++; } }   // Lock.EnterScope под капотом

    public async ValueTask DisposeAsync()
    {
        await output.FlushAsync();
        await output.DisposeAsync();
    }
}

await using var writer = new ReportWriter(File.Create(path));
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) | Stephen Toub, 2019 | Когда нужен `ConfigureAwait(false)` и когда нет |
| [Understanding ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/) | Stephen Toub, 2018 | Ограничения `ValueTask`: не ожидать дважды |
| [Async/Await best practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming) | Stephen Cleary, 2013 | «Async all the way», избегать `async void`. Классика, местами про старый ASP.NET |
| [Async scenarios](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios) | Microsoft | I/O-bound и CPU-bound, советы по ревью |
| [CA2000](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2000), [CA2007](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2007) | Microsoft | Анализаторы: неосвобождённые объекты, `ConfigureAwait` |

## DI и Options

### Ключевые выводы
- Зависимости — через конструктор. Контейнер используется только в composition root (`Program.cs`), а не как service locator внутри классов.
- Lifetimes: singleton — только для потокобезопасных сервисов с дорогим или общим состоянием; scoped — на запрос/единицу работы (`DbContext`); transient — лёгкие stateless-сервисы.
- Captive dependency: singleton, держащий scoped-сервис, ломает данные между запросами. Включать проверку scopes (`ValidateScopes`, `ValidateOnBuild`); в Development в ASP.NET Core она включена по умолчанию.
- Не вызывать `BuildServiceProvider()` при регистрации; фабрики делать быстрыми и синхронными; не резолвить transient `IDisposable` из root — утечка памяти.
- Большой конструктор (7+ зависимостей) — сигнал нарушения SRP, а не повод перейти на service locator.
- Keyed services (.NET 8+): `AddKeyedSingleton<IMessageSender, SmsSender>("sms")` + `[FromKeyedServices("sms")]` — для нескольких реализаций одного интерфейса без самописных фабрик.
- Конфигурация — Options pattern: `IOptions<T>` (singleton, без перезагрузки), `IOptionsSnapshot<T>` (scoped, нельзя в singleton), `IOptionsMonitor<T>` (singleton, уведомления об изменениях). Валидировать при старте: `ValidateDataAnnotations().ValidateOnStart()` или source-generated валидатор.

Источники: [DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines), [DI overview](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection), [Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options), [options validation generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator).
```csharp
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddKeyedSingleton<INotifier, EmailNotifier>("email");
builder.Services.AddKeyedSingleton<INotifier, SmsNotifier>("sms");

builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration("Smtp")
    .ValidateDataAnnotations()
    .ValidateOnStart();                       // ошибка конфига — при старте, а не в 3 часа ночи

public sealed class AlertService([FromKeyedServices("sms")] INotifier sms, IOptions<SmtpOptions> smtp) { }
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Service Locator is an Anti-Pattern](https://blog.ploeh.dk/2010/02/03/ServiceLocatorisanAnti-Pattern/) | Mark Seemann, 2010 | «A DI container used as a Service Locator is an anti-pattern. A DI container used as a composition root is fine» |
| [Service Locator violates SOLID](https://blog.ploeh.dk/2014/05/15/service-locator-violates-solid/), […violates encapsulation](https://blog.ploeh.dk/2015/10/26/service-locator-violates-encapsulation/) | Mark Seemann, 2014–2015 | Продолжение аргументации |
| [Captive Dependency](https://blog.ploeh.dk/2014/06/02/captive-dependency/) | Mark Seemann, 2014 | Источник термина, на который ссылается Microsoft |
| [Composition Root](https://blog.ploeh.dk/2011/07/28/CompositionRoot/) | Mark Seemann, 2011 | Где единственное место для контейнера |
| [Keyed services in .NET 8](https://andrewlock.net/exploring-the-dotnet-8-preview-keyed-services-dependency-injection-support/) | Andrew Lock, 2023 | Как устроены keyed services и их ограничения |
| [Keyed services: named registrations](https://blog.maartenballiauw.be/posts/2026-08-11-keyed-services-named-registrations-in-dotnet-service-provider/) | Maarten Balliauw, 2026 | Свежий практический обзор |
| [Breaking change: keyed validation (.NET 9)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/non-keyed-params) | Microsoft, 2024 | `[FromKeyedServices]` без keyed-регистрации теперь ошибка |

## Source generators как идиома

### Ключевые выводы
- В современном .NET шаблонный код генерируют при компиляции, а не делают через reflection в рантайме: это быстрее, дружит с trimming и Native AOT, ошибки видны при сборке.
- Использовать встроенные генераторы: `[GeneratedRegex]`, `[LoggerMessage]`, `JsonSerializerContext` для System.Text.Json, `[LibraryImport]`, валидатор Options.
- Маппинг: Mapperly (генератор) или ручной код вместо AutoMapper. Медиатор: Mediator (Martin Othamar, MIT, генератор) или обычные сервисы вместо MediatR.
- Свой генератор писать как incremental (`IIncrementalGenerator`); `ISourceGenerator` `[устарело]`.

Источник: [Source generators overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview).
```csharp
public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Заказ {OrderId} отклонён: {Reason}")]
    public static partial void OrderDeclined(ILogger logger, OrderId orderId, string reason);
}

public static partial class Patterns
{
    [GeneratedRegex(@"^\d{6}$")] public static partial Regex PostalCode();
}

[Mapper] public static partial class OrderMapper
{
    public static partial OrderDto ToDto(Order order);   // Riok.Mapperly
}
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Mapperly](https://github.com/riok/mapperly) | riok, живой | Маппинг на этапе сборки, читаемый сгенерированный код |
| [Mediator](https://github.com/martinothamar/Mediator) | Martin Othamar, живой | API в духе MediatR без reflection, совместим с AOT, MIT |
| [AutoMapper vs Mapperly vs hand-written (2026)](https://startdebugging.net/2026/08/automapper-vs-mapperly-vs-hand-written-mapping-in-2026/) | startdebugging.net, 2026 | Сравнение после смены лицензии AutoMapper |

## Мини-каталог паттернов

| Задача | Решение на C# | Источник |
|---|---|---|
| Примитив с доменным смыслом | `readonly record struct` с фабрикой `TryCreate` или Vogen/StronglyTypedId | [Vogen](https://github.com/SteveDunn/Vogen), [Andrew Lock](https://andrewlock.net/series/using-strongly-typed-entity-ids-to-avoid-primitive-obsession/) |
| Закрытое множество вариантов | Абстрактный record с приватным ctor + `sealed`-наследники; C# 15: `union`/`closed` | [C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15), [Dunet](https://github.com/domn1995/dunet) |
| Ожидаемая бизнес-ошибка | `ErrorOr<T>`/`Result<T>` или `Try*`-метод | [ErrorOr](https://github.com/amantinband/error-or), [Khorikov](https://enterprisecraftsmanship.com/posts/error-handling-exception-or-result/) |
| Много необязательных параметров | Объект-параметр с `init`/`required` и `with`; Builder — для сложной пошаговой сборки | [Refactoring.Guru: Builder](https://refactoring.guru/design-patterns/csharp) |
| Несколько алгоритмов за одним контрактом | Strategy: интерфейс + keyed services или `Func<>`-делегат | [DI overview](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) |
| Сквозная функциональность (лог, ретраи, кэш) | Decorator через DI (Scrutor `Decorate` или ручная регистрация), middleware, `DelegatingHandler` | [dofactory: Decorator](https://www.dofactory.com/net/design-patterns) |
| Создание объектов с зависимостями в рантайме | Фабрика + `ActivatorUtilities.CreateInstance`, `IHttpClientFactory`, `IDbContextFactory` | [DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines) |
| Освобождение ресурса | `using`/`await using`, `IDisposable`/`IAsyncDisposable` | [Implement DisposeAsync](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync) |
| Конфигурация | Options pattern + `ValidateOnStart` | [Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) |
| Наблюдатель / события | `event EventHandler<T>`; поток событий — `IAsyncEnumerable<T>` или `System.Threading.Channels` | [Refactoring.Guru: Observer](https://refactoring.guru/design-patterns/csharp) |
| Расширение чужого типа | Extension methods, в C# 14 — extension-свойства и статические члены | [`extension`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/extension) |
| Числовой алгоритм для любых типов | Generic math (`INumber<T>`) | [Generic math](https://learn.microsoft.com/en-us/dotnet/standard/generics/math) |
| Чистое ядро, эффекты на краях | Functional core / imperative shell, ports and adapters | [Seemann: Functional architecture](https://blog.ploeh.dk/2016/03/18/functional-architecture-is-ports-and-adapters/) |
| Command/Query-обработчики | Обычные сервисы; MediatR — только если pipeline behaviors реально нужны | [startdebugging.net](https://startdebugging.net/2026/05/mediatr-vs-plain-service-classes-in-2026/) |
| Маппинг DTO ↔ домен | Ручной метод или Mapperly | [Mapperly](https://github.com/riok/mapperly) |
| Ленивое значение | `Lazy<T>`, `field ??= ...` в свойстве (C# 14) | [`field`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/field) |

## Лицензии библиотек: изменения 2025–2026

### Ключевые выводы
- Перед добавлением зависимости проверять лицензию конкретной **версии**: несколько популярных библиотек стали коммерческими начиная с мажорного релиза, старые версии остались под прежней лицензией без обновлений.
- **AutoMapper 15+ и MediatR 13+** (2 июля 2025, Jimmy Bogard, Lucky Penny Software): двойная лицензия RPL-1.5 / коммерческая. Бесплатная Community-лицензия — компаниям с выручкой до $5 млн в год, некоммерческим организациям, образованию и для non-production. MediatR ≤ 12.x и AutoMapper ≤ 14.x остаются под прежними лицензиями.
- **FluentAssertions 8+** (январь 2025, партнёрство с Xceed): коммерческая лицензия для коммерческого использования. 7.x — Apache 2.0. Сообщество форкнуло проект: AwesomeAssertions (Apache 2.0).
- **MassTransit 9** (Chris Patterson, Massient Inc.): коммерческая лицензия. v8 остаётся open source, исправления безопасности для неё обещаны до конца 2026 года.
- Практический вывод: в новом коде MediatR и AutoMapper по умолчанию не нужны (обычные сервисы, Mapperly, ручной маппинг). FluentAssertions → AwesomeAssertions или Shouldly. Для шины сообщений сравнивать MassTransit 9 (платно), Wolverine, Rebus, Brighter. У последних трёх на октябрь 2026 года ядро под MIT (файлы LICENSE в репозиториях [Wolverine](https://github.com/JasperFx/wolverine), [Rebus](https://github.com/rebus-org/Rebus), [Brighter](https://github.com/BrighterCommand/Brighter)); платными бывают только поддержка и дополнения (например, Rebus Pro).

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [AutoMapper and MediatR Commercial Editions Launch Today](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/) | Jimmy Bogard, 2025 | Первичный источник: RPL-1.5, тарифы, критерии Community |
| [Lucky Penny Software](https://luckypennysoftware.com/) | Lucky Penny, 2025– | Актуальные условия лицензий |
| [MediatR vs plain service classes in 2026](https://startdebugging.net/2026/05/mediatr-vs-plain-service-classes-in-2026/) | startdebugging.net, 2026 | Когда MediatR ещё оправдан (pipeline behaviors) |
| [Announcing MassTransit v9](https://masstransit.io/introduction/v9-announcement) | Chris Patterson, 2025 | Условия v9 и поддержка v8 |
| [MediatR and MassTransit Going Commercial](https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you) | Milan Jovanović, 2025 | Что делать командам |
| [Fluent Assertions license change](https://www.devclass.com/development/2025/01/16/another-open-source-project-shifts-to-restrictive-license-fluent-assertions-following-xceed-partnership/1621343) | DevClass, 2025 | Хронология и цена ($129.95 за разработчика в год) |
| [AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions) | сообщество, 2025– | Форк FluentAssertions под Apache 2.0 |

## Антипаттерны

### Ключевые выводы
- **`async void`** вне обработчиков событий: исключение роняет процесс, вызывающий не может ждать завершения.
- **Sync-over-async** (`.Result`, `.Wait()`): дедлоки и голодание пула потоков. «Async all the way».
- **Service locator**: `IServiceProvider.GetService` внутри бизнес-кода прячет зависимости и переносит ошибки из компиляции в рантайм.
- **Captive dependency**: scoped или transient-сервис внутри singleton.
- **Исключения для потока управления**: `try { int.Parse } catch` вместо `TryParse`, исключения на ожидаемые бизнес-исходы в горячем пути.
- **`catch (Exception) { }`** и `throw ex;`: проглоченные ошибки и потерянный стек.
- **Primitive obsession**: `string email`, `Guid customerId`, `decimal amount` без валюты.
- **Anemic domain model** — спорный пункт. Фаулер (2003) называет его антипаттерном для сложного домена. Для CRUD и transaction script это нормальный выбор: не надо навязывать DDD там, где нет инвариантов.
- **Переабстрагирование**: интерфейс на каждый класс, generic repository поверх EF Core `DbSet`, «Manager»/«Helper»/«Util»-классы, слои ради слоёв.
- **MediatR и AutoMapper по умолчанию**: косвенность без выгоды, навигация «Go to definition» ломается, ошибки маппинга видны только в рантайме. Плюс лицензия с 2025 года.
- **Изменяемые публичные коллекции и `set;` везде**: внешний код ломает инварианты. Отдавать `IReadOnlyList<T>`, сеттеры — `init` или приватные.
- **`!` для подавления nullable-предупреждений** вместо исправления контракта.
- **`DateTime.Now` в логике**: недетерминированные тесты. Использовать `TimeProvider` (.NET 8+).
- **Сравнение и форматирование строк по текущей культуре**: `ToString()`, `double.Parse`, `string.Format`, `StartsWith(string)`, `IndexOf(string)`, `CompareTo` без `CultureInfo`/`StringComparison` зависят от локали машины. Типичный баг: `double.Parse("1.5")` на машине с `ru-RU` бросает `FormatException` (разделитель — запятая), хотя на CI с `en-US` тест зелёный, а `ToUpper` под `tr-TR` ломает сравнение «i»/«I». Для идентификаторов, ключей, путей и протоколов — `StringComparison.Ordinal`/`OrdinalIgnoreCase` и `CultureInfo.InvariantCulture`, текущая культура — только для текста, который видит пользователь. Ловят правила CA1305, CA1307, CA1309, CA1310 (по умолчанию в .NET 10 выключены, включайте) ([Best practices for comparing strings](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings), [CA1310](https://github.com/dotnet/docs/blob/main/docs/fundamentals/code-analysis/quality-rules/ca1310.md)).
- **`lock (this)`, `lock (typeof(X))`, `lock` на строке**: внешний код может взять тот же замок. Нужно приватное поле `Lock`.

**Исключения для потока управления.** Источник: [Best practices for exceptions](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions).
```csharp
// Плохо: исключение на частом исходе
try { var n = int.Parse(input); Use(n); } catch (FormatException) { Use(0); }
// Лучше
Use(int.TryParse(input, out var n) ? n : 0);
```

**Service locator.** Источник: [DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines).
```csharp
// Плохо: зависимость спрятана, ошибка регистрации всплывёт в рантайме
public sealed class Checkout(IServiceProvider sp)
{
    public void Run() => sp.GetRequiredService<IPaymentGateway>().Charge();
}
// Лучше: зависимость видна в сигнатуре
public sealed class Checkout(IPaymentGateway gateway) { public void Run() => gateway.Charge(); }
```

**Captive dependency.** Источник: [Seemann](https://blog.ploeh.dk/2014/06/02/captive-dependency/).
```csharp
builder.Services.AddSingleton<PriceCache>();        // singleton...
builder.Services.AddScoped<AppDbContext>();
public sealed class PriceCache(AppDbContext db) { } // ...держит scoped DbContext навсегда
// Лучше: внедрить IDbContextFactory<AppDbContext> или IServiceScopeFactory и создавать scope на операцию
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [AnemicDomainModel](https://martinfowler.com/bliki/AnemicDomainModel.html) | Martin Fowler, 2003 | Исходная аргументация; читать вместе с контраргументами про CRUD |
| [AsyncGuidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md) | David Fowler, живой | Каталог async-антипаттернов с примерами «плохо/хорошо» |
| [Service locator pattern](https://www.deviq.com/design-patterns/service-locator-pattern/) | DevIQ (Steve Smith) | Краткая сводка аргументов |

## Именование и анализаторы

### Ключевые выводы
- `PascalCase` для типов, методов, свойств, констант; `camelCase` для параметров и локальных переменных; `_camelCase` для приватных полей; `I` — префикс интерфейсов; суффикс `Async` у методов, возвращающих `Task`/`ValueTask`; `Exception` — суффикс исключений; без венгерской нотации.
- Стиль кода фиксировать в `.editorconfig` в корне репозитория, проверять в CI: `dotnet format --verify-no-changes`.
- Включать анализаторы .NET (`<AnalysisLevel>latest-recommended</AnalysisLevel>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`), `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` в CI.
- Классы по умолчанию `sealed`, если наследование не задумано: это явный контракт и небольшой выигрыш в производительности (CA1852).
- Подавления — точечно, `[SuppressMessage(..., Justification = "...")]` или `#pragma warning disable` с комментарием. Не глушить правило на весь проект без причины.

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>
</PropertyGroup>
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Naming guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines) | Microsoft | Правила именования публичного API |
| [Code analysis overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) | Microsoft | `AnalysisLevel`, `AnalysisMode`, CA/IDE-правила |
| [roslyn-analyzers](https://github.com/dotnet/roslyn-analyzers) | dotnet, живой | Исходники и обсуждения правил CA |

## Чеклист code review: идиоматичный C#

1. В проекте включены `<Nullable>enable</Nullable>` и предупреждения как ошибки в CI. Нет `!` без комментария, почему значение не `null`.
2. Доменные значения (ID, email, деньги) — value objects или strongly-typed IDs, а не голые `Guid`/`string`/`decimal`.
3. Внешний ввод разбирается на границе в типы с инвариантами (`TryCreate`/`From`); внутри домена повторных проверок нет.
4. DTO и неизменяемые данные — `record`; обязательные поля — `required`; сеттеры — `init` или приватные.
5. Наружу отдаются `IReadOnlyList<T>`/`IReadOnlyCollection<T>`, а не изменяемые `List<T>`.
6. Закрытое множество вариантов — закрытая иерархия records (или `union`/`closed` на C# 15+), а не набор флагов и nullable-полей.
7. `switch`-выражения с паттернами вместо цепочек `if`/`as`; проверки `null` через `is null`/`is not null`.
8. Исключения — для неожиданных сбоев; ожидаемые исходы — `Try*`, `null` с явным контрактом или Result. Нет исключений для потока управления в горячем пути.
9. Нет `catch (Exception)` без логирования и переброса; переброс — `throw;`, а не `throw ex;`.
10. Аргументы проверяются throw-хелперами (`ThrowIfNull`, `ThrowIfNullOrWhiteSpace`, `ThrowIfNegative`); в async-методах — до первого `await`.
11. Свои исключения: суффикс `Exception`, три стандартных конструктора, полезные свойства, `innerException` сохранён.
12. Нет `async void`, кроме обработчиков событий UI.
13. Нет `.Result`, `.Wait()`, `GetAwaiter().GetResult()` на async-коде.
14. `CancellationToken` принимается публичными async-методами и передаётся дальше; `CancellationTokenSource` освобождается.
15. Async-методы называются с суффиксом `Async` и возвращают `Task`/`ValueTask`; `ValueTask` не ожидается дважды.
16. `IDisposable`/`IAsyncDisposable` освобождаются через `using`/`await using`. Объекты из DI вручную не освобождаются.
17. Поле для `lock` — `private readonly Lock`; нет `lock (this)`, `lock` на типе или строке.
18. Зависимости приходят через конструктор; нет `IServiceProvider.GetService` в бизнес-коде; нет `BuildServiceProvider()` при регистрации.
19. Lifetimes согласованы: singleton не зависит от scoped/transient; scope validation включена.
20. Primary constructor в классе не используется там, где нужен `readonly`-инвариант; параметр не переприсваивается.
21. Конфигурация — через Options pattern с валидацией при старте (`ValidateOnStart`), а не `IConfiguration["Key"]` в глубине кода.
22. Подходящий вариант `IOptions`/`IOptionsSnapshot`/`IOptionsMonitor`; `IOptionsSnapshot` не внедряется в singleton.
23. Интерфейсы есть только там, где нужна подмена (несколько реализаций, тестовый двойник, граница модуля). Нет «интерфейса на каждый класс».
24. Нет generic repository поверх EF Core и других слоёв, которые лишь проксируют вызовы.
25. LINQ читаем; `IEnumerable<T>` не перечисляется дважды; `Any()` вместо `Count() > 0`; материализация осознанная.
26. Reflection-шаблоны заменены генераторами, где это возможно (`[GeneratedRegex]`, `[LoggerMessage]`, `JsonSerializerContext`, Mapperly).
27. Новые зависимости проверены на лицензию конкретной версии (MediatR 13+, AutoMapper 15+, FluentAssertions 8+, MassTransit 9+ — коммерческие).
28. MediatR/AutoMapper не добавлены «по привычке»: есть конкретная выгода (pipeline behaviors, сложный маппинг).
29. Классы, не предназначенные для наследования, — `sealed`; поля — `readonly`, где возможно.
30. Время берётся из `TimeProvider`, а не из `DateTime.Now`/`UtcNow` напрямую в логике.
31. Именование по .NET conventions: `PascalCase`, `_camelCase` для приватных полей, `I`-префикс, без сокращений и венгерской нотации.
32. Публичный API документирован XML-комментариями; исключения, которые бросает метод, перечислены в `<exception>`.
33. Нет `Equals`/`GetHashCode`/`ToString`, которые бросают исключения (CA1065).
34. Возможности C# 15 (`union`, `closed`) появляются в продакшен-коде только вместе с осознанным переходом на `net11.0` (STS, GA 10 ноября 2026), а не через `LangVersion` поверх `net10.0`.
35. `dotnet build` без предупреждений и `dotnet format --verify-no-changes` проходят в CI.

## Влиятельные статьи и книги

| Статья / книга | Автор, год | Зачем читать |
|---|---|---|
| [Parse, don't validate](https://lexi-lambda.github.io/blog/2019/11/05/parse-don-t-validate/) | Alexis King, 2019 | Тип как доказательство проверки |
| [Vexing exceptions](https://ericlippert.com/2008/09/10/vexing-exceptions/) | Eric Lippert, 2008 | Какие исключения ловить |
| [Service Locator is an Anti-Pattern](https://blog.ploeh.dk/2010/02/03/ServiceLocatorisanAnti-Pattern/) | Mark Seemann, 2010 | Классика про DI |
| [Functional architecture is Ports and Adapters](https://blog.ploeh.dk/2016/03/18/functional-architecture-is-ports-and-adapters/) | Mark Seemann, 2016 | Чистое ядро и эффекты на краях |
| [Encapsulation in Functional Programming](https://blog.ploeh.dk/2022/10/24/encapsulation-in-functional-programming/) | Mark Seemann, 2022 | Инварианты и контракты в FP-стиле |
| [Integration-testing composed functions](https://blog.ploeh.dk/2015/12/21/integration-testing-composed-functions/) | Mark Seemann, 2015 | Тестирование при функциональной композиции |
| [Functional Programming in C#, 2nd ed.](https://www.manning.com/books/functional-programming-in-c-sharp-second-edition) | Enrico Buonanno, 2022 | Главная книга по FP на C# |
| [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/) | Scott Wlaschin, 2014 | Модель Result-цепочек (пример на F#, идея переносится) |
| [AnemicDomainModel](https://martinfowler.com/bliki/AnemicDomainModel.html) | Martin Fowler, 2003 | Исходный текст спора о «тонких» моделях |
| [Value Objects explained](https://enterprisecraftsmanship.com/posts/value-objects-explained/) | Vladimir Khorikov | Value object против entity |
| [Strongly-typed IDs (серия)](https://andrewlock.net/series/using-strongly-typed-entity-ids-to-avoid-primitive-obsession/) | Andrew Lock, 2019–2023 | От ручного кода до генератора, EF Core |
| [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) | Stephen Toub, 2019 | Окончательный ответ про `ConfigureAwait` |
| [Async and Await](https://blog.stephencleary.com/2012/02/async-and-await.html) | Stephen Cleary, 2012 | Базовая модель async/await |
| [AsyncGuidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md) | David Fowler, живой | Список правил async с примерами |
| [C# 15 union types](https://devblogs.microsoft.com/dotnet/csharp-15-union-types/) | Bill Wagner, 2026 | Будущее моделирования вариантов в C# |
| [Discriminated unions in C#, for real this time](https://blog.maartenballiauw.be/posts/2026-06-16-discriminated-unions-in-csharp-for-real-this-time/) | Maarten Balliauw, 2026 | Когда переходить на unions |

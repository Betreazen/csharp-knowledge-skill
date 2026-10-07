# Инструментарий .NET: dotnet CLI, анализаторы, тесты и CI

Справочник по инструментам вокруг компилятора C# по состоянию на октябрь 2026 года. Актуальный LTS — .NET 10 (runtime 10.0.12, SDK 10.0.401, C# 14, сентябрьский патч от 8 сентября 2026). .NET 11 (STS, C# 15) вышел в RC1 8 сентября 2026 с go-live лицензией, GA запланирован на 10 ноября 2026. В тот же день заканчивается поддержка .NET 8 и .NET 9, так что новые проекты начинайте на `net10.0`. Для каждого инструмента указано, что он делает, как его поставить, какие команды нужны чаще всего и какие у него оговорки. В конце собраны порядок шагов CI, готовый workflow и шаблоны `Directory.Build.props` и `.editorconfig`. В скобках у пакетов стоит последняя стабильная версия на NuGet на 7 октября 2026.

- [0. Как ставить инструменты](#0-как-ставить-инструменты)
- [1. База: SDK, global.json, dotnet CLI](#1-база-sdk-globaljson-dotnet-cli)
- [2. Компилятор и анализаторы](#2-компилятор-и-анализаторы)
- [3. Форматирование](#3-форматирование)
- [4. Тесты](#4-тесты)
- [5. Покрытие и мутационное тестирование](#5-покрытие-и-мутационное-тестирование)
- [6. Бенчмарки](#6-бенчмарки)
- [7. Диагностика и профилирование](#7-диагностика-и-профилирование)
- [8. Зависимости и supply chain](#8-зависимости-и-supply-chain)
- [9. Упаковка и релиз](#9-упаковка-и-релиз)
- [10. IDE, LSP и EF Core tooling](#10-ide-lsp-и-ef-core-tooling)
- [11. Минимальный набор и инструменты по ситуации](#11-минимальный-набор-и-инструменты-по-ситуации)
- [12. Порядок CI-пайплайна и workflow](#12-порядок-ci-пайплайна-и-workflow)
- [13. Шаблоны: Directory.Build.props, .editorconfig, global.json](#13-шаблоны-directorybuildprops-editorconfig-globaljson)
- [14. Ресурсы](#14-ресурсы)

## 0. Как ставить инструменты

**Ключевые выводы**
- CLI-инструменты (.NET tools) ставьте локально в репозиторий: `dotnet new tool-manifest`, затем `dotnet tool install <id>`. Версии фиксируются в `.config/dotnet-tools.json`, а в CI их восстанавливает одна команда `dotnet tool restore`.
- Для разового запуска без установки в .NET 10 есть `dotnet tool exec <id>[@версия]` и короткий скрипт `dnx`. Если рядом лежит манифест, берётся версия из него. Флаг `-y`/`--yes` принимает вопрос о скачивании пакета без интерактива, но по документации он появился только в SDK .NET 11; на SDK 10 в CI надёжнее локальный манифест и `dotnet tool restore` ([dotnet tool exec](https://github.com/dotnet/docs/blob/main/docs/core/tools/dotnet-tool-exec.md)).
- Анализаторы, тестовые фреймворки и расширения покрытия — это NuGet-пакеты, а не tools. Их версии держите в `Directory.Packages.props` (Central Package Management).
- `--cli-schema` у любой команды выводит JSON с аргументами и опциями. Агенту это удобнее, чем разбирать `--help`.

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Manage .NET tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) | Microsoft, 2026 | Глобальные и локальные tools, манифест |
| [dotnet tool exec](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec) | Microsoft, 2025–2026 | Разовый запуск и `dnx` |
| [What's new in the SDK for .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk) | Microsoft, 2025–2026 | Все новшества CLI .NET 10 одним списком |

## 1. База: SDK, global.json, dotnet CLI

**Ключевые выводы**
- Закрепите SDK в `global.json`: `"version": "10.0.401", "rollForward": "latestFeature"`. Без файла берётся самый свежий установленный SDK, включая preview, и сборка у агента, разработчика и CI расходится. Если используете lock-файлы NuGet, документация советует `rollForward: "disable"`: при смене feature band меняется pruning пакетов, и lock-файл начинает «плыть».
- В .NET 10 у `global.json` появились `paths` (локальный SDK в `.dotnet/` репозитория) и `errorMessage`, а также раздел `test.runner` для выбора MTP.
- `dotnet new sln` в .NET 10 по умолчанию создаёт `.slnx` (XML вместо старого формата). Старый `.sln` переводится командой `dotnet sln migrate`.
- Агенту в цикле правки достаточно `dotnet build`: он выполняет restore и компиляцию с анализаторами. Отдельный `dotnet restore` нужен в CI, чтобы прогнать его один раз в режиме `--locked-mode`, а дальше передавать `--no-restore`.
- File-based apps: `dotnet run app.cs` запускает одиночный файл без проекта. Директивы `#:package Humanizer@2.14.1`, `#:sdk Microsoft.NET.Sdk.Web`, `#:property PublishAot=false`, `#:project ../Lib/Lib.csproj`. `dotnet publish app.cs` по умолчанию собирает native AOT. Когда скрипт разросся, `dotnet project convert app.cs` превращает его в проект. Это замена PowerShell- и Python-скриптов для утилит репозитория.
- В .NET 10 появились формы «существительное-глагол»: `dotnet package add|list|remove|update`, `dotnet reference add|list|remove`. Старые `dotnet add package` работают, но в новых скриптах пишите новые формы. На SDK 9 и раньше их нет.
- Для разбора медленной или странной сборки используйте `-bl` (binary log) и MSBuild Structured Log Viewer.

| Команда | Что делает | Полезные флаги |
|---|---|---|
| `dotnet --info`, `dotnet --list-sdks`, `dotnet sdk check` | Какой SDK выбран, что установлено, что устарело | — |
| `dotnet new <template>` | Проекты и конфиги: `console`, `classlib`, `webapi`, `worker`, `xunit`, `mstest`, `nunit`, `sln`, `globaljson`, `buildprops`, `editorconfig`, `tool-manifest`, `nugetconfig`, `gitignore` | `-n`, `-o`, `-f net10.0`; `mstest --test-runner MSTest --sdk`; `globaljson --sdk-version 10.0.401 --roll-forward latestFeature --test-runner Microsoft.Testing.Platform` |
| `dotnet build` | Restore + компиляция + анализаторы | `-c Release`, `--no-restore`, `-warnaserror`, `-p:Prop=Value`, `-bl`, `--artifacts-path` |
| `dotnet test` | Тесты (VSTest или MTP, см. раздел 4) | `--no-build`, `-c Release`, `--project`, `--solution` |
| `dotnet run` | Запуск проекта или `app.cs` | `--project`, `-c Release`, `-- <args>` |
| `dotnet publish` | Публикация | `-c Release -r linux-x64 --self-contained`, `-p:PublishAot=true`, `-p:PublishTrimmed=true`, `/t:PublishContainer` |
| `dotnet pack` | NuGet-пакет | `-c Release --no-build -o artifacts` |
| `dotnet format` | Стиль и форматирование по `.editorconfig` | `--verify-no-changes` (раздел 3) |
| `dotnet package list` | Зависимости, уязвимые и устаревшие пакеты | `--vulnerable --include-transitive`, `--outdated`, `--deprecated`, `--format json` |
| `dotnet nuget why <pkg>` | Почему транзитивный пакет попал в граф | — |
| `dotnet completions script pwsh` | Нативное автодополнение (bash, zsh, fish, nushell, pwsh) | — |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [global.json overview](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) | Microsoft, 2026 | Все значения `rollForward`, `paths`, `test.runner` |
| [dotnet new templates](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates) | Microsoft, 2026 | Опции встроенных шаблонов, slnx по умолчанию |
| [File-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps) | Microsoft, 2025–2026 | Директивы `#:` и ограничения |
| [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support) | Microsoft, 2026 | LTS/STS и даты окончания поддержки |
| [Announcing .NET 11 RC 1](https://devblogs.microsoft.com/dotnet/dotnet-11-rc-1/) | .NET team, 2026 | Что приходит в ноябре |
| [.NET September 2026 servicing](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-september-2026-servicing-updates/) | .NET team, 2026 | Текущие патч-версии 10.0.12 / SDK 10.0.401 |
| [Customize your build by folder](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory) | Microsoft, 2026 | Как работают `Directory.Build.props/targets` |
| [MSBuild Structured Log Viewer](https://github.com/KirillOsenkov/MSBuildStructuredLog) | Kirill Osenkov, 2026 | Разбор `-bl` логов |

## 2. Компилятор и анализаторы

**Ключевые выводы**
- `<Nullable>enable</Nullable>` обязателен в новом коде. Предупреждения nullable (CS8600–CS8634 и др.) — это ошибки логики, а не стиль. В старом коде включайте по файлам через `#nullable enable` и подавляйте точечно.
- `TreatWarningsAsErrors=true` в CI, а не локально. Локально предупреждения не должны мешать экспериментам, а в CI любая новая warning ломает PR. Один нюанс: флаг делает ошибками и предупреждения NuGet Audit (NU1901–NU1904), поэтому свежий CVE в чужом пакете внезапно ломает сборку. Выведите их через `WarningsNotAsErrors` и проверяйте уязвимости отдельным шагом.
- `-warnaserror` в командной строке и `TreatWarningsAsErrors` в проекте одинаково действуют на предупреждения компилятора, но флаг CLI превращает в ошибки и NU1901–NU1904, не глядя на `WarningsNotAsErrors` (см. раздел 8). Поэтому в CI включайте `TreatWarningsAsErrors` условием на `$(CI)` в `Directory.Build.props`, а не флагом. `WarningsAsErrors` с перечнем кодов (`CS8602;CA2000`) превращает в ошибки только выбранные.
- Встроенные .NET-анализаторы (CAxxxx) включены по умолчанию, но в режиме `Default` почти все правила — только подсказки. Поднимайте уровень: `<AnalysisLevel>latest-recommended</AnalysisLevel>` для большинства проектов, `latest-all` для библиотек с гасением шума в `.editorconfig`. Чтобы обновление SDK не добавляло новых предупреждений, закрепите версию: `10.0-recommended`.
- Стиль (IDExxxx) в командной строке по умолчанию не проверяется. Нужны `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` и severity в `.editorconfig`. Правило IDE0005 (лишние using) на сборке работает только при `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. Для приложений добавьте `NoWarn` на CS1591, чтобы не требовать XML-комментарии везде.
- Сторонние анализаторы подключайте как `PackageReference` с `PrivateAssets="all"` в `Directory.Build.props`. Разумный стартовый набор — Meziantou.Analyzer и (для async-кода) Microsoft.VisualStudio.Threading.Analyzers. Roslynator и SonarAnalyzer добавляйте, если команда готова разбирать их шум. StyleCop.Analyzers фактически не развивается (стабильная 1.1.118 вышла в 2019, бета — в 2023), используйте его только ради правил документации и порядка членов.
- BannedApiAnalyzers запрещает конкретные API (`DateTime.Now`, `Thread.Sleep`, `Newtonsoft.Json`) через `BannedSymbols.txt`. PublicApiAnalyzers для библиотек фиксирует публичный API в `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`.
- Анализаторы замедляют сборку. В цикле правки агента можно временно передать `-p:RunAnalyzers=false`, но в CI они обязательны.

| Инструмент | Что и зачем | Установка | Ключевые настройки / правила | Оговорки |
|---|---|---|---|---|
| .NET analyzers (в SDK) | CAxxxx (качество, надёжность, производительность, безопасность) и IDExxxx (стиль) | встроено, `EnableNETAnalyzers` | `AnalysisLevel`, `AnalysisMode` (`None`, `Default`, `Minimum`, `Recommended`, `All`), `AnalysisMode<Category>`, `CodeAnalysisTreatWarningsAsErrors` | `latest-all` не включает CA1017, CA1045 и метрики CA15xx. В .NET 10 добавились CA2023 (шаблон сообщения логгера), CA2266 |
| Meziantou.Analyzer (3.0.294) | ~240 правил: `StringComparison`, `CancellationToken`, `ConfigureAwait`, производительность, безопасность | `dotnet package add Meziantou.Analyzer` | `.editorconfig`: `dotnet_diagnostic.MA0004.severity = none` и т.п. | Самый полезный сторонний набор на единицу шума |
| Roslynator.Analyzers (5.0.1) | 200+ анализаторов и рефакторингов, отдельный CLI | пакет; CLI `dotnet tool install roslynator.dotnet.cli` | `roslynator analyze`, `roslynator fix` | Много стилистических правил, решите заранее, какие включать |
| SonarAnalyzer.CSharp (10.35) | Баги, уязвимости, code smells (Sxxxx) | пакет | Работает и без сервера SonarQube | Шумный в `All`, начинайте с правил по умолчанию |
| StyleCop.Analyzers (1.1.118 / 1.2.0-beta.556) | Порядок членов, документация, оформление | пакет + `stylecop.json` | SA1600 и т.п. | Не развивается. Форматные правила конфликтуют с CSharpier |
| Microsoft.VisualStudio.Threading.Analyzers (18.7.23) | VSTHRD: sync-over-async, `async void`, забытые `Task` | пакет | VSTHRD002, VSTHRD100, VSTHRD110, VSTHRD200 | Часть правил рассчитана на приложения с UI-потоком |
| IDisposableAnalyzers (4.0.8) | Утечки `IDisposable` (IDISP001…) | пакет | — | Последний релиз в июне 2024. Частично дублирует CA2000 |
| Microsoft.CodeAnalysis.BannedApiAnalyzers (5.6.0) | Запрет API по списку (RS0030) | пакет + `<AdditionalFiles Include="BannedSymbols.txt" />` | `T:System.DateTime;Use TimeProvider` | — |
| Microsoft.CodeAnalysis.PublicApiAnalyzers | Контроль публичного API (RS0016, RS0017) | пакет + `PublicAPI.*.txt` | Code fix сам дописывает Unshipped | Для библиотек на NuGet |
| SecurityCodeScan | [устарело] Не поддерживается | — | — | Замена: CodeQL, SonarAnalyzer, правила CA3xxx/CA5xxx |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Code analysis in .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) | Microsoft, 2026 | `AnalysisMode`, `EnforceCodeStyleInBuild`, правила по умолчанию |
| [MSBuild properties for .NET SDK](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props) | Microsoft, 2026 | `AnalysisLevel`, `Nullable`, `GenerateDocumentationFile` и прочее |
| [Configuration options for code analysis](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options) | Microsoft, 2026 | Severity в `.editorconfig` и `.globalconfig` |
| [Code quality rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/) и [Code style rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/) | Microsoft, 2026 | Справочник CAxxxx и IDExxxx |
| [Compiler options: errors and warnings](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/errors-warnings) | Microsoft, 2026 | `TreatWarningsAsErrors`, `WarningsAsErrors`, `NoWarn`, `WarningLevel` |
| [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references) | Microsoft, 2026 | Миграция и атрибуты nullable-анализа |
| [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) | Gérald Barré, 2026 | Список правил MAxxxx |
| [Roslynator](https://github.com/dotnet/roslynator) | Josef Pihrt, .NET Foundation, 2026 | Анализаторы, рефакторинги, CLI |
| [sonar-dotnet](https://github.com/SonarSource/sonar-dotnet) | SonarSource, 2026 | Правила Sxxxx |
| [StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) | DotNetAnalyzers, 2023 | Если нужны SA-правила |
| [vs-threading analyzers](https://microsoft.github.io/vs-threading/analyzers/index.html) | Microsoft, 2026 | Правила VSTHRD |
| [IDisposableAnalyzers](https://github.com/DotNetAnalyzers/IDisposableAnalyzers) | DotNetAnalyzers, 2024 | Правила IDISP |
| [BannedApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers) и [PublicApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers) | Roslyn team, 2026 | Пакеты и формат файлов |

## 3. Форматирование

**Ключевые выводы**
- Вариант по умолчанию — `dotnet format` по правилам `.editorconfig`. Он встроен в SDK, умеет пробелы (`whitespace`), стиль IDE (`style`) и автоисправления анализаторов (`analyzers`). В CI запускайте `dotnet format --verify-no-changes`: при нужных правках он завершится с ненулевым кодом.
- `dotnet format` загружает решение через MSBuild и может долго работать на больших репозиториях. В CI ему помогает `--no-restore` после явного restore. Документация предупреждает: он компилирует проект и запускает анализаторы, поэтому чужой код им не проверяйте.
- CSharpier — opinionated-форматер в духе Prettier: почти без настроек, детерминированный, быстрый. Если команда выбирает его, `dotnet format whitespace` больше не запускайте (они спорят), а `dotnet format style` и `analyzers` оставьте.
- Агенту после правок полезно выполнять `dotnet format <проект>` (или `dotnet csharpier format .`), а не форматировать руками. Тогда дифф содержит только смысловые изменения.

| Инструмент | Что и зачем | Установка | Главные команды | Оговорки |
|---|---|---|---|---|
| dotnet format (в SDK) | Пробелы, стиль IDE, исправления анализаторов по `.editorconfig` | встроено | `dotnet format`; `dotnet format --verify-no-changes`; `dotnet format style --severity info`; `dotnet format analyzers --diagnostics CA1822`; `--include src/ --exclude src/Generated/` | По умолчанию чинит severity ≥ warn. `--framework` появится в .NET 11 |
| CSharpier (1.3.0) | Opinionated-форматер на Roslyn | `dotnet tool install csharpier` (локально) | `dotnet csharpier format .`; `dotnet csharpier check .` (CI); `.csharpierrc.json` (`printWidth`) | С версии 1.0 команды `format`/`check`. Старые `dotnet csharpier .` и `--check` [устарело]. Есть расширения для VS, Rider, VS Code |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) | Microsoft, 2026 | Все опции и подкоманды |
| [C# formatting options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/csharp-formatting-options) | Microsoft, 2026 | Ключи `csharp_*` для `.editorconfig` |
| [CSharpier](https://csharpier.com/) и [репозиторий](https://github.com/belav/csharpier) | Bela VanderVoort, 2026 | CLI, интеграции, pre-commit |

## 4. Тесты

**Ключевые выводы**
- В .NET есть два движка запуска тестов: старый VSTest и новый Microsoft.Testing.Platform (MTP 2.5.1). На MTP тестовый проект — это исполняемый файл, без внешнего хоста, быстрее и с расширениями (TRX, покрытие, ретраи) в виде NuGet-пакетов. Свежие major-версии фреймворков (xUnit v3 4.x, MSTest 4.x, TUnit) работают на MTP v2, и с .NET 10 SDK такие проекты запускаются только в режиме MTP.
- Режим MTP для `dotnet test` включается в `global.json`: `"test": { "runner": "Microsoft.Testing.Platform" }`. Тогда MTP должны использовать все тестовые проекты решения, смешивать с VSTest нельзя. Проект должен включить MTP сам: шаблон `dotnet new mstest` в SDK 10.0.401 создаёт проект на VSTest, и `dotnet test` в режиме MTP падает с кодом 1 («projects use VSTest»). Для MSTest добавьте `<EnableMSTestRunner>true</EnableMSTestRunner>` и `<OutputType>Exe</OutputType>` или берите `MSTest.Sdk` (проверено). Старое свойство `TestingPlatformDotnetTestSupport` [устарело].
- Синтаксис отличается. В режиме MTP проект и решение задаются `--project`/`--solution`, опции расширений (`--report-trx`, `--coverage`) идут как есть или после `--`, а фильтр зависит от фреймворка (`--filter-method`, `--treenode-filter` в TUnit, `--filter` в MSTest). Флаги VSTest (`--logger trx`, `--collect`) в MTP не работают. Неизвестная опция завершает прогон с кодом 5.
- `--minimum-expected-tests N` защищает от «зелёного» прогона, в котором из-за фильтра или ошибки обнаружения не запустилось ни одного теста (код 9). Код 8 означает, что тестов ноль.
- Выбор фреймворка. Новый проект без legacy — xUnit v3 или TUnit (source-generated discovery, параллельность по умолчанию, AOT). Корпоративный стандарт Microsoft — MSTest 4. NUnit 5 вышел 27 сентября 2026: если проект на NUnit 3/4, мигрируйте по release notes, а не на другой фреймворк.
- Assertion-библиотеки. FluentAssertions с версии 8 (январь 2025) платная для коммерческого использования (лицензия Xceed), бесплатная последняя — 7.2.x. Для коммерческого кода берите AwesomeAssertions (форк 7.x под Apache-2.0, почти тот же API) или Shouldly (BSD-3). Встроенный `Assert` фреймворка тоже нормальный выбор.
- Интеграционные тесты: `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) для HTTP, Testcontainers для реальных БД и брокеров, Verify для снапшотов.

| Инструмент | Что и зачем | Установка | Главные команды | Оговорки |
|---|---|---|---|---|
| Microsoft.Testing.Platform (2.5.1) | Движок запуска тестов: проект — exe, расширения через NuGet | приходит с фреймворком | `dotnet test`; `dotnet test --project tests/X.Tests`; `--list-tests`; `--minimum-expected-tests 1`; `--results-directory TestResults`; `dotnet run --project tests/X.Tests` | Нужен .NET 10 SDK и запись в `global.json`. TRX — пакет `Microsoft.Testing.Extensions.TrxReport` (`--report-trx`) |
| VSTest | Старый движок (`Microsoft.NET.Test.Sdk`) | пакет `Microsoft.NET.Test.Sdk` + адаптер | `dotnet test --filter "Category=Unit" --logger trx --collect:"XPlat Code Coverage"` | Для новых проектов на .NET 10 не выбирайте. Оставьте, пока существующие проекты не мигрированы |
| xUnit.net v3 (4.0.1) | Популярный фреймворк. 4.0: Native AOT тесты, режим параллельности `all`, без MTP v1 и Mono | `dotnet new xunit` или пакет `xunit.v3` | `[Fact]`, `[Theory]`, `TestContext.Current.CancellationToken` | v2 [устарело] для нового кода. Analyzers 2.0 не поддерживают VS 2019 |
| MSTest (4.5.1) | Фреймворк Microsoft, MTP по умолчанию в `MSTest.Sdk` | `dotnet new mstest --sdk --test-runner MSTest` | `[TestClass]`, `[TestMethod]`, `[DataRow]` | `MSTest.Sdk` подключает расширения профилем `--extensions-profile` |
| NUnit (5.0.0) | Классический фреймворк | `dotnet new nunit` | `[Test]`, `[TestCase]` | 5.0 (сентябрь 2026) ломает совместимость: `Assert.ThrowsAsync`/`CatchAsync`/`DoesNotThrowAsync` нужно `await`-ить, удалены `TestDelegate` и `ActualValueDelegate`, `[Platform("NET")]` теперь означает современный .NET, а не .NET Framework ([release notes](https://docs.nunit.org/articles/nunit/release-notes/framework.html)) |
| TUnit (1.73.0) | Source-generated discovery, параллельность по умолчанию, AOT, встроенные моки | `dotnet new install TUnit.Templates`, `dotnet new TUnit` | `[Test]`, `[Arguments]`, `[DependsOn]`, `[NotInParallel]`; фильтр `--treenode-filter` | Только MTP. Частые релизы, закрепляйте версию |
| AwesomeAssertions (9.6.0) | Fluent-ассерты, Apache-2.0 | `dotnet package add AwesomeAssertions` | `result.Should().Be(42)` | Пространство имён `AwesomeAssertions` |
| FluentAssertions (8.11.0) | Fluent-ассерты | пакет | — | С v8 коммерческое использование платное. 7.2.x бесплатна, но заморожена |
| Shouldly (4.3.0) | Ассерты с понятными сообщениями | пакет | `x.ShouldBe(1337)` | Релизы редкие (последний — январь 2025) |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [MTP overview](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro) | Microsoft, 2026 | Архитектура, расширения, коды выхода |
| [dotnet test with MTP](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp) | Microsoft, 2026 | Опции режима MTP, `--` и область действия аргументов |
| [Testing with dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test) | Microsoft, 2026 | Выбор между VSTest и MTP, смешанные решения |
| [xUnit.net](https://xunit.net/) | xUnit.net team, 2026 | Миграция на v3, release notes 4.0 |
| [MSTest overview](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-intro) | Microsoft, 2026 | MSTest.Sdk, профили расширений |
| [NUnit docs](https://docs.nunit.org/) | NUnit team, 2026 | Release notes 5.0 |
| [TUnit](https://tunit.dev/) | Tom Longhurst, 2026 | Source generators, фильтры, AOT |
| [AwesomeAssertions](https://awesomeassertions.org/) | AwesomeAssertions team, 2026 | Миграция с FluentAssertions |
| [FluentAssertions на NuGet](https://www.nuget.org/packages/FluentAssertions) | Xceed, 2026 | Условия лицензии v8 |
| [Shouldly](https://github.com/shouldly/shouldly) | Shouldly team, 2025 | API |
| [Testcontainers for .NET](https://dotnet.testcontainers.org/) | Testcontainers, 2026 | Интеграционные тесты с контейнерами |
| [Verify](https://github.com/VerifyTests/Verify) | Simon Cropp, 2026 | Снапшот-тесты |

## 5. Покрытие и мутационное тестирование

**Ключевые выводы**
- На MTP используйте `Microsoft.Testing.Extensions.CodeCoverage` (движок Microsoft) или `coverlet.MTP`. Классический `coverlet.collector` работает только с VSTest и с MTP v2 несовместим.
- Отчёт для людей и для CI собирает ReportGenerator: HTML, Cobertura, Markdown-сводка для `$GITHUB_STEP_SUMMARY`.
- Порог покрытия — сигнал, а не цель. 100% строк не доказывает, что тесты проверяют поведение. Это проверяет мутационное тестирование (Stryker.NET), и в PR его запускайте на диффе (`--since`), а полный прогон — по расписанию.

| Инструмент | Что и зачем | Установка | Главные команды | Оговорки |
|---|---|---|---|---|
| Microsoft.Testing.Extensions.CodeCoverage (18.12.0) | Покрытие для MTP | пакет в тестовый проект | `dotnet test --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml` | Настройки в `--coverage-settings coverage.config` (XML) |
| coverlet.MTP (10.1.0) | Coverlet для MTP | пакет | `dotnet test -- --coverlet --coverlet-output-format cobertura --coverlet-threshold 80` | Пакет относительно новый, сверяйте опции с README |
| coverlet.collector (10.1.0) | Coverlet для VSTest | пакет (есть в шаблоне `xunit`) | `dotnet test --collect:"XPlat Code Coverage"` | С MTP v2 не работает |
| dotnet-coverage | Сбор покрытия с любого процесса, слияние отчётов | `dotnet tool install dotnet-coverage` | `dotnet-coverage collect "dotnet test" -f cobertura -o coverage.xml`; `dotnet-coverage merge -o merged.xml -f cobertura **/*.coverage` | Удобен для слияния отчётов из нескольких модулей |
| ReportGenerator (5.5.11) | HTML и сводки из Cobertura, OpenCover, lcov | `dotnet tool install dotnet-reportgenerator-globaltool` | `reportgenerator -reports:"**/*.cobertura.xml" -targetdir:coverage -reporttypes:"HtmlInline;Cobertura;MarkdownSummaryGithub"` | В Actions есть готовый `danielpalme/ReportGenerator-GitHub-Action` |
| Stryker.NET (5.0.0) | Мутационное тестирование | `dotnet tool install dotnet-stryker` | `dotnet stryker`; `--since:main` (только изменённый код); `--reporter html --reporter progress`; `--break-at 60`; `stryker-config.json` | Полный прогон долгий. В 5.0 (сентябрь 2026) из ломающих изменений только два: инструменту нужен runtime .NET 10, а baseline на диске пишется по пути `--output`; добавлены покрытие `perTest` для MTP и опция `--diag` ([release](https://github.com/stryker-mutator/stryker-net/releases/tag/dotnet-stryker%405.0.0)) |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Microsoft.Testing.Extensions.CodeCoverage](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage) | Microsoft, 2026 | Пакет и ссылки на конфигурацию |
| [dotnet-coverage](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-coverage) | Microsoft, 2026 | Сбор и слияние |
| [coverlet](https://github.com/coverlet-coverage/coverlet) | coverlet-coverage, 2026 | collector, MTP, MSBuild-интеграция |
| [ReportGenerator](https://github.com/danielpalme/ReportGenerator) | Daniel Palme, 2026 | Форматы отчётов |
| [Stryker.NET docs](https://stryker-mutator.io/docs/stryker-net/introduction/) | Stryker team, 2026 | Конфигурация, `--since`, пороги |

## 6. Бенчмарки

**Ключевые выводы**
- Микробенчмарки пишите только на BenchmarkDotNet (0.15.8). Собственный `Stopwatch` в цикле не учитывает прогрев, JIT tiering, GC и шум.
- Запускайте всегда в Release и без отладчика: `dotnet run -c Release --project bench -- --filter "*"`. BDN откажется работать с Debug-сборкой.
- `[MemoryDiagnoser]` показывает аллокации на операцию. Для горячих путей это часто важнее времени.
- `[Benchmark(Baseline = true)]` даёт колонку Ratio внутри одного прогона. Для сравнения двух версий кода (до и после правки) сохраняйте полный JSON (`--exporters fulljson --artifacts <dir>`) и сравнивайте скриптом скилла [scripts/compare-bdn.cs](../scripts/compare-bdn.cs): `dotnet run compare-bdn.cs -- before after 5`. Он ставит вердикт `faster`/`slower`, только если медиана сдвинулась больше порога и 99,9% доверительные интервалы не пересекаются, и возвращает код 1 при регрессии.
- ResultsComparer из dotnet/performance вне этого репозитория собрать сложно: корневой `global.json` требует SDK .NET 11 RC, а `src/Directory.Build.props` — внутренний Arcade SDK (на SDK 10.0.401 сборка падает с MSB4236, проверено). Для своих проектов берите `compare-bdn.cs`.
- Если SDK стоит не в стандартном месте, BDN 0.15.8 падает с `NullReferenceException` в `DotNetSdkValidator`. Укажите путь явно: `-- --cli <путь к dotnet>` (проверено).
- В PR-CI запускайте бенчмарки только как смоук (`--job dry`), чтобы они компилировались и не падали. Время на общих раннерах шумит. Тренды собирайте по расписанию на выделенном раннере через github-action-benchmark или Bencher.

| Инструмент | Что и зачем | Установка | Главные команды | Оговорки |
|---|---|---|---|---|
| BenchmarkDotNet (0.15.8) | Статистические микробенчмарки, диагностеры памяти, сравнение рантаймов | `dotnet new console -n Bench`, `dotnet package add BenchmarkDotNet`, `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)` | `dotnet run -c Release -- --filter "*Parser*"`; `--list flat`; `--job short` / `--job dry`; `--memory`; `--runtimes net10.0 net11.0`; `--exporters json` (для `compare-bdn.cs` — `fulljson`); `--artifacts ./bdn` | Последний стабильный релиз — ноябрь 2025. `--runtimes` требует, чтобы проект мультитаргетился на эти TFM |
| compare-bdn.cs (скрипт скилла) | Сравнение двух каталогов результатов BDN с порогом, код выхода 1 при регрессии | лежит в `scripts/` скилла | `dotnet run compare-bdn.cs -- before after 5` | Нужен .NET 10 SDK (file-based apps) |
| ResultsComparer (dotnet/performance) | Сравнение двух каталогов результатов BDN с порогом | клон `dotnet/performance` | `dotnet run -- --base old/ --diff new/ --threshold 2%` | Внутренний инструмент: требует Arcade SDK и SDK из `global.json` репозитория, вне него не собирается |
| github-action-benchmark | Хранение истории и алерты на регрессии в GitHub Pages | action | `tool: 'benchmarkdotnet'`, `output-file-path: ...-report-full-compressed.json`, `alert-threshold: '150%'` | Нужен JSON-экспорт BDN: файл `<Класс>-report-full-compressed.json` в `BenchmarkDotNet.Artifacts/results/` создаёт `--exporters json` или `[JsonExporterAttribute.FullCompressed]` ([пример](https://github.com/benchmark-action/github-action-benchmark/tree/master/examples/benchmarkdotnet)) |
| Bencher | Continuous benchmarking с порогами и статистикой | CLI `bencher` | `bencher run --adapter c_sharp_dot_net ...` | Облако или self-hosted. Адаптер `c_sharp_dot_net` — для BenchmarkDotNet ([adapters](https://bencher.dev/docs/explanation/adapters/)) |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [BenchmarkDotNet docs](https://benchmarkdotnet.org/articles/overview.html) | .NET Foundation, 2025 | Jobs, диагностеры, CLI-аргументы |
| [BenchmarkDotNet repo](https://github.com/dotnet/BenchmarkDotNet) | .NET Foundation, 2026 | Релизы и issues |
| [dotnet/performance](https://github.com/dotnet/performance) | .NET team, 2026 | ResultsComparer, практики бенчмаркинга рантайма |
| [github-action-benchmark](https://github.com/benchmark-action/github-action-benchmark) | benchmark-action, 2026 | Настройка для BDN |
| [Bencher](https://bencher.dev/) | Bencher, 2026 | Пороги и CI-интеграция |
| [Writing high-performance .NET code](https://www.writinghighperf.net/) | Ben Watson, 2018 | Методология измерений, GC, JIT (по возможностям рантайма частично [устарело]) |

## 7. Диагностика и профилирование

**Ключевые выводы**
- Порядок такой: `dotnet-counters` для обзора (CPU, GC, аллокации, thread pool, исключения), затем `dotnet-trace` для CPU-профиля, `dotnet-gcdump` или `dotnet-dump` для памяти и зависаний.
- Все `dotnet-*` диагностические инструменты работают на Linux, macOS и Windows и подключаются к процессу по PID (`-p`) или имени (`-n`) через EventPipe. Отладчик и права root не нужны, если процесс запущен тем же пользователем.
- В контейнерах и Kubernetes используйте `dotnet-monitor` как sidecar: он отдаёт дампы, трассы и метрики по HTTP.
- На Windows для глубокого анализа CPU и GC — PerfView. Трассы `dotnet-trace` в формате `.nettrace` открываются в PerfView и Visual Studio, а с `--format speedscope` — в speedscope.app.
- `dotnet-dump analyze` с SOS-командами (`clrstack -all`, `dumpheap -stat`, `gcroot`) решает большинство вопросов «почему висит» и «кто держит память» без IDE.

| Инструмент | Что и зачем | Установка | Главные команды | Оговорки |
|---|---|---|---|---|
| dotnet-counters | Метрики рантайма в реальном времени | `dotnet tool install -g dotnet-counters` | `dotnet-counters ps`; `dotnet-counters monitor -n MyApp --counters System.Runtime,Microsoft.AspNetCore.Hosting`; `dotnet-counters collect --format csv` | Поддерживает и `System.Diagnostics.Metrics` (имена Meter) |
| dotnet-trace (10.0.745401) | CPU-сэмплинг и события EventPipe | `dotnet tool install -g dotnet-trace` | `dotnet-trace collect -p <PID>`; `--format speedscope`; `--duration 00:00:30`; `dotnet-trace collect -- dotnet bin/app.dll`; `dotnet-trace convert` | В 10.x профиль `cpu-sampling` удалён; без `--profile` включаются `dotnet-common` и `dotnet-sampled-thread-time`, список — `dotnet-trace list-profiles`. На Linux есть `collect-linux` (preview, perf_events, glibc ≥ 2.27) ([docs](https://github.com/dotnet/docs/blob/main/docs/core/diagnostics/dotnet-trace.md)) |
| dotnet-dump | Полный дамп и анализ SOS | `dotnet tool install -g dotnet-dump` | `dotnet-dump collect -p <PID>`; `dotnet-dump analyze core_x` → `clrstack -all`, `dumpheap -stat`, `gcroot <addr>`, `threadpool`, `syncblk` | Дампы большие и содержат секреты из памяти |
| dotnet-gcdump | Лёгкий снимок управляемой кучи | `dotnet tool install -g dotnet-gcdump` | `dotnet-gcdump collect -p <PID>`; `dotnet-gcdump report file.gcdump` | Вызывает полный GC. Открывается в VS и PerfView |
| dotnet-stack | Стеки всех потоков без дампа | `dotnet tool install -g dotnet-stack` | `dotnet-stack report -p <PID>` | Быстрый ответ на «где висит» |
| dotnet-monitor (10.0.4) | HTTP API для дампов, трасс, метрик; sidecar | `dotnet tool install -g dotnet-monitor` или образ `mcr.microsoft.com/dotnet/monitor` | `dotnet monitor collect`; `GET /processes`, `/dump`, `/trace`, `/metrics` | `--no-auth` только локально |
| PerfView | CPU, GC, аллокации, ETW на Windows | скачать exe с GitHub | `PerfView collect`; открыть `.nettrace` / `.etl` | Только Windows, интерфейс суровый |
| Visual Studio / Rider profilers, dotTrace, dotMemory | Профилирование в IDE | вместе с IDE | — | dotTrace и dotMemory платные (JetBrains) |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [.NET diagnostic tools overview](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/tools-overview) | Microsoft, 2026 | Карта инструментов |
| [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters) | Microsoft, 2026 | Счётчики и форматы вывода |
| [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace) | Microsoft, 2026 | Профили и провайдеры |
| [dotnet-dump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump) | Microsoft, 2026 | SOS-команды |
| [dotnet-gcdump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump) | Microsoft, 2026 | Анализ кучи |
| [dotnet-monitor](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-monitor) | Microsoft, 2026 | Sidecar, egress, правила сбора |
| [Debug a memory leak](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-memory-leak) | Microsoft, 2026 | Пошаговый сценарий с dotnet-dump |
| [PerfView](https://github.com/microsoft/perfview) | Microsoft (Vance Morrison), 2026 | Скачивание и туториалы |

## 8. Зависимости и supply chain

**Ключевые выводы**
- NuGet Audit встроен в restore. В .NET 10 SDK для проектов на `net10.0` по умолчанию проверяются и транзитивные пакеты (`NuGetAuditMode=all`, раньше было `direct`). Предупреждения NU1901–NU1904 соответствуют severity low–critical, NU1900 — ошибке связи с источником.
- Рекомендуемая схема: в обычных сборках уязвимости остаются предупреждениями (`WarningsNotAsErrors` для NU1901–NU1904), а отдельный шаг CI падает на high/critical. Документация предлагает для этого паттерн с условным свойством (`-p:AuditPipeline=true`).
- `dotnet package list --vulnerable --include-transitive` показывает уязвимые пакеты, но код выхода не зависит от результата: на SDK 10.0.401 он равен 0 и при найденной уязвимости (проверено). Текстовый вывод локализован, поэтому `grep` по английской фразе на машине с другим языком интерфейса не сработает. В CI берите `--format json` и ищите поле `"severity"` или задайте `DOTNET_CLI_UI_LANGUAGE=en`.
- `-warnaserror` в командной строке превращает NU1901–NU1904 в ошибки сборки даже при `WarningsNotAsErrors` в проекте (на SDK 10.0.401 с уязвимым пакетом сборка упала с NU1903). Если уязвимости должны ловиться только отдельным шагом, используйте `TreatWarningsAsErrors` в `Directory.Build.props` или `dotnet build -p:TreatWarningsAsErrors=true` вместо флага: свойство учитывает `WarningsNotAsErrors` (проверено на SDK 10.0.401, предупреждения CS стали ошибками, NU1903 остался предупреждением).
- Чинить уязвимости: `dotnet package update --vulnerable` (.NET 10) поднимает пакеты до первой безопасной версии. Путь транзитивной зависимости показывает `dotnet nuget why <пакет>`. Подавление конкретного advisory — `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-..." />`, только с комментарием, почему оно безопасно.
- Central Package Management (`Directory.Packages.props`, `ManagePackageVersionsCentrally`) держит версии в одном месте. `CentralPackageTransitivePinningEnabled` позволяет закрепить исправленную версию транзитивного пакета.
- Lock-файлы (`RestorePackagesWithLockFile`) плюс `dotnet restore --locked-mode` в CI гарантируют, что граф не изменится незаметно. Кеш NuGet в `actions/setup-dotnet` без lock-файла не работает.
- Package Source Mapping в `nuget.config` закрепляет, из какого фида может прийти каждый префикс пакетов. Это главная защита от dependency confusion при нескольких фидах.
- Обновления автоматизируйте Dependabot (экосистемы `nuget` и `dotnet-sdk` для `global.json`) или Renovate (понимает CPM, `global.json`, `dotnet-tools.json`).
- SBOM генерируйте `sbom-tool` (SPDX) или CycloneDX. Подписи пакетов проверяйте через `signatureValidationMode=require` в `nuget.config` в окружениях с повышенными требованиями.

| Инструмент | Что и зачем | Установка / конфиг | Главные команды | Оговорки |
|---|---|---|---|---|
| NuGet Audit | Проверка уязвимостей при restore по GitHub Advisory Database | встроено; `NuGetAudit`, `NuGetAuditMode` (`direct`/`all`), `NuGetAuditLevel` (`low`…`critical`) | `dotnet restore`; коды NU1900–NU1905; `auditSources` в `nuget.config` | Если корпоративный фид не отдаёт данные об уязвимостях, добавьте audit source `https://api.nuget.org/v3/index.json` |
| `dotnet package list` | Уязвимые, устаревшие и deprecated пакеты | встроено | `dotnet package list --vulnerable --include-transitive`; `--outdated --highest-minor`; `--deprecated`; `--format json` | В .NET 10 сам делает restore. На SDK 9 — `dotnet list package` |
| `dotnet package update` | Обновление пакетов (.NET 10) | встроено | `dotnet package update --vulnerable`; `dotnet package update <id>` | Проверяйте дифф `Directory.Packages.props` |
| Lock-файлы | Воспроизводимый граф | `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` | `dotnet restore --locked-mode`; `--force-evaluate` для обновления | Разные feature band SDK меняют pruning, поэтому закрепите SDK |
| Package Source Mapping | Какой фид для какого префикса | `<packageSourceMapping>` в `nuget.config` | — | Требует явного маппинга для всех пакетов |
| Dependabot / Renovate | PR с обновлениями | `.github/dependabot.yml` (`package-ecosystem: "nuget"`, `"dotnet-sdk"`); `renovate.json` | — | Группируйте обновления (`groups`), иначе будет поток PR |
| sbom-tool (Microsoft.Sbom.DotNetTool 4.1.13) | SBOM в SPDX 2.2/3.0 | `dotnet tool install Microsoft.Sbom.DotNetTool` | `sbom-tool generate -b ./artifacts -bc . -pn MyApp -pv 1.2.0 -ps "My Org" -nsb https://example.com` | Есть MSBuild-интеграция: пакет `Microsoft.Sbom.Targets` (4.1.13) с `<GenerateSBOM>true</GenerateSBOM>` создаёт SBOM при `dotnet pack` |
| CycloneDX for .NET | SBOM в CycloneDX | `dotnet tool install CycloneDX` | `dotnet CycloneDX MySolution.slnx -o sbom` | — |
| `dotnet nuget sign` / `verify` | Подпись и проверка пакетов | встроено | `dotnet nuget verify --all ./packages/*.nupkg` | Подпись требует сертификата code signing |
| CodeQL | SAST для C# | `github/codeql-action` | `languages: csharp`, `build-mode: none` | Бесплатно для публичных репозиториев |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Auditing package dependencies](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages) | NuGet team, 2026 | Свойства аудита, коды, схема для CI |
| [dotnet package list](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list) | Microsoft, 2026 | Опции `--vulnerable`, `--outdated` |
| [dotnet nuget why](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-why) | Microsoft, 2026 | Поиск пути транзитивной зависимости |
| [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) | NuGet team, 2026 | `Directory.Packages.props`, transitive pinning |
| [Lock files](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies) | NuGet team, 2026 | `--locked-mode` |
| [Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping) | NuGet team, 2026 | Защита от подмены пакетов |
| [NuGet security best practices](https://learn.microsoft.com/en-us/nuget/concepts/security-best-practices) | NuGet team, 2026 | Фиды, подписи, аудит |
| [Dependabot options reference](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/dependabot-options-reference) | GitHub, 2026 | Экосистемы и группировка |
| [Renovate: NuGet manager](https://docs.renovatebot.com/modules/manager/nuget/) | Mend, 2026 | CPM и `global.json` |
| [sbom-tool](https://github.com/microsoft/sbom-tool) | Microsoft, 2026 | Генерация SPDX |
| [cyclonedx-dotnet](https://github.com/CycloneDX/cyclonedx-dotnet) | CycloneDX, 2026 | SBOM в CycloneDX |
| [CodeQL Action](https://github.com/github/codeql-action) | GitHub, 2026 | SAST в Actions |

## 9. Упаковка и релиз

**Ключевые выводы**
- Source Link встроен в SDK начиная с .NET 8 для GitHub, GitLab, Azure Repos и Bitbucket. Отдельный пакет `Microsoft.SourceLink.*` для них не нужен [устарело].
- В CI ставьте `ContinuousIntegrationBuild=true`: пути в PDB нормализуются, и сборка становится воспроизводимой. `Deterministic` включён по умолчанию. Пакет `DotNet.ReproducibleBuilds` выставит всё это одним `PackageReference`.
- Для библиотек: `GenerateDocumentationFile`, `IncludeSymbols` + `SymbolPackageFormat=snupkg`, `PackageReadmeFile`, `PackageLicenseExpression`, `EnablePackageValidation`. Package Validation с `PackageValidationBaselineVersion` сравнит API с опубликованной версией и поймает ломающие изменения (аналог cargo-semver-checks).
- Версию берите из git-тегов: MinVer (минимализм, только теги) или Nerdbank.GitVersioning (`version.json`, высота коммита, CLI `nbgv`). Вручную версию в `.csproj` не правьте.
- Публикуйте в nuget.org через Trusted Publishing (OIDC из GitHub Actions) вместо долгоживущего API-ключа. На nuget.org заводится политика (владелец, репозиторий, имя файла workflow, опционально environment), в job — `permissions: id-token: write`, шаг `NuGet/login@v1` с `user: <профиль nuget.org>` выдаёт временный ключ на 1 час для `dotnet nuget push --api-key ${{ steps.login.outputs.NUGET_API_KEY }}`. Поддерживается и GitLab ([Trusted Publishing on nuget.org](https://github.com/NuGet/docs.microsoft.com-nuget/blob/main/docs/nuget-org/trusted-publishing.md)).

| Инструмент | Что и зачем | Установка | Главные команды / свойства | Оговорки |
|---|---|---|---|---|
| `dotnet pack` | Сборка `.nupkg`/`.snupkg` | встроено | `dotnet pack -c Release --no-build -o artifacts` | Метаданные в `Directory.Build.props` |
| Package Validation / ApiCompat | Совместимость между TFM и с прошлой версией | `<EnablePackageValidation>true</EnablePackageValidation>`; tool `Microsoft.DotNet.ApiCompat.Tool` | `<PackageValidationBaselineVersion>1.4.0</PackageValidationBaselineVersion>`; `apicompat package new.nupkg --baseline-package old.nupkg` | Ошибки CP0xxx подавляются через `CompatibilitySuppressions.xml` |
| MinVer (8.0.0) | Версия из git-тегов | `dotnet package add MinVer` | тег `v1.2.3`; `MinVerTagPrefix=v` | В CI нужен `fetch-depth: 0` |
| Nerdbank.GitVersioning (3.10.94) | Версия из `version.json` и высоты коммита | `dotnet tool install nbgv`; `nbgv install` | `nbgv get-version`; `nbgv prepare-release` | Нужна полная история git |
| DotNet.ReproducibleBuilds | Source Link, `ContinuousIntegrationBuild`, embed sources | пакет с `PrivateAssets="all"` | — | Отдельный пакет `Isolated` для SDK — опционально |
| `dotnet nuget push` | Публикация | встроено | `dotnet nuget push artifacts/*.nupkg -s https://api.nuget.org/v3/index.json -k $KEY --skip-duplicate` | Ключ — только из секретов CI |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Source Link](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink) | Microsoft, 2026 | Настройка для библиотек |
| [dotnet/sourcelink](https://github.com/dotnet/sourcelink) | .NET team, 2026 | Провайдеры и свойства |
| [Package validation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview) | Microsoft, 2026 | Baseline и правила CP |
| [Library guidance](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/) | Microsoft, 2026 | Версионирование, strong naming, зависимости |
| [dotnet/reproducible-builds](https://github.com/dotnet/reproducible-builds) | .NET Foundation, 2026 | Пакет DotNet.ReproducibleBuilds |
| [MinVer](https://github.com/adamralph/minver) | Adam Ralph, 2026 | Теги и пре-релизы |
| [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) | Andrew Arnott, 2026 | `version.json`, `nbgv` |

## 10. IDE, LSP и EF Core tooling

**Ключевые выводы**
- Основные IDE: Visual Studio 2026 (Windows), JetBrains Rider (кросс-платформенно, бесплатен для некоммерческого использования), VS Code с расширением C# (open source, Roslyn LSP) и C# Dev Kit (лицензия как у VS Community: коммерческим командам нужна подписка VS).
- Агенту, работающему вне IDE, нужен LSP-сервер. Варианты: `roslyn-language-server` — тот же сервер, что в расширении C# для VS Code (пока prerelease-tool), и `csharp-ls` (0.28.0, на Roslyn, проще настраивается). Оба требуют .NET 10. OmniSharp [устарело]: расширение VS Code ушло с него на Roslyn LSP в 2023.
- Если LSP недоступен, источник истины — `dotnet build`: ошибки и предупреждения выводятся в формате `file(line,col): error CSxxxx`, который легко разобрать.
- EF Core: `dotnet-ef` ставьте локальным tool той же major-версии, что и EF Core в проекте. В CI проверяйте `dotnet ef migrations has-pending-model-changes`: шаг падает, если модель изменили, а миграцию забыли.

| Инструмент | Что и зачем | Установка | Главные команды | Оговорки |
|---|---|---|---|---|
| roslyn-language-server (5.12.0-1.x, prerelease) | Официальный Roslyn LSP для C# и Razor | `dotnet tool install -g roslyn-language-server --prerelease` | запуск редактором по stdio | Интерфейс запуска может меняться между версиями |
| csharp-ls (0.28.0) | Лёгкий LSP на Roslyn | `dotnet tool install -g csharp-ls` | `csharp-ls --solution MySolution.slnx` | Меньше возможностей, чем у Roslyn LSP |
| C# Dev Kit | Solution Explorer, тесты, отладка в VS Code | Marketplace | — | Лицензия VS Community |
| ilspycmd (11.1.0) | Декомпиляция сборок и NuGet-пакетов без исходников: посмотреть, что на самом деле делает зависимость, во что компилятор развернул `async`/`yield`/records | `dotnet tool install -g ilspycmd` | `ilspycmd Lib.dll -t Ns.Type`; `-il` (IL); `-p -o out/` (компилируемый проект); `-l c` (список классов) | Для одного фрагмента кода быстрее [SharpLab](https://sharplab.io/); лицензия ILSpy — MIT ([README](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.ILSpyCmd/README.md)) |
| dotnet-ef (10.0.12) | Миграции, скрипты, бандлы, scaffold | `dotnet tool install dotnet-ef` (локально) + пакет `Microsoft.EntityFrameworkCore.Design` | `dotnet ef migrations add Init`; `dotnet ef database update`; `dotnet ef migrations script --idempotent -o migrate.sql`; `dotnet ef migrations bundle --self-contained -r linux-x64`; `dotnet ef dbcontext scaffold "<conn>" Npgsql.EntityFrameworkCore.PostgreSQL`; `dotnet ef migrations has-pending-model-changes` | В продакшен применяйте SQL-скрипт или бандл, а не `database update` из приложения |

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [C# in VS Code](https://code.visualstudio.com/docs/csharp/get-started) | Microsoft, 2026 | Расширение C#, Dev Kit, лицензия |
| [Rider](https://www.jetbrains.com/rider/) | JetBrains, 2026 | Лицензии и возможности |
| [roslyn-language-server](https://www.nuget.org/packages/roslyn-language-server) | Roslyn team, 2026 | Пакет LSP-сервера |
| [csharp-language-server](https://github.com/razzmatazz/csharp-language-server) | Saulius Menkevičius, 2026 | Настройка csharp-ls в редакторах |
| [EF Core CLI reference](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) | Microsoft, 2026 | Все команды `dotnet ef` |
| [Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) | Microsoft, 2026 | Скрипты, бандлы, почему не мигрировать при старте |

## 11. Минимальный набор и инструменты по ситуации

**Ключевые выводы**
- Минимум для любого проекта: `global.json`, `Directory.Build.props` со строгими настройками, `.editorconfig`, `Directory.Packages.props`, `dotnet format`, тесты на MTP, NuGet Audit с отдельным гейтом. Всё это бесплатно и встроено в SDK.
- Для библиотек добавляются Package Validation, PublicApiAnalyzers, Source Link и `GenerateDocumentationFile`.
- Остальное подключайте под конкретную проблему: каждый анализатор и каждый шаг CI стоят времени на каждом PR.

| Ситуация | Инструменты |
|---|---|
| Всегда | `global.json`, `Directory.Build.props` (Nullable, `AnalysisLevel`, `EnforceCodeStyleInBuild`, warnings as errors в CI), `.editorconfig`, CPM, `dotnet format`, xUnit v3 / MSTest / TUnit на MTP, NuGet Audit, Meziantou.Analyzer |
| Библиотека на NuGet | + Package Validation, PublicApiAnalyzers, Source Link, MinVer или NBGV, `snupkg` |
| Async-нагруженный код | + Microsoft.VisualStudio.Threading.Analyzers, правила CA2007/CA2016, Meziantou MA0004/MA0040 |
| Веб-сервис | + `WebApplicationFactory`, Testcontainers, dotnet-counters, dotnet-monitor в контейнерах |
| Работа с БД через EF Core | + dotnet-ef, `has-pending-model-changes` в CI, идемпотентные скрипты миграций |
| Качество тестов | + покрытие (Microsoft CodeCoverage или coverlet.MTP), ReportGenerator, Stryker.NET по диффу |
| Производительность | + BenchmarkDotNet, `[MemoryDiagnoser]`, dotnet-trace, dotnet-gcdump, PerfView |
| Зависания и утечки в проде | + dotnet-dump, dotnet-stack, dotnet-monitor |
| Повышенные требования к supply chain | + lock-файлы, Package Source Mapping, SBOM, CodeQL, проверка подписей |
| Командные запреты API | + BannedApiAnalyzers |

## 12. Порядок CI-пайплайна и workflow

**Ключевые выводы**
- Шаги идут от дешёвых к дорогим: restore, формат, сборка с warnings as errors, тесты, уязвимости, упаковка. Покрытие собирайте в том же прогоне тестов, а мутации и бенчмарки выносите в отдельные job'ы по расписанию.
- Restore выполняется один раз, дальше — `--no-restore` и `--no-build`. Так шаги не делают лишнюю работу и не расходятся в настройках.
- Матрица ОС нужна только для тестов. Формат, анализ уязвимостей и упаковку достаточно выполнять на одной ОС (Linux дешевле и быстрее).
- Версии actions (`checkout@v7`, `setup-dotnet@v6` по README на октябрь 2026) сверяйте с их README в момент настройки и закрепляйте по SHA в чувствительных репозиториях.

Порядок шагов:
1. `dotnet restore --locked-mode` (или просто `dotnet restore` без lock-файлов)
2. `dotnet format --verify-no-changes --no-restore` (или `dotnet csharpier check .` + `dotnet format style --verify-no-changes`)
3. `dotnet build -c Release --no-restore` (предупреждения становятся ошибками через `TreatWarningsAsErrors` при `CI=true` из `Directory.Build.props`; флаг `-warnaserror` не добавляйте, он ломает сборку на NU1901–NU1904)
4. `dotnet test -c Release --no-build --minimum-expected-tests 1 --results-directory TestResults --report-trx --coverage --coverage-output-format cobertura` (матрица ОС; на VSTest: `--logger trx --collect:"XPlat Code Coverage"`)
5. Уязвимости: `dotnet package list --vulnerable --include-transitive --format json`, шаг падает, если в JSON есть `"severity"` (или restore с `-p:AuditPipeline=true`)
6. Для EF Core: `dotnet ef migrations has-pending-model-changes`
7. Для библиотек: `dotnet pack -c Release --no-build -o artifacts` (с Package Validation)
8. Отчёт покрытия: `reportgenerator ... -reporttypes:"MarkdownSummaryGithub"` в summary
9. По расписанию: `dotnet stryker --since:main`, бенчмарки, `dotnet package list --outdated`, CodeQL

```yaml
# .github/workflows/ci.yml — ключевые шаги
name: ci
on: [push, pull_request]
permissions: { contents: read }
env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
jobs:
  build:
    strategy:
      matrix: { os: [ubuntu-latest, windows-latest] }
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v7
        with: { fetch-depth: 0 }                  # нужно MinVer/NBGV
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
          cache: true                             # требует packages.lock.json
          cache-dependency-path: '**/packages.lock.json'
      - run: dotnet tool restore
      - run: dotnet restore --locked-mode
      - run: dotnet format --verify-no-changes --no-restore
        if: matrix.os == 'ubuntu-latest'
      - run: dotnet build -c Release --no-restore   # TreatWarningsAsErrors включает Directory.Build.props при CI=true
      - run: >
          dotnet test -c Release --no-build --minimum-expected-tests 1
          --results-directory TestResults --report-trx
          --coverage --coverage-output-format cobertura
      - name: Vulnerable packages
        if: matrix.os == 'ubuntu-latest'
        shell: bash
        run: |
          dotnet package list --vulnerable --include-transitive --no-restore --format json > vuln.json
          cat vuln.json
          ! grep -q '"severity"' vuln.json
      - name: Coverage summary
        if: matrix.os == 'ubuntu-latest'
        run: |
          dotnet reportgenerator -reports:"TestResults/**/*.cobertura.xml" -targetdir:coverage -reporttypes:"MarkdownSummaryGithub"
          cat coverage/SummaryGithub.md >> $GITHUB_STEP_SUMMARY
      - uses: actions/upload-artifact@v7
        if: always()
        with: { name: test-results-${{ matrix.os }}, path: TestResults }
      - run: dotnet pack -c Release --no-build -o artifacts   # только для библиотек
        if: matrix.os == 'ubuntu-latest'
```

Оговорки к workflow. `--report-trx` и `--coverage` требуют пакетов `Microsoft.Testing.Extensions.TrxReport` и `Microsoft.Testing.Extensions.CodeCoverage` во всех тестовых проектах (иначе код выхода 5). `dotnet reportgenerator` работает, если ReportGenerator поставлен в локальный манифест. Последний релиз `actions/upload-artifact` — v7.0.2 (октябрь 2026), v6+ работает на Node.js 24 и требует runner не ниже 2.327.1 ([releases](https://github.com/actions/upload-artifact/releases)).

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [actions/setup-dotnet](https://github.com/actions/setup-dotnet) | GitHub, 2026 | Входы, кеш, `global-json-file` |
| [actions/checkout](https://github.com/actions/checkout) | GitHub, 2026 | Изменения v7 для fork PR |
| [Building and testing .NET in GitHub Actions](https://docs.github.com/en/actions/use-cases-and-examples/building-and-testing/building-and-testing-net) | GitHub, 2026 | Базовый рецепт |
| [dorny/test-reporter](https://github.com/dorny/test-reporter) | Michal Dorner, 2026 | Отчёт TRX в PR |
| [ReportGenerator GitHub Action](https://github.com/danielpalme/ReportGenerator-GitHub-Action) | Daniel Palme, 2026 | Покрытие в summary |

## 13. Шаблоны: Directory.Build.props, .editorconfig, global.json

**Ключевые выводы**
- `Directory.Build.props` в корне применяется ко всем проектам ниже по дереву. Тестовым проектам можно ослабить правила вложенным `Directory.Build.props` в `tests/` с `<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />`.
- Свойство `CI` выставляют GitHub Actions, Azure Pipelines (как `TF_BUILD`) и большинство CI. MSBuild читает переменные окружения как свойства, поэтому условие `'$(CI)' == 'true'` работает без дополнительных флагов.
- Шумные правила гасите в `.editorconfig` с комментарием, почему. `#pragma warning disable` в коде оставляйте для единичных мест с обоснованием.

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- LangVersion не задаём: по умолчанию берётся версия для TFM (C# 14 для net10.0) -->

    <!-- Анализ -->
    <AnalysisLevel>latest-recommended</AnalysisLevel>   <!-- закрепите "10.0-recommended", если не хотите новых правил при обновлении SDK -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile> <!-- нужен для IDE0005 на сборке -->
    <NoWarn>$(NoWarn);CS1591</NoWarn>                   <!-- для библиотек уберите: требуйте XML-доки -->

    <!-- Предупреждения как ошибки только в CI; уязвимости NuGet проверяются отдельным шагом -->
    <TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>

    <!-- Воспроизводимость -->
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>

    <!-- Аудит зависимостей -->
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <NuGetAuditLevel>low</NuGetAuditLevel>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Meziantou.Analyzer" PrivateAssets="all" />
    <!-- По ситуации: -->
    <!-- <PackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" PrivateAssets="all" /> -->
    <!-- <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" /> -->
    <!-- <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" /> -->
  </ItemGroup>
</Project>
```

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Meziantou.Analyzer" Version="3.0.294" />
    <PackageVersion Include="xunit.v3" Version="4.0.1" />
    <PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.12.0" />
    <PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="2.5.1" />
    <PackageVersion Include="AwesomeAssertions" Version="9.6.0" />
  </ItemGroup>
</Project>
```

```json
// global.json
{
  "sdk": {
    "version": "10.0.401",
    "rollForward": "latestFeature",   // с lock-файлами безопаснее "disable" или "latestPatch"
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

```ini
# .editorconfig (скелет; полный дефолт даёт `dotnet new editorconfig`)
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{json,yml,yaml,xml,csproj,props,targets,slnx}]
indent_size = 2

[*.cs]
# Стиль, проверяемый на сборке (EnforceCodeStyleInBuild)
csharp_style_namespace_declarations = file_scoped:warning
csharp_prefer_braces = true:warning
dotnet_style_require_accessibility_modifiers = for_non_interface_members:warning
csharp_style_var_for_built_in_types = true:suggestion
dotnet_sort_system_directives_first = true
dotnet_diagnostic.IDE0005.severity = warning     # лишние using
dotnet_diagnostic.IDE0161.severity = warning     # file-scoped namespace

# Качество и надёжность
dotnet_diagnostic.CA2000.severity = warning      # Dispose до выхода из области видимости
dotnet_diagnostic.CA2016.severity = warning      # передавать CancellationToken
dotnet_diagnostic.CA1848.severity = suggestion   # LoggerMessage — для горячих путей
dotnet_diagnostic.CA2007.severity = none         # ConfigureAwait: включайте в библиотеках, не в приложениях
dotnet_diagnostic.CA1062.severity = none         # при включённом Nullable дублирует проверки

[tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none         # подчёркивания в именах тестов
dotnet_diagnostic.CA2007.severity = none
dotnet_diagnostic.CS1591.severity = none

[**/{Migrations,Generated}/**.cs]
generated_code = true
```

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [EditorConfig для .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files) | Microsoft, 2026 | `.editorconfig` и `.globalconfig`, приоритеты |
| [Language rules option format](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/language-rules) | Microsoft, 2026 | Синтаксис `option = value:severity` |
| [Artifacts output layout](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output) | Microsoft, 2026 | `UseArtifactsOutput`, единый каталог артефактов |
| [Suppress code analysis warnings](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/suppress-warnings) | Microsoft, 2026 | Способы подавления с обоснованием |

## 14. Ресурсы

| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Download .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) | Microsoft, 2026 | Актуальные SDK и runtime |
| [What's new in .NET 10 SDK](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk) | Microsoft, 2026 | `dnx`, file-based apps, MTP, slnx |
| [.NET CLI overview](https://learn.microsoft.com/en-us/dotnet/core/tools/) | Microsoft, 2026 | Справочник всех команд |
| [.NET 11 RC1 (InfoQ)](https://www.infoq.com/news/2026/09/dotnet-11-rc-1-release/) | InfoQ, 2026 | Обзор нововведений .NET 11 |
| [Code analysis in .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) | Microsoft, 2026 | Анализаторы и уровни |
| [dotnet test with MTP](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp) | Microsoft, 2026 | Новый `dotnet test` |
| [Auditing packages](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages) | NuGet team, 2026 | Уязвимости зависимостей |
| [.NET diagnostic tools](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/tools-overview) | Microsoft, 2026 | Диагностика в проде |
| [BenchmarkDotNet](https://benchmarkdotnet.org/) | .NET Foundation, 2025 | Микробенчмарки |
| [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) | Stryker team, 2026 | Мутационное тестирование |
| [Andrew Lock: .NET Escapades](https://andrewlock.net/) | Andrew Lock, обновляется | Глубокие разборы SDK, MSBuild, тестов и нововведений каждого релиза |
| [Meziantou's blog](https://www.meziantou.net/) | Gérald Barré, обновляется | Практики настройки сборки, анализаторов и CI в .NET |

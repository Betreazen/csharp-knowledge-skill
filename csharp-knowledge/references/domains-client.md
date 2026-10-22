# Клиентские и прочие несерверные домены C#

Справочник по областям применения C#/.NET за пределами веб-бэкенда (актуально на октябрь 2026: .NET 10 LTS — текущая стабильная платформа, .NET 11 STS с C# 15 — в RC1 с 8 сентября 2026, релиз запланирован на 10 ноября 2026; текущий язык — C# 14; поддержка .NET 8 и 9 заканчивается 10 ноября 2026). Для каждого домена — архитектурные и производительные правила, пакеты (в скобках — актуальная версия на октябрь 2026 по NuGet) и проверенные ресурсы. Пометка `[устарело]` — ресурс или пакет больше не развивается; `[не проверено]` — факт не удалось подтвердить первоисточником. Общие идиомы, тулчейн и серверные домены описаны в соседних файлах справочника.

- [1. Desktop GUI](#1-desktop-gui)
- [2. Мобильная разработка](#2-мобильная-разработка)
- [3. CLI-утилиты и скрипты](#3-cli-утилиты-и-скрипты)
- [4. Библиотеки и NuGet-пакеты](#4-библиотеки-и-nuget-пакеты)
- [5. Разработка игр](#5-разработка-игр)
- [6. AI/ML и численные методы](#6-aiml-и-численные-методы)
- [7. IoT и встраиваемые устройства](#7-iot-и-встраиваемые-устройства)
- [8. Interop с другими экосистемами](#8-interop-с-другими-экосистемами)
- [9. Инструменты разработчика: Roslyn и MSBuild](#9-инструменты-разработчика-roslyn-и-msbuild)

---

## 1. Desktop GUI

### Ключевые выводы
- Выбор фреймворка по задаче:
  - Только Windows, существующее приложение или LOB-софт с богатой экосистемой контролов — **WPF**.
  - Простые внутренние формы и быстрые утилиты — **WinForms**.
  - Только Windows, новый проект с современным UI Windows 11 — **WinUI 3** (Windows App SDK).
  - Кроссплатформенный десктоп (Windows/macOS/Linux) с единым отрисованным UI — **Avalonia** или **Uno Platform**.
  - Приоритет — мобильные устройства, десктоп вторичен — **.NET MAUI** (см. раздел 2).
- WPF и WinForms живы и развиваются вместе с каждым релизом .NET. В .NET 10 у WinForms тёмная тема (`Application.SetColorMode(SystemColorMode.Dark)`) и асинхронные формы (`Form.ShowAsync`, `ShowDialogAsync`, `TaskDialog.ShowDialogAsync`) вышли из экспериментального статуса. У WPF Fluent-тема (`ThemeMode`, появилась в .NET 9) охватывает всё больше контролов, но Microsoft прямо пишет, что поддержка Fluent «ещё в процессе». Перед выбором проверьте нужные вам контролы.
- Буфер обмена: в .NET 10 WPF и WinForms используют общий код clipboard. Методы, зависящие от `BinaryFormatter`, помечены устаревшими; используйте `TryGetData<T>` и JSON-сериализацию. `BinaryFormatter` в .NET 9+ удалён — старый код, который клал в буфер произвольные объекты, придётся переписать.
- MVVM — стандарт для XAML-фреймворков (WPF, WinUI, Avalonia, Uno, MAUI). Берите `CommunityToolkit.Mvvm`: генераторы `[ObservableProperty]` (в 8.4 — на partial-свойствах, что дружит с AOT и WinRT) и `[RelayCommand]` убирают шаблонный код `INotifyPropertyChanged`. Для слабосвязанного обмена сообщениями — `WeakReferenceMessenger`. ViewModel не должна ссылаться на типы UI-фреймворка: так её можно тестировать обычными unit-тестами.
- Включайте compiled bindings (`x:DataType` в Avalonia/MAUI, `x:Bind` в WinUI/Uno). Ошибки в путях привязки ловятся при компиляции, исчезает рефлексия, приложение совместимо с trimming и AOT. В Avalonia 12 compiled bindings включены по умолчанию.
- UI-поток — святое: тяжёлую работу выполняйте через `await` и `Task.Run` для CPU-задач, обновление UI — после `await` в UI-контексте или через `Dispatcher`. Никогда не вызывайте `.Result`/`.Wait()` на UI-потоке: в WPF/WinForms это классический дедлок с `SynchronizationContext`.
- Avalonia 12 (апрель 2026) — релиз о производительности и стабильности: минимум .NET 8 (рекомендуется .NET 10), поддержка netstandard2.0/.NET Framework, Tizen, Direct2D1 и Blazor-интеграции удалена. Шейпер текста настраивается отдельно (`UseHarfBuzz`), плагин DataAnnotations выключен по умолчанию. Миграция с 11.x — по официальному гайду breaking changes. Для запуска существующего WPF-приложения на macOS/Linux без переписывания есть коммерческий **Avalonia XPF** (лицензия за приложение, от €9 500).
- Uno Platform 6.x реализует API WinUI на всех платформах, рисуя через Skia. В 6.6 (август 2026) появились Native AOT-публикация на пяти платформах (по данным релиза, ускорение старта от 21% на iOS до 61% на Android) и опциональный бэкенд Vulkan. Uno Platform Studio (Hot Design, MCP-серверы для AI-агентов) — платная надстройка.
- Windows App SDK перешёл на SemVer: версия 2.0 вышла 29 апреля 2026 (первый мажорный релиз с 1.0), ломающие изменения теперь только в мажорных версиях, следующая параллельно устанавливаемая — 3.0. Минорные 2.x выходят примерно раз в месяц (2.5.1 — сентябрь 2026). Ветка 1.8 продолжает получать исправления. WinUI 3 по-прежнему только для Windows. Для упаковки выбирайте между MSIX и unpackaged-режимом заранее: от этого зависят API идентификации приложения, уведомления и автообновление.
- Нативный Win32 из C#: вместо ручных `DllImport` генерируйте P/Invoke через `Microsoft.Windows.CsWin32` (source generator по метаданным Win32) и используйте `LibraryImport` вместо `DllImport` — так маршалинг генерируется при компиляции и совместим с AOT.
- Доступность (accessibility) закладывайте сразу: у каждого интерактивного элемента — имя для экранного диктора (`AutomationProperties.Name` в WPF/WinUI/Avalonia, `SemanticProperties.Description` в MAUI), полная работа с клавиатуры и видимый фокус, поддержка высокого контраста и масштабирования текста. Свои контролы должны отдавать `AutomationPeer`. Проверяйте Accessibility Insights for Windows и Экранным диктором, а не только глазами.
- Безопасность десктопа: всё, что поставлено пользователю, читается (декомпилятор, отладчик), поэтому никаких API-ключей и секретов в бинарнике или `appsettings.json` — клиент ходит через ваш бэкенд с токеном пользователя (OAuth с PKCE через системный браузер). Локальные секреты пользователя шифруйте DPAPI (`ProtectedData` с `DataProtectionScope.CurrentUser`) или храните в Credential Manager. Подписывайте `.exe`/MSIX сертификатом (signtool), а автообновление не пишите сами: обновления только по HTTPS и только подписанные; готовый вариант — Velopack, который встраивает подпись кода в сборку релиза.
- Локализация: строки — в `.resx` (сателлитные сборки по культурам), форматирование чисел и дат — по `CurrentCulture` для показа и по `InvariantCulture` для файлов и протоколов. Закладывайте запас ширины под длинные переводы и поддержку RTL (`FlowDirection`).

### Пакеты
- `CommunityToolkit.Mvvm` (8.4.2) — MVVM на генераторах; net8.0 + netstandard2.0.
- `Avalonia` (12.1.3) — кроссплатформенный UI, MIT; `Avalonia.Headless` — тесты UI без окна (xUnit v3, NUnit 4).
- `Uno.Sdk` (линия 6.x; 6.6 — август 2026) — WinUI API на всех платформах.
- `Microsoft.WindowsAppSDK` (2.5.1; 1.8.x — поддерживаемая ветка) — WinUI 3 и API Windows.
- `Microsoft.Windows.CsWin32` (0.3.355; линия 0.x) — генерация P/Invoke для Win32.
- `Velopack` (1.2, MIT) — установщик, дельта-обновления и автообновление десктоп-приложений.
- WPF и WinForms входят в .NET SDK (`<UseWPF>`/`<UseWindowsForms>`), отдельных пакетов не требуют.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [What's new in WPF for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100) | Microsoft, 2025–2026 | Fluent-стили, clipboard, производительность, новые API |
| [What's new in WPF for .NET 9](https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net90) | Microsoft, 2024 | Появление Fluent-темы и `ThemeMode` |
| [Fluent theme: not a theme](https://badecho.com/index.php/2025/09/03/fluent-theme-not-a-theme/) | Matt Weber (Bad Echo), 2025 | Критический разбор того, как устроена Fluent-тема WPF |
| [What's new in WinForms for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100) | Microsoft, 2025–2026 | Тёмная тема, async-формы, `ScreenCaptureMode`, новые анализаторы |
| [WinForms what's new (все версии)](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new) | Microsoft, живой | История изменений WinForms по релизам |
| [Windows App SDK 1.8 release notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-8) | Microsoft, 2025–2026 | Состояние поддерживаемой ветки 1.8 |
| [Windows App SDK 2.0.1](https://github.com/microsoft/WindowsAppSDK/releases/tag/v2.0.1) | Microsoft, апрель 2026 | Первый релиз на SemVer: что нового и что сломано |
| [Accessibility overview (Windows apps)](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview) | Microsoft, живой | Требования доступности для настольных приложений; инструмент проверки — [Accessibility Insights for Windows](https://accessibilityinsights.io/) |
| [How to: Use Data Protection (DPAPI)](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection) | Microsoft, живой | `ProtectedData` для локальных секретов пользователя |
| [Sign an app package using SignTool](https://learn.microsoft.com/en-us/windows/msix/package/sign-app-package-using-signtool) | Microsoft, живой | Подпись MSIX и исполняемых файлов |
| [Velopack](https://docs.velopack.io/) | Velopack, живой | Установка и автообновление для Windows, macOS, Linux |
| [microsoft-ui-xaml](https://github.com/microsoft/microsoft-ui-xaml) | Microsoft, живой | Исходники и roadmap WinUI 3 |
| [MVVM Toolkit docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) | Microsoft, живой | `ObservableObject`, генераторы, мессенджер |
| [Avalonia 12 Preview 1](https://avaloniaui.net/blog/avalonia-12.0-preview-1) | AvaloniaUI, 2026 | Цели 12-й версии и что удалено |
| [Breaking changes in Avalonia 12](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes) | AvaloniaUI, 2026 | Обязательный гайд миграции с 11.x |
| [Avalonia releases](https://github.com/AvaloniaUI/Avalonia/releases) | AvaloniaUI, живой | Изменения по патчам 12.x |
| [Avalonia XPF](https://avaloniaui.net/xpf) | AvaloniaUI, живой | Запуск WPF-приложений на macOS/Linux, цены |
| [Uno Platform 6.4](https://platform.uno/blog/uno-platform-6-4/) | Uno Platform, 2025 | Поддержка .NET 10, Studio 2.0 |
| [Uno Platform 6.6 (InfoQ)](https://infoq.com/news/2026/08/uno-platform-6-6) | InfoQ, 2026 | Native AOT, Vulkan, упрощённый XAML |
| [Uno Platform 6.0 (InfoQ)](https://www.infoq.com/news/2025/05/uno-platform-6-release) | InfoQ, 2025 | Переход на единый Skia-рендерер |

---

## 2. Мобильная разработка

### Ключевые выводы
- Варианты на C#:
  - **.NET MAUI** — один проект, нативные контролы каждой ОС через handlers.
  - **.NET for Android / .NET for iOS** без MAUI — полный доступ к нативным API, UI на нативных инструментах (Android XML/Compose-интероп, UIKit).
  - **Uno Platform** и **Avalonia** — UI, отрисованный Skia одинаково везде. Модель ближе к Flutter, пиксель-в-пиксель на всех платформах.
- Политика поддержки MAUI отличается от .NET: мажорная версия MAUI поддерживается до шести месяцев после выхода следующей. MAUI 10 (на .NET 10 LTS) поддерживается только до 11 мая 2027 года — планируйте ежегодный апгрейд мобильных приложений, LTS здесь не спасает.
- .NET 10 для MAUI — релиз качества. `ListView`, `TableView` и ячейки устарели: используйте `CollectionView`. `MessagingCenter` стал internal: переходите на `WeakReferenceMessenger`. `DisplayAlert` и анимации заменены `*Async`-версиями. Включайте генератор XAML: `<MauiXamlInflator>SourceGen</MauiXamlInflator>`.
- .NET 11 меняет рантайм: CoreCLR стал рантаймом по умолчанию для MAUI на Android, iOS и Mac Catalyst вместо Mono; NativeAOT — опциональная публикация. В Release-сборках Android используется частичный ReadyToRun; полный включается `MauiEnableFullReadyToRun`, это обмен размера пакета на скорость старта. Минимальный Android API поднят до 24, x86 для Android не поддерживается, пакет `Microsoft.Maui.Controls.Compatibility` больше не выпускается. Shell и навигация по умолчанию переведены на handlers: кастомные renderers нужно перепроверить.
- Пишите AOT/trim-совместимо с самого начала: compiled bindings с `x:DataType`, `System.Text.Json` с source generation, никакой рефлексии в навигации и DI-сканировании. На iOS AOT обязателен, а предупреждения trimmer'а в .NET 10 включены по умолчанию — их нельзя игнорировать.
- Производительность списков и старта: `CollectionView` с виртуализацией и простыми `DataTemplate`, без вложенных `StackLayout` в ячейках. Используйте метрики `Microsoft.Maui` (measure/arrange в .NET 10) и `dotnet-trace`/`dotnet-counters` на устройстве. Тяжёлую инициализацию откладывайте после первого кадра.
- Против нативной разработки и Flutter: C# выигрывает при общей с бэкендом кодовой базе и команде .NET. Проигрывает при глубокой работе с новыми API ОС в день релиза: биндинги отстают, и Xcode/Android SDK диктуют свои сроки. Для приложения, которое в основном показывает веб-контент, рассмотрите `HybridWebView`/`BlazorWebView`.

### Пакеты
- `Microsoft.Maui.Controls` (10.0.110; 11.0.0-rc.1 — превью) — MAUI.
- Workloads `android`, `ios`, `maccatalyst` (`dotnet workload install maui`) — .NET for Android (API 36 по умолчанию в .NET 10) и .NET for iOS.
- `CommunityToolkit.Maui` (15.0.1) — поведения, конвертеры, попапы.
- `CommunityToolkit.Mvvm` (8.4.2) — MVVM.
- `Avalonia.Android` / `Avalonia.iOS` (12.1.3), `Uno.Sdk` (6.x) — альтернативы с отрисовкой через Skia.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [What's new in .NET MAUI for .NET 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10) | Microsoft, 2025–2026 | Устаревшие контролы, XAML SourceGen, изменения Android/iOS |
| [What's new in .NET MAUI for .NET 11](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-11) | Microsoft, 2026 | CoreCLR по умолчанию, новые handlers, ленивые ресурсы |
| [Accessibility in .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility?view=net-maui-10.0) | Microsoft, живой | `SemanticProperties`, заголовки, фокус, проверка экранными чтецами |
| [CoreCLR for .NET MAUI](https://aka.ms/maui-coreclr) | .NET Blog, 2026 | Зачем MAUI переходит с Mono на CoreCLR |
| [.NET MAUI Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/maui) | Microsoft, живой | Сроки поддержки каждой версии MAUI |
| [.NET MAUI 10: quality and performance](https://www.infoq.com/news/2025/11/net-maui-10-quality-performance) | InfoQ, 2025 | Обзор MAUI 10 |
| [.NET 11 Preview 6 roundup](https://visualstudiomagazine.com/articles/2026/07/15/net-11-preview-6-roundup-aspnet-core-maui-c-ef-core-and-sdk-updates.aspx) | Visual Studio Magazine, 2026 | Изменения MAUI и SDK в цикле .NET 11 |
| [dotnet/maui](https://github.com/dotnet/maui) | Microsoft, живой | Исходники, issues, обсуждение регрессий |
| [dotnet/android releases](https://github.com/dotnet/android/releases) | Microsoft, живой | Изменения .NET for Android по версиям |
| [dotnet/macios releases](https://github.com/dotnet/macios/releases) | Microsoft, живой | Изменения .NET for iOS/macOS и поддержка Xcode |
| [Known issues in .NET 10 (macios)](https://github.com/dotnet/macios/wiki/Known-issues-in-.NET10) | Microsoft, живой | Известные проблемы iOS-тулчейна |
| [MAUI 10 RC1 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/preview/rc1/dotnetmaui.md) | Microsoft, 2025 | Подробный список изменений перед GA |

---

## 3. CLI-утилиты и скрипты

### Ключевые выводы
- Парсер аргументов:
  - **System.CommandLine** — официальная библиотека, на которой построен .NET CLI. Линия 2.0 стабильна (вышла вместе с .NET 10, ежемесячные патчи; 2.0.12 — сентябрь 2026). API 2.0 сильно отличается от бет: `RootCommand`, `Option<T>("--name")`, `command.SetAction(parseResult => ...)`, `parseResult.GetValue(option)`. Старые туториалы до beta5 вводят в заблуждение. Библиотека дружит с trimming и AOT, автодополнение и response-файлы встроены.
  - **ConsoleAppFramework** (Cysharp) — генератор кода без рефлексии и аллокаций, лучший выбор для минимального бинарника и времени старта.
  - **Spectre.Console.Cli** — если UI уже на Spectre.
- Вывод: Spectre.Console — таблицы, деревья, прогресс-бары, промпты, разметка `[green]...[/]`. Пишите данные в stdout, а логи, прогресс и ошибки — в stderr. Проверяйте `Console.IsOutputRedirected`, чтобы отключать цвет и анимацию в пайпе; уважайте `NO_COLOR`.
- Коды выхода: `return` из `Main`/top-level `int` или `Environment.ExitCode`. Различайте ошибку пользователя (2) и внутреннюю ошибку (1). Ctrl+C обрабатывайте через `CancellationToken` (System.CommandLine и ConsoleAppFramework передают его в обработчик).
- Скрипты без проекта: в .NET 10 `dotnet run app.cs` (или просто `dotnet app.cs`) запускает одиночный файл.
  - Директивы: `#:package Spectre.Console@0.57.2`, `#:project ../Lib/Lib.csproj`, `#:property PublishAot=false`, `#:sdk Microsoft.NET.Sdk.Web`; `#:include` для нескольких файлов — в SDK 10.0.300+ и .NET 11.
  - На Unix работает shebang `#!/usr/bin/env -S dotnet --` (файл с LF, без BOM).
  - Когда скрипт вырос — `dotnet project convert app.cs`.
  - Не кладите такие файлы внутрь каталога с `.csproj`: на них влияют `Directory.Build.props` и прочие неявные файлы сборки.
- Публикация файлового приложения (`dotnet publish app.cs`) по умолчанию делает Native AOT, а `dotnet pack app.cs` — пакет .NET tool. Native AOT для CLI даёт старт за миллисекунды и один бинарник без рантайма. Цена — запрет неанализируемой рефлексии, `System.Text.Json` только с source generation, сборка под каждый RID отдельно.
- Распространение: глобальный или локальный .NET tool (`<PackAsTool>true</PackAsTool>`, `dotnet tool install -g`; локальные tools фиксируются в `.config/dotnet-tools.json`). Для пользователей без .NET — self-contained или AOT-бинарники в GitHub Releases, winget, Homebrew.
- Тестируемость: держите логику команд в обычных классах, а парсер — тонким слоем. System.CommandLine позволяет вызвать `Parse(args).InvokeAsync()` прямо в тесте с подменённым выводом.

### Пакеты
- `System.CommandLine` (2.0.12) — официальный парсер.
- `Spectre.Console` (0.57.2; 0.x, но стабилен в практике) — богатый консольный вывод; `Spectre.Console.Cli` — его командный фреймворк.
- `ConsoleAppFramework` (5.7.13) — CLI на source generators, AOT-safe; требует C# 13.
- `Cocona` (2.2.0, последний релиз в марте 2023) — `[устарело]`, для новых проектов не берите.
- `Microsoft.Extensions.Hosting` (10.x) — DI, конфигурация и логирование для сложных CLI.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [System.CommandLine overview](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) | Microsoft, живой | Официальная документация стабильной версии 2.0 |
| [Migration guide to 2.0.0-beta5+](https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5) | Microsoft, 2025 | Как перевести код со старых бет на актуальный API |
| [System.CommandLine на NuGet](https://www.nuget.org/packages/System.CommandLine) | Microsoft, живой | Текущая версия и история патчей |
| [File-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps) | Microsoft, обновлено 2026 | Директивы `#:`, AOT по умолчанию, shebang, кеш сборки |
| [Announcing dotnet run app.cs](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/) | .NET Blog, 2025 | Мотивация и первые примеры файловых приложений |
| [Spectre.Console](https://spectreconsole.net/) | Spectre Systems, живой | Виджеты, разметка, CLI-фреймворк |
| [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework) | Cysharp (Yoshifumi Kawai), живой | CLI без рефлексии: генерация, middleware, фильтры |
| [Create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create) | Microsoft, живой | Упаковка и публикация глобальных и локальных tools |
| [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) | Microsoft, живой | Ограничения AOT, анализаторы, размер бинарника |
| [Cocona](https://www.nuget.org/packages/Cocona) | Mayuki Sawatari, 2023 | `[устарело]` — не обновляется с 2023 года |

---

## 4. Библиотеки и NuGet-пакеты

### Ключевые выводы
- Целевые фреймворки:
  - Для новой библиотеки достаточно `net8.0;net10.0` (минимальная поддерживаемая LTS и текущая). Поддержка .NET 8 и .NET 9 заканчивается 10 ноября 2026: после этого `net8.0` оставляйте, только пока он нужен вашим потребителям, а по умолчанию таргетируйте `net10.0`.
  - Добавляйте `netstandard2.0` только если нужны потребители на .NET Framework, Unity (Mono) или старых платформах. Это всё ещё массовый случай: `Microsoft.Extensions.AI`, MCP SDK, OpenAI, Anthropic SDK и `CommunityToolkit.Mvvm` таргетят netstandard2.0.
  - Avalonia 12 от него отказалась — решайте по своей аудитории.
  - Новые возможности C# на старых TFM дают полифилы (`PolySharp`).
- Мультитаргетинг: общий код плюс `#if NET8_0_OR_GREATER` для быстрых путей (`Span`, `SearchValues`, generic math). Включайте `EnablePackageValidation`: проверка сравнит публичный API между TFM. С `PackageValidationBaselineVersion` она сравнит API с прошлой стабильной версией и поймает ломающие изменения до публикации.
- Trimming и AOT: ставьте `<IsAotCompatible>true</IsAotCompatible>` (подразумевает `IsTrimmable`) условно для TFM от net6.0 и выше. Анализ trimming для netstandard и .NET Framework не поддерживается. Рефлексию либо убирайте (source generators), либо описывайте через `[DynamicallyAccessedMembers]`, либо поднимайте `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` до публичного API. `UnconditionalSuppressMessage` — только с доказуемым обоснованием. Полный список предупреждений даёт тестовое приложение с `PublishTrimmed` и `TrimmerRootAssembly`. Появление новых trim-предупреждений — ломающее изменение для пользователей.
- Дизайн публичного API — по Framework Design Guidelines:
  - Минимальная поверхность, `sealed` по умолчанию.
  - Асинхронные методы с `CancellationToken` последним параметром; `ValueTask` — только когда синхронное завершение частое и измерено.
  - Nullable reference types включены, исключения вместо кодов ошибок.
  - Не выставляйте типы зависимостей в публичный API без нужды: их мажорный апгрейд станет вашим.
  - Для расширяемости — интерфейсы в отдельном `*.Abstractions`-пакете (так устроены `Microsoft.Extensions.*`).
- Версионирование: SemVer для пакета. `AssemblyVersion` меняйте только на мажорных версиях, иначе потребители на .NET Framework получат проблемы с binding redirects. Версию выводите из git-тегов (MinVer, Nerdbank.GitVersioning). Сборки детерминированные (`ContinuousIntegrationBuild=true` в CI).
- Метаданные пакета: `PackageReadmeFile` (README показывается на nuget.org), `PackageIcon` (встроенный файл, не URL), `PackageLicenseExpression`, `RepositoryUrl`. SourceLink встроен в SDK начиная с .NET 8: достаточно git-репозитория и публикации `.snupkg` с символами, тогда отладчик потребителя скачает ваш исходник. Подписывайте пакеты и включайте 2FA на nuget.org.
- Source generator как продукт: отдельный проект на netstandard2.0, упакованный в `analyzers/dotnet/cs`, без транзитивных runtime-зависимостей. Атрибуты-маркеры — в основном пакете или генерируются через `RegisterPostInitializationOutput`. Подробности — в разделе 9.

### Пакеты
- `Microsoft.DotNet.ApiCompat.Tool` — проверка совместимости API вне MSBuild (Package Validation встроена в SDK).
- `PolySharp` (1.16.0) — полифилы атрибутов и типов C# для netstandard2.0/.NET Framework.
- `MinVer` (8.0.0), `Nerdbank.GitVersioning` (3.10.94) — версия из git-тегов.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` (5.6.0) — фиксация публичного API в `PublicAPI.Shipped.txt`.
- `Microsoft.SourceLink.*` — только для не-GitHub/GitLab хостингов и старых SDK; в .NET 8+ встроено.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Open-source library guidance](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/) | Microsoft, живой | Полный чек-лист: TFM, NuGet, SemVer, зависимости, SourceLink |
| [Cross-platform targeting](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/cross-platform-targeting) | Microsoft, живой | Когда нужен netstandard2.0, когда — net8.0+ |
| [Versioning](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning) | Microsoft, живой | `AssemblyVersion` против `PackageVersion`, правила апгрейда |
| [Source Link](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink) | Microsoft, живой | Отладка исходников пакета у потребителя |
| [Package Validation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview) | Microsoft, живой | Три валидатора и базовая версия |
| [Prepare libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming) | Microsoft, 2025–2026 | `IsTrimmable`, аннотации, тестовое приложение |
| [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) | Microsoft (K. Cwalina и др.), живой | Именование, типы, исключения, расширяемость |
| [MSBuild property functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions) | Microsoft, живой | Условия по TFM (`IsTargetFrameworkCompatible`) |
| [PolySharp](https://github.com/Sergio0694/PolySharp) | Sergio Pedri, живой | Новые фичи C# на старых TFM без рантайм-зависимостей |

---

## 5. Разработка игр

### Ключевые выводы
- Движок по задаче:
  - **Unity** — самый большой рынок, ассеты и мобильные/консольные платформы.
  - **Godot 4 (.NET)** — открытый движок с редактором; C# — полноправный язык скриптинга на десктопе.
  - **MonoGame / FNA** — фреймворки в духе XNA: всё в коде, без редактора.
  - **Stride** — открытый 3D-движок с редактором на современном .NET.
  - **raylib-cs** — обучение и джемы.
- Unity сегодня: Unity 6.3 LTS поддерживает C# 9 с ограничениями. Нет init-only setters (нет `IsExternalInit`), нет ковариантных возвратов и module initializers; `record` ограничены и не сериализуются. Рантайм — форк Mono в редакторе и IL2CPP для сборок. Переход на CoreCLR идёт поэтапно:
  - Unity 6.6 — Fast Enter Play Mode по умолчанию, Burst встроен в ядро.
  - Unity 6.7 LTS — последний релиз на Mono, экспериментальный CoreCLR-плеер.
  - Следующий крупный релиз — Unity 7 (в более ранних планах — 6.8): по июньскому отчёту 2026 года он полностью удаляет Mono и даёт CoreCLR, .NET 10 и C# 14. По анонсу на Unite Seoul (июль 2026) превью Unity 7 выходит в декабре 2026, полный релиз — в I квартале 2027 года. На октябрь 2026 года Unity 7 ещё не вышел.
  - Готовьтесь уже сейчас: не полагайтесь на domain reload для сброса статического состояния, сбрасывайте статику явно, используйте новые lifecycle-атрибуты.
- Производительность в Unity: горячий код — Jobs + Burst (подмножество C# без managed-объектов: `struct`, `NativeArray`, `[BurstCompile]`). Много однородных сущностей — Entities (DOTS). Обычный MonoBehaviour-код — без аллокаций в `Update`.
- Godot C#: Godot 4.x требует .NET 8+, для экспорта на Android — .NET 9+. Desktop поддерживается полностью, Android/iOS — экспериментально. Экспорт C#-проектов в Web в Godot 4 невозможен. Работа над переводом C# на GDExtension идёт; в мае 2025 года показан прототип веб-экспорта со статически слинкованным Mono, но команда официально не называет сроков. Версия 4.7 (июнь 2026) веб-экспорт C# не добавила. Оценки «не раньше 4.8» на форуме — мнение сообщества, а не план разработчиков. Если нужен браузер — GDScript или Godot 3. Сигналы подключайте через сгенерированные события C#. Не держите ссылки на освобождённые узлы (`IsInstanceValid`), горячие вызовы между C# и движком делайте крупными: маршалинг `Variant` стоит дорого.
- Правило игрового цикла — ноль аллокаций на кадр. GC-паузы в Unity (Boehm, инкрементальный) и на мобильных платформах видны как фризы. В `Update`:
  - не создавайте коллекции, не используйте LINQ, лямбды с захватом, `string`-конкатенацию, `foreach` по интерфейсам (боксинг энумератора) и `params`;
  - предвыделяйте буферы, используйте пулы (`ObjectPool<T>`, `ArrayPool<T>`), `Span<T>`/`stackalloc`, структуры для данных;
  - кешируйте `GetComponent` и поиск узлов.
- Отделяйте симуляцию от движка: чистая библиотека правил (netstandard2.1 или net8.0 в зависимости от движка) без `UnityEngine`/`Godot`-типов, с детерминированным RNG в состоянии и фиксированным шагом. Её можно тестировать обычным xUnit, гонять на сервере и использовать для реплеев. В Unity такую логику оформляйте через assembly definitions.
- Детерминизм для сетевой игры (lockstep, rollback, реплеи). Решения, которые трудно вернуть позже:
  - состояние симуляции в целых числах или fixed-point; `float`/`double` и `Math.Sin`/`Pow` дают разные результаты на разных CPU, ОС и с разной шириной SIMD;
  - свой RNG в состоянии мира, никакого `System.Random` и времени внутри шага;
  - обход только по индексам массивов, не по `Dictionary`/`HashSet`: порядок перебора и хэши строк от запуска к запуску разные;
  - команды игроков применяются в фиксированном порядке (тик, игрок, номер);
  - хэш состояния раз в N тиков для поиска рассинхрона и тест «два прогона с одним seed дают одинаковый хэш».

  Lockstep передаёт только ввод и подходит стратегиям с тысячами юнитов. Rollback откатывает и пересчитывает несколько тиков и подходит играм, где важен мгновенный отклик: файтингам, спортивным, платформерам. Оба подхода требуют детерминированной симуляции.
- Много однородных сущностей вне Unity DOTS: рукописный SoA (массив на поле) или ECS-библиотека — Arch, Friflo.Engine.ECS. ECS окупается, когда комбинаций компонентов много. Для одного типа юнитов с одинаковыми полями SoA проще.
- MonoGame 3.8.5 работает на .NET 8+, ветка 3.8.6 в превью. FNA — точная реимплементация XNA4 на SDL3 для десктопа, поддерживает NativeAOT и консоли, выпускается ежемесячно. Stride 4.3 таргетит .NET 10. raylib-cs 8.1 (raylib 6.0) добавил запуск в браузере через WebAssembly, но поддерживается пассивно.

### Пакеты
- `GodotSharp` (4.7.x; движок 4.7.2 — август 2026) — C# API Godot.
- `MonoGame.Framework.DesktopGL` (3.8.5.1; 3.8.6-preview) — кроссплатформенный XNA-подобный фреймворк.
- FNA — подключается как исходный проект или сабмодуль (ежемесячные релизы на GitHub).
- `Stride.Engine` (4.3.0.2507) — 3D-движок на .NET 10.
- `Raylib-cs` (8.1.0, raylib 6.0) — привязки raylib, включая browser-wasm.
- Unity: пакеты `com.unity.burst`, `com.unity.entities`, `com.unity.collections` ставятся через Package Manager (с 6.6 Burst — встроенный модуль).
- `Arch` (2.1.0, май 2025; Apache-2.0) — архетипная ECS на .NET, без зависимости от движка.
- `Friflo.Engine.ECS` (3.6.0, март 2026; 4.0 в превью; MIT) — ECS с запросами, событиями и сериализацией, без зависимости от движка.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Unity C# compiler](https://docs.unity3d.com/6000.3/Documentation/Manual/csharp-compiler.html) | Unity, 2025–2026 | Какая версия C# и какие фичи не поддерживаются |
| [CoreCLR, Scripting and ECS — March 2026](https://discussions.unity.com/t/coreclr-scripting-and-ecs-status-update-march-2026/1711852) | Unity, 2026 | План 6.5–6.8, EntityId, подготовка кода |
| [CoreCLR, Scripting and Serialization — June 2026](https://discussions.unity.com/t/coreclr-scripting-and-serialization-update-june-2026/1723299) | Unity, 2026 | Последний Mono-релиз 6.7, CoreCLR в Unity 7.0 |
| [Unite Seoul 2026 keynote recap](https://unity.com/blog/unite-seoul-keynote-2026-recap) | Unity, июль 2026 | Анонс Unity 7: превью в декабре 2026, релиз в I квартале 2027, CoreCLR, .NET 10, C# 14 |
| [Path to CoreCLR: Upgrade Guide](https://discussions.unity.com/t/path-to-coreclr-2026-upgrade-guide/1714279) | Unity, 2026 | Что поменять в проекте до перехода |
| [Unity's 2026 roadmap](https://digitalproduction.com/2025/11/26/unitys-2026-roadmap-coreclr-verified-packages-fewer-surprises/) | Digital Production, 2025 | Обзор дорожной карты |
| [Burst manual](https://docs.unity3d.com/Packages/com.unity.burst@latest) | Unity, живой | Подмножество HPC#, ограничения, отладка |
| [Godot C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html) | Godot, живой | Требования .NET, поддерживаемые платформы, ограничения |
| [Godot 4.7.2](https://godotengine.org/download/archive/4.7.2-stable/) | Godot, 2026 | Текущий стабильный релиз |
| [C# web export: обсуждение](https://forum.godotengine.org/t/is-there-an-update-on-exporting-c-projects-to-web/128821) | Godot Forum, 2025–2026 | Статус экспорта C# в браузер |
| [.NET 10 and C# Web support proposal](https://github.com/godotengine/godot-proposals/discussions/13076) | godot-proposals, 2025 | Технические препятствия веб-экспорта |
| [The Future of C# in Godot](https://gamefromscratch.com/the-future-of-c-in-godot/) | GameFromScratch, 2023 | План переноса C# на GDExtension |
| [MonoGame](https://monogame.net/) | MonoGame Foundation, живой | Документация и туториалы |
| [FNA](https://github.com/FNA-XNA/FNA) / [docs](https://fna-xna.github.io/docs/) | FNA team, живой | Точный XNA4 на SDL3, порты на консоли |
| [Stride](https://www.stride3d.net/) | Stride community, живой | Открытый C#-движок с редактором |
| [Raylib-cs](https://www.nuget.org/packages/Raylib-cs) | ChrisDill и сообщество, 2026 | Привязки raylib 6.0 |
| [Game Programming Patterns](https://gameprogrammingpatterns.com/) | Robert Nystrom, 2014 | Game Loop, Object Pool, Command — словарь игровой архитектуры |
| [Fix Your Timestep!](https://gafferongames.com/post/fix_your_timestep/) | Glenn Fiedler, 2004 | Фиксированный шаг симуляции и интерполяция рендера |
| [Floating Point Determinism](https://gafferongames.com/post/floating_point_determinism/) | Glenn Fiedler, 2010 | Почему `float` расходится между машинами и что с этим делать |
| [1500 Archers on a 28.8](https://www.gamedeveloper.com/programming/1500-archers-on-a-28-8-network-programming-in-age-of-empires-and-beyond) | Paul Bettner, Mark Terrano, 2001 | Классика lockstep: передавать команды, а не состояние |
| [Arch](https://github.com/genaray/Arch) | genaray и сообщество, 2025 | Архетипная ECS на .NET, бенчмарки и примеры |
| [Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS) | Ullrich Praetz, 2026 | ECS на .NET, сравнение с другими библиотеками |

---

## 6. AI/ML и численные методы

### Ключевые выводы
- Слой абстракций — `Microsoft.Extensions.AI`: `IChatClient`, `IEmbeddingGenerator<TInput,TEmbedding>`, экспериментальный `IImageGenerator`. Пишите код приложения против интерфейсов, а провайдера (OpenAI, Azure, Anthropic, Ollama, ONNX) подставляйте через DI. Сквозные задачи собирайте конвейером `ChatClientBuilder`: `UseFunctionInvocation`, `UseOpenTelemetry`, `UseDistributedCache`, логирование. Авторы библиотек-провайдеров ссылаются только на `Microsoft.Extensions.AI.Abstractions`.
- Агенты: Microsoft Agent Framework (`Microsoft.Agents.AI`) — прямой наследник Semantic Kernel и AutoGen.
  - Версия 1.0 GA вышла в апреле 2026, дальше идут частые минорные релизы.
  - Даёт агентов, графовые workflow с чекпоинтами и human-in-the-loop, MCP и A2A, middleware, OpenTelemetry.
  - Semantic Kernel продолжает выходить (1.81), но находится в режиме поддержки: только критические исправления и безопасность. Новые проекты начинайте на Agent Framework, для старых есть официальный гайд миграции.
  - Правило из документации: «если задачу решает обычная функция — напишите функцию, а не агента».
- Официальные SDK провайдеров: `Anthropic` (с версии 10 — официальный SDK Claude для C#, реализует `IChatClient`) и `OpenAI` 2.x (генерируется из OpenAPI-спецификации совместно с Microsoft). Ключи API — из конфигурации или секрет-хранилища, никогда не из кода. Стриминг — через `IAsyncEnumerable`, отмена — через `CancellationToken`.
- MCP: официальный C# SDK (`ModelContextProtocol` 2.x, совместно с Microsoft). Сервер на stdio — `AddMcpServer().WithStdioServerTransport()` и методы с `[McpServerTool]`; HTTP-сервер — `ModelContextProtocol.AspNetCore`. В stdio-сервере ничего не пишите в stdout, кроме протокола: логи — в stderr.
- RAG и векторы: `Microsoft.Extensions.VectorData` — единые абстракции коллекций, CRUD, векторного и гибридного поиска с LINQ-фильтрами; коннекторы под конкретные БД ставятся отдельно. Для локальных вычислений сходства используйте `TensorPrimitives.CosineSimilarity`/`Dot` из `System.Numerics.Tensors`: SIMD без ручных циклов.
- Локальный инференс: ONNX Runtime (модель, обученная в Python, экспортируется в ONNX), ONNX Runtime GenAI для LLM на устройстве (Phi, Llama, Gemma, Mistral), LLamaSharp для GGUF-моделей через llama.cpp. Обучение глубоких сетей на .NET — TorchSharp (привязки к libtorch 2.10, API близок к PyTorch). Классическое ML (классификация, регрессия, рекомендации, аномалии) — ML.NET 5, AutoML через Model Builder.
- Производительность: `InferenceSession` ONNX Runtime создавайте один раз — он потокобезопасен для `Run`. Тензоры передавайте через `OrtValue` поверх заранее выделенной памяти. Для пакетной обработки выбирайте execution provider (CUDA, DirectML, CoreML) явно и измеряйте его.
- Численные методы: `System.Numerics.Tensors` (`Tensor<T>`, `TensorPrimitives`) и generic math (`INumber<T>`) — современная основа. Math.NET Numerics 5.0 (2022) работает, но новых стабильных релизов давно нет: для новых проектов с линейной алгеброй проверьте альтернативы или изолируйте зависимость. Графики для отчётов и научных приложений (WinForms, WPF, Avalonia, Blazor, PNG/SVG без UI) — ScottPlot.
- Данные и ETL: C# хорошо подходит для конвейеров, где важны типы и скорость, но экосистема DataFrame слабее, чем у Python.
  - CSV: Sep (быстрый, на `Span`, AOT-совместим) или CsvHelper (удобный маппинг на классы). Большие файлы читайте потоково (`IAsyncEnumerable`), а не целиком в память.
  - Колоночные форматы: Parquet.Net (чистый C#), Apache Arrow для обмена с Python/Spark без копирования.
  - Аналитический SQL прямо в процессе — DuckDB.NET (ADO.NET-провайдер, читает Parquet/CSV напрямую). Часто проще, чем собирать агрегации на LINQ.
  - `Microsoft.Data.Analysis.DataFrame` (часть ML.NET) остаётся в версиях 0.x и развивается медленно — для серьёзной табличной аналитики берите DuckDB или Python.
  - Конвейер собирайте из `Channel<T>`/TPL Dataflow с ограниченной ёмкостью (backpressure) и пакетной записью в БД (`SqlBulkCopy`, `COPY` в Npgsql). Числа из файлов парсите с `CultureInfo.InvariantCulture`.

### Пакеты
- `Microsoft.Extensions.AI` / `.Abstractions` (10.10.0) — абстракции LLM и конвейер middleware.
- `Microsoft.Extensions.VectorData.Abstractions` (10.10.0) — абстракции векторных хранилищ.
- `Microsoft.Agents.AI` (1.24.0) — Agent Framework.
- `Microsoft.SemanticKernel` (1.81.0) — режим поддержки, для существующих проектов.
- `ModelContextProtocol` (2.2.0), `ModelContextProtocol.AspNetCore` — MCP-серверы и клиенты.
- `Anthropic` (12.54.0) — официальный SDK Claude; `OpenAI` (2.14.0) — официальный SDK OpenAI.
- `Microsoft.ML` (5.0.0) — ML.NET.
- `Microsoft.ML.OnnxRuntime` (1.30.0), `Microsoft.ML.OnnxRuntimeGenAI` (0.17.1) — инференс ONNX и LLM на устройстве.
- `TorchSharp` (0.107.0, libtorch 2.10) — тензоры и обучение.
- `LLamaSharp` (0.27.0) — локальные GGUF-модели.
- `System.Numerics.Tensors` (10.0.12; 11.0 в RC) — `TensorPrimitives`, `Tensor<T>`.
- `MathNet.Numerics` (5.0.0, 2022) — классическая численная библиотека, развивается медленно.
- `ScottPlot` (5.1.59, MIT) — графики.
- `Sep` (0.17.1, MIT), `CsvHelper` (33.1.0, MS-PL/Apache-2.0) — CSV.
- `Parquet.Net` (6.1.0, MIT), `Apache.Arrow` (23.0.0) — колоночные форматы.
- `DuckDB.NET.Data.Full` (1.5.6, MIT) — встроенная аналитическая СУБД.
- `Microsoft.Data.Analysis` (0.23.0, ноябрь 2025) — DataFrame из ML.NET.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Microsoft.Extensions.AI libraries](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) | Microsoft, 2026 | `IChatClient`, `IEmbeddingGenerator`, middleware |
| [Microsoft Agent Framework overview](https://learn.microsoft.com/en-us/agent-framework/overview/) | Microsoft, 2026 | Агенты, workflow, когда агент не нужен |
| [Migration from Semantic Kernel](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-semantic-kernel/) | Microsoft, 2026 | Перенос SK-кода на Agent Framework |
| [microsoft/agent-framework](https://github.com/microsoft/agent-framework) | Microsoft, живой | Исходники и примеры .NET/Python |
| [Agent Framework announcements](https://devblogs.microsoft.com/agent-framework/tag/announcement/) | Microsoft, 2025–2026 | История релизов RC → 1.0 → 1.x |
| [Semantic Kernel](https://github.com/microsoft/semantic-kernel) | Microsoft, живой | Для поддержки существующих проектов |
| [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) / [docs](https://csharp.sdk.modelcontextprotocol.io/) | MCP + Microsoft, живой | Серверы и клиенты MCP на .NET |
| [Anthropic C# SDK](https://www.nuget.org/packages/Anthropic) | Anthropic, 2026 | Официальный SDK Claude |
| [OpenAI .NET](https://www.nuget.org/packages/OpenAI) | OpenAI + Microsoft, 2026 | Официальный SDK OpenAI |
| [ML.NET](https://www.nuget.org/packages/Microsoft.ML) | Microsoft, 2025 | Классическое ML на .NET |
| [ONNX Runtime GenAI](https://onnxruntime.ai/docs/genai) | Microsoft, живой | LLM на устройстве из C# |
| [TorchSharp](https://www.nuget.org/packages/TorchSharp) | .NET Foundation, 2026 | PyTorch-подобный API на .NET |
| [LLamaSharp](https://www.nuget.org/packages/LLamaSharp) | SciSharp, 2026 | GGUF-модели через llama.cpp |
| [System.Numerics.Tensors](https://www.nuget.org/packages/System.Numerics.Tensors) | Microsoft, 2026 | `TensorPrimitives` и `Tensor<T>` |
| [eShopSupport](https://github.com/dotnet/eShopSupport) | Microsoft, 2024–2025 | Сквозной пример AI-приложения на M.E.AI |
| [Semantic Kernel: что его заменило](https://atlan.com/know/ai-agent/microsoft/semantic-kernel/) | Atlan, 2026 | Вторичный обзор перехода SK → Agent Framework |
| [ScottPlot](https://scottplot.net/) | Scott Harden и сообщество, живой | Графики: кулинарная книга примеров для всех UI-фреймворков |
| [Sep](https://github.com/nietras/Sep) / [CsvHelper](https://joshclose.github.io/CsvHelper/) | nietras / Josh Close, живой | Быстрый и удобный разбор CSV |
| [Parquet.Net](https://github.com/aloneguid/parquet-dotnet) / [Arrow .NET](https://github.com/apache/arrow-dotnet) | aloneguid / Apache, живой | Колоночные форматы данных |
| [DuckDB.NET](https://github.com/Giorgi/DuckDB.NET) | Giorgi Dalakishvili, живой | Аналитический SQL в процессе приложения |

---

## 7. IoT и встраиваемые устройства

### Ключевые выводы
- Три уровня железа:
  - **Одноплатные компьютеры с Linux** (Raspberry Pi и подобные) — обычный .NET 8/10 и библиотеки .NET IoT (`System.Device.Gpio`, `Iot.Device.Bindings`).
  - **Микроконтроллеры** (ESP32, STM32) — .NET nanoFramework: свой урезанный CLR и подмножество BCL.
  - **Meadow** (Wilderness Labs) — собственные платы F7 с полноценным .NET и та же модель API на Linux/Windows. Платформа развивается: в сентябре 2026 года вышли бета-версии Meadow 3.0, репозитории активны.
- На Raspberry Pi публикуйте self-contained под `linux-arm64` (или Native AOT): старт быстрее, рантайм на устройство ставить не нужно. Разрабатывайте на ПК и разворачивайте по SSH/`scp` или в контейнере. Для GPIO на современных ядрах используйте драйвер libgpiod (sysfs-интерфейс устарел), доступ — через группу `gpio`, без root.
- Драйверы датчиков в `Iot.Device.Bindings` построены поверх абстракций `I2cDevice`/`SpiDevice`/`GpioController`. Пишите свою логику против этих абстракций, тогда её можно тестировать на ПК с фейковыми устройствами.
- nanoFramework — не «маленький .NET»: подмножество BCL, другие NuGet-пакеты (`nanoFramework.*`), ограниченная рефлексия (есть вариант CoreLibrary без неё), прошивка через `nanoff` и расширение Visual Studio/VS Code. Код проектируйте под малую память: никаких LINQ-цепочек и больших строк, предвыделенные буферы. Стабильная CoreLibrary — 1.17.x, линия 2.0 пока в превью.
- Надёжность: watchdog, повторные попытки с экспоненциальной задержкой при работе с сетью и MQTT, локальная буферизация телеметрии при обрыве связи, OTA-обновления с откатом. Время берите из RTC/NTP: логика на `DateTime.Now` без синхронизации ломается после перезагрузки без сети.

### Пакеты
- `System.Device.Gpio`, `Iot.Device.Bindings` (4.2.0) — GPIO/I2C/SPI/PWM и сотни драйверов устройств.
- `nanoFramework.CoreLibrary` (1.17.11; 2.0.0-preview) — BCL nanoFramework; остальные пакеты — `nanoFramework.*`.
- `Meadow` (2.5.0; 3.0.x-beta — сентябрь 2026, Apache-2.0), `Meadow.F7`, `Meadow.Linux` — платформа Wilderness Labs.
- `MQTTnet` (5.2) — MQTT-клиент и брокер.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [dotnet/iot](https://github.com/dotnet/iot) | Microsoft + сообщество, живой | Библиотеки, драйверы, примеры по устройствам |
| [Iot.Device.Bindings](https://www.nuget.org/packages/Iot.Device.Bindings) | .NET Foundation, 2026 | Текущая версия и список устройств |
| [.NET IoT docs](https://learn.microsoft.com/en-us/dotnet/iot/) | Microsoft, живой | Настройка Raspberry Pi, GPIO, I2C, развёртывание |
| [.NET nanoFramework docs](https://docs.nanoframework.net/) | nanoFramework, живой | Поддерживаемые платы, тулинг, ограничения |
| [nanoFramework.CoreLibrary](https://www.nuget.org/packages/nanoFramework.CoreLibrary) | nanoFramework, 2025–2026 | Версии BCL, вариант без рефлексии |
| [Meadow](https://www.nuget.org/packages/Meadow) | Wilderness Labs, 2025 | Платформа Meadow и её пакеты |

---

## 8. Interop с другими экосистемами

### Ключевые выводы
- PowerShell-модули на C# (binary modules):
  - Для поддержки Windows PowerShell 5.1 и PowerShell 7 таргетьте netstandard2.0 и ссылайтесь на `PowerShellStandard.Library` (`PrivateAssets=all`). Только для PowerShell 7.6 (на .NET 10) — `System.Management.Automation`.
  - Командлет — класс-наследник `PSCmdlet` с `[Cmdlet(VerbsCommon.Get, "Thing")]`. Пишите объекты в конвейер через `WriteObject`, а не текст.
  - Зависимости модуля конфликтуют с загруженными в процесс PowerShell: изолируйте их через `AssemblyLoadContext`.
  - `Microsoft.PowerShell.SDK` нужен только для хостинга PowerShell внутри своего приложения.
- Excel:
  - **Excel-DNA** — UDF-функции, ленты, RTD и асинхронные функции на .NET 6+ или .NET Framework; результат упаковывается в один `.xll`. Лучший выбор для высокопроизводительных пользовательских функций в десктопном Excel на Windows.
  - Кроссплатформенные Office Add-ins (Excel/Word/Outlook на вебе, Mac, Windows) пишутся на JavaScript/TypeScript; C# участвует только как бэкенд-API.
  - VSTO работает только на .NET Framework и Windows — для новых проектов это тупик.
- Python:
  - **pythonnet** 3.x — двусторонний мост (Python вызывает .NET и наоборот). Перед инициализацией обязательно укажите `Runtime.PythonDLL` или `PYTHONNET_PYDLL`. Держите GIL как можно меньше (`using (Py.GIL())`).
  - **CSnakes** — source generator: по Python-функциям с аннотациями типов генерирует типизированные C#-методы. Вызов идёт в том же процессе через C-API, NumPy-массивы отображаются в `Span`, поддерживается free-threaded Python 3.13. Хороший путь, чтобы встроить ML-код на Python в .NET-сервис без HTTP-прослойки.
  - Альтернатива обоим — отдельный процесс с gRPC/HTTP, если нужна изоляция падений и версий.
- Скрипты на C# внутри приложения: Roslyn Scripting (`CSharpScript.EvaluateAsync`) позволяет выполнять пользовательский код, но тянет компилятор (десятки МБ), не совместим с Native AOT и не даёт песочницы. Недоверенный код так исполнять нельзя: AppDomain-изоляции в .NET нет. Для собственных скриптов разработчика удобнее файловые приложения `dotnet run app.cs` (раздел 3).
- F# из C# и наоборот: общий IL, проекты ссылаются друг на друга напрямую. В публичном API F#-библиотеки, предназначенной для C#, избегайте F#-специфичных типов (`option`, `list`, каррированные функции, размеченные объединения без вспомогательных методов). Используйте `Task`, `IEnumerable`, `Nullable`/null и методы-члены.
- Общий принцип interop: границу делайте крупнозернистой (пакеты данных, а не тысячи мелких вызовов), конвертируйте типы на границе и держите доменное ядро свободным от типов чужого рантайма.

### Пакеты
- `PowerShellStandard.Library` (5.1.1) — компиляция модулей для 5.1 и 7; `Microsoft.PowerShell.SDK` (7.6.6, net10.0) — хостинг PowerShell.
- `ExcelDna.AddIn` (1.9.0) — надстройки Excel (.NET Framework 4.6.2+ и .NET 6+).
- `pythonnet` (3.2.0) — мост с CPython.
- `CSnakes.Runtime` (1.2.1 — последняя стабильная, август 2025; линия 2.0 в бете, 2.0.0-beta — июль 2026) — встраивание Python через генератор.
- `Microsoft.CodeAnalysis.CSharp.Scripting` (5.x, синхронно с Roslyn) — выполнение C#-скриптов.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [How to write a PowerShell binary module](https://learn.microsoft.com/en-us/powershell/scripting/developer/module/how-to-write-a-powershell-binary-module) | Microsoft, живой | Структура модуля, манифест, командлеты |
| [Microsoft.PowerShell.SDK](https://www.nuget.org/packages/Microsoft.PowerShell.SDK) | Microsoft, 2026 | Хостинг PowerShell 7.6 |
| [Excel-DNA](https://excel-dna.net/) | Govert van Drimmelen, живой | UDF, ленты, RTD, упаковка `.xll` |
| [ExcelDna.AddIn](https://www.nuget.org/packages/ExcelDna.AddIn) | Excel-DNA, 2025 | Версии и поддерживаемые TFM |
| [Office Add-ins overview](https://learn.microsoft.com/en-us/office/dev/add-ins/overview/office-add-ins) | Microsoft, живой | Кроссплатформенная модель надстроек Office |
| [pythonnet](https://github.com/pythonnet/pythonnet) | pythonnet, живой | Мост Python ↔ .NET, встраивание интерпретатора |
| [pythonnet на NuGet](https://www.nuget.org/packages/pythonnet) | pythonnet, 2026 | Настройка `PythonDLL` |
| [CSnakes](https://github.com/tonybaloney/CSnakes) | Anthony Shaw, живой | Python в .NET через source generator |
| [F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines) | Microsoft, живой | Как проектировать F#-API для C#-потребителей |

---

## 9. Инструменты разработчика: Roslyn и MSBuild

### Ключевые выводы
- Только инкрементальные генераторы (`IIncrementalGenerator`). Старый `ISourceGenerator` устарел и убивает производительность IDE.
- Правила инкрементального генератора:
  - Вход — через `ForAttributeWithMetadataName` (по cookbook — минимум в 99 раз эффективнее ручного поиска).
  - Модели в конвейере — значимо-сравнимые (`record`, `EquatableArray`), без `ISymbol`, `SyntaxNode` и `Compilation`: они не сравниваются между запусками и удерживают старые компиляции в памяти.
  - Код генерируйте текстом (`IndentedTextWriter`/`StringBuilder`), не через `SyntaxFactory` + `NormalizeWhitespace`.
  - Не ищите «все типы, реализующие интерфейс» по всей компиляции.
- Проект генератора или анализатора:
  - Таргетит `netstandard2.0` (так требует хост компилятора в VS и SDK).
  - `EnforceExtendedAnalyzerRules=true`, `IsRoslynComponent=true`.
  - Упаковывается в `analyzers/dotnet/cs`, без runtime-зависимостей.
  - Ссылается на минимальную версию `Microsoft.CodeAnalysis.CSharp`, которую поддерживают ваши пользователи. Последняя (5.9) не загрузится в старых SDK и VS.
- Тестирование: snapshot-тесты сгенерированного кода (Verify.SourceGenerators) плюс тест на инкрементальность — повторный запуск драйвера с `IncrementalStepRunReason.Cached`. Для анализаторов — `Microsoft.CodeAnalysis.Testing` с разметкой диагностик `{|ID:...|}`. Code fix поставляйте вместе с анализатором.
- Диагностики анализатора: стабильные ID с префиксом продукта, `helpLinkUri` на документацию, категории и уровни по умолчанию. Регистрируйте `context.EnableConcurrentExecution()` и `ConfigureGeneratedCodeAnalysis(None)`. Отслеживайте релизы правил в `AnalyzerReleases.Shipped.md`.
- Задачи MSBuild:
  - Наследуйте `Microsoft.Build.Utilities.Task`, ссылайтесь на `Microsoft.Build.Utilities.Core` с `ExcludeAssets="runtime"`/`PrivateAssets="all"`.
  - Учитывайте, что VS запускает MSBuild на .NET Framework: таргетьте netstandard2.0 или собирайте две версии задачи.
  - Объявляйте `Inputs`/`Outputs` у таргетов для инкрементальной сборки. Пакет кладите в `build/`/`buildTransitive/` с `.props`/`.targets`.
  - Часто инлайн-задача или `Exec` проще своей сборки — начинайте с них.
- Генерация кода из не-C# файлов (JSON-схемы, ресурсы): `AdditionalFiles` + `AdditionalTextsProvider` в генераторе предпочтительнее MSBuild-задачи — работает в IDE в реальном времени.

### Пакеты
- `Microsoft.CodeAnalysis.CSharp` (5.9.0) / `Microsoft.CodeAnalysis.Analyzers` — API Roslyn и правила для авторов анализаторов.
- `Microsoft.CodeAnalysis.CSharp.Workspaces` — для code fix и рефакторингов.
- `Microsoft.CodeAnalysis.Testing` (`Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` и др., 1.1.4 на nuget.org) — тесты анализаторов.
- `Verify.SourceGenerators` (2.5.0) — snapshot-тесты генераторов.
- `Microsoft.Build.Utilities.Core`, `Microsoft.Build.Framework` (18.10.1; ссылайтесь на минимальную версию MSBuild, которую поддерживаете) — авторинг MSBuild-задач.

### Ресурсы
| Ресурс | Автор, год | Зачем читать |
|---|---|---|
| [Incremental generators cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) | Roslyn team, живой | Главные правила производительности и упаковки |
| [Incremental generators spec](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md) | Roslyn team, живой | Модель конвейера, провайдеры, кеширование |
| [Roslyn SDK docs](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) | Microsoft, живой | Анализаторы, code fix, Syntax Visualizer |
| [Creating a source generator (серия)](https://andrewlock.net/series/creating-a-source-generator/) | Andrew Lock, 2022–2025 | Подробный практический курс: тесты, упаковка, ловушки |
| [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Microsoft, 2026 | Версии Roslyn и их соответствие SDK |
| [Tutorial: custom task for code generation](https://learn.microsoft.com/en-us/visualstudio/msbuild/tutorial-custom-task-code-generation) | Microsoft, живой | Свою MSBuild-задачу — от кода до NuGet |
| [MSBuild reserved and well-known properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties) | Microsoft, живой | Свойства для таргетов и путей |

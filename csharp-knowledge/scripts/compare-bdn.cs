// Сравнивает два прогона BenchmarkDotNet по *-report-full.json.
// Запуск (.NET 10+): dotnet run compare-bdn.cs -- <base-dir> <diff-dir> [порог-%, по умолчанию 5]
// Результаты получают так: dotnet run -c Release -- --filter "*" --exporters fulljson --artifacts <dir>
// Вердикт «faster»/«slower» ставится, только если медиана сдвинулась больше порога
// и 99,9% доверительные интервалы средних не пересекаются. Иначе «same» (в пределах шума).
// Код выхода: 0 — регрессий нет, 1 — есть хотя бы одно «slower», 2 — ошибка ввода.
using System.Globalization;
using System.Text.Json;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: dotnet run compare-bdn.cs -- <base-dir> <diff-dir> [threshold-%]");
    return 2;
}

double threshold = args.Length > 2 ? double.Parse(args[2].TrimEnd('%'), CultureInfo.InvariantCulture) / 100 : 0.05;
var baseline = Load(args[0]);
var diff = Load(args[1]);
if (baseline.Count == 0 || diff.Count == 0)
{
    Console.Error.WriteLine("no *-report-full.json found (run BenchmarkDotNet with --exporters fulljson)");
    return 2;
}

int regressions = 0;
Console.WriteLine($"{"Benchmark",-60} {"Base median",14} {"Diff median",14} {"Change",8}  Verdict");
foreach (var (name, b) in baseline.OrderBy(p => p.Key, StringComparer.Ordinal))
{
    if (!diff.TryGetValue(name, out var d))
    {
        Console.WriteLine($"{name,-60} {"",14} {"",14} {"",8}  missing in diff");
        continue;
    }
    double change = d.Median / b.Median - 1;
    string verdict = "same";
    if (change <= -threshold && d.Upper < b.Lower) verdict = "faster";
    else if (change >= threshold && d.Lower > b.Upper) { verdict = "slower"; regressions++; }
    Console.WriteLine($"{name,-60} {Fmt(b.Median),14} {Fmt(d.Median),14} {change,8:+0.0%;-0.0%}  {verdict}");
}
foreach (var name in diff.Keys.Except(baseline.Keys).Order(StringComparer.Ordinal))
    Console.WriteLine($"{name,-60} {"",14} {"",14} {"",8}  new (no baseline)");

return regressions > 0 ? 1 : 0;

static Dictionary<string, Stat> Load(string dir)
{
    var result = new Dictionary<string, Stat>();
    foreach (var file in Directory.EnumerateFiles(dir, "*-report-full.json", SearchOption.AllDirectories))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var bench in doc.RootElement.GetProperty("Benchmarks").EnumerateArray())
        {
            var stats = bench.GetProperty("Statistics");
            if (stats.ValueKind != JsonValueKind.Object) continue; // бенчмарк упал
            var ci = stats.GetProperty("ConfidenceInterval");
            result[bench.GetProperty("FullName").GetString()!] = new Stat(
                stats.GetProperty("Median").GetDouble(),
                ci.GetProperty("Lower").GetDouble(),
                ci.GetProperty("Upper").GetDouble());
        }
    }
    return result;
}

static string Fmt(double ns) => ns switch
{
    >= 1e9 => $"{ns / 1e9:0.###} s",
    >= 1e6 => $"{ns / 1e6:0.###} ms",
    >= 1e3 => $"{ns / 1e3:0.###} us",
    _ => $"{ns:0.###} ns",
};

record Stat(double Median, double Lower, double Upper);

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CodexUsageMonitor;

// Read-only local telemetry. No prompts, answers, or credentials are retained.
internal sealed class LocalUsage
{
    internal record Entry(DateTime Day, string Model, string Project, long Tokens);
    internal record Ranking(string Name, long Tokens);
    private readonly Dictionary<string, (long Length, DateTime Modified, List<Entry> Entries)> cache = new();
    public int Files { get; private set; }
    public int Unreadable { get; private set; }
    public List<Entry> Entries { get; private set; } = new();
    public void Refresh()
    {
        string root = Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in new[] { "sessions", "archived_sessions" })
        {
            var dir = Path.Combine(root, folder);
            if (Directory.Exists(dir))
                foreach (var p in Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories)) paths.Add(p);
        }
        Unreadable = 0;
        foreach (string path in paths)
        {
            try
            {
                var stat = new FileInfo(path);
                if (cache.TryGetValue(path, out var prior) && prior.Length == stat.Length && prior.Modified == stat.LastWriteTimeUtc) continue;
                cache[path] = (stat.Length, stat.LastWriteTimeUtc, Read(path));
            }
            catch (IOException) { Unreadable++; cache.Remove(path); }
            catch (UnauthorizedAccessException) { Unreadable++; cache.Remove(path); }
        }
        foreach (string key in cache.Keys.Where(p => !paths.Contains(p)).ToArray()) cache.Remove(key);
        Files = cache.Count;
        Entries = cache.Values.SelectMany(v => v.Entries).ToList();
    }
    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static long Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var value) && value.TryGetInt64(out var n) ? n : 0;
    private static List<Entry> Read(string path)
    {
        var result = new Dictionary<(DateTime, string, string), long>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        string model = "Sin modelo", project = "Sin proyecto";
        long? previous = null;
        DateTimeOffset? forkStart = null;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            // Avoid parsing message bodies, large tool outputs and unrelated event records.
            if (!line.Contains("\"session_meta\"") && !line.Contains("\"turn_context\"") && !line.Contains("\"token_count\"")) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var e = doc.RootElement;
                if (!e.TryGetProperty("payload", out var p)) continue;
                string kind = Str(e, "type");
                if (kind == "session_meta")
                {
                    var cwd = Str(p, "cwd");
                    if (!string.IsNullOrWhiteSpace(cwd)) project = cwd.TrimEnd('\\', '/');
                    if (p.TryGetProperty("forked_from_id", out var fork) && fork.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(Str(e, "timestamp"), out var start)) forkStart = start;
                }
                else if (kind == "turn_context")
                {
                    model = Str(p, "model");
                    if (model.Length == 0) model = "Sin modelo";
                    var cwd = Str(p, "cwd");
                    if (!string.IsNullOrWhiteSpace(cwd)) project = cwd.TrimEnd('\\', '/');
                }
                else if (kind == "event_msg" && Str(p, "type") == "token_count" &&
                    p.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object &&
                    info.TryGetProperty("total_token_usage", out var total))
                {
                    long current = Number(total, "total_tokens");
                    // First record uses only the last request: inherited totals cannot be attributed.
                    long delta = previous.HasValue ? Math.Max(0, current - previous.Value)
                        : info.TryGetProperty("last_token_usage", out var last) ? Number(last, "total_tokens") : 0;
                    previous = current;
                    if (delta <= 0 || !DateTimeOffset.TryParse(Str(e, "timestamp"), out var timestamp)) continue;
                    if (forkStart.HasValue && timestamp < forkStart.Value) continue;
                    var key = (timestamp.LocalDateTime.Date, model, project);
                    result.TryGetValue(key, out long tokens);
                    result[key] = tokens + delta;
                }
            }
            catch (JsonException) { /* May be the partial last line of an active rollout. Retry next refresh. */ }
        }
        return result.Select(v => new Entry(v.Key.Item1, v.Key.Item2, v.Key.Item3, v.Value)).ToList();
    }
    public List<Ranking> Rank(string range, bool models)
    {
        DateTime start = range == "7d" ? DateTime.Today.AddDays(-6) : range == "30d" ? DateTime.Today.AddDays(-29) : DateTime.MinValue;
        return Entries.Where(e => e.Day >= start).GroupBy(e => models ? e.Model : e.Project)
            .Select(g => new Ranking(g.Key, g.Sum(e => e.Tokens))).OrderByDescending(e => e.Tokens).ToList();
    }
}


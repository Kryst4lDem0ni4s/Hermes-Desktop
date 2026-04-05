namespace Hermes.Agent.Soul;

using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

/// <summary>
/// Central service for the agent's "soul" — persistent identity, user understanding,
/// project rules, mistake tracking, and learned behaviors.
///
/// File layout under hermesHome:
///   SOUL.md              — Agent identity (global)
///   USER.md              — User profile (global)
///   soul/mistakes.jsonl  — Append-only mistake journal
///   soul/habits.jsonl    — Append-only good-habit journal
///   projects/{dir}/AGENTS.md — Per-project rules
/// </summary>
public sealed class SoulService
{
    private readonly string _hermesHome;
    private readonly string _soulDir;
    private readonly ILogger<SoulService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>Maximum characters for assembled soul context to stay within ~1500 tokens.</summary>
    private const int MaxSoulContextChars = 6000;
    private const int MaxUserChars = 1500;
    private const int MaxAgentsChars = 1500;
    private const int MaxJournalEntries = 5;

    public string SoulFilePath => Path.Combine(_hermesHome, "SOUL.md");
    public string UserFilePath => Path.Combine(_hermesHome, "USER.md");
    public string MistakesFilePath => Path.Combine(_soulDir, "mistakes.jsonl");
    public string HabitsFilePath => Path.Combine(_soulDir, "habits.jsonl");

    public SoulService(string hermesHome, ILogger<SoulService> logger)
    {
        _hermesHome = hermesHome;
        _soulDir = Path.Combine(hermesHome, "soul");
        _logger = logger;

        Directory.CreateDirectory(_soulDir);
        EnsureDefaultTemplates();
    }

    // ── Load / Save soul files ──

    /// <summary>Load a soul file by type. Returns empty string if file doesn't exist.</summary>
    public async Task<string> LoadFileAsync(SoulFileType type, string? projectDir = null)
    {
        var path = GetFilePath(type, projectDir);
        if (!File.Exists(path)) return "";
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>Save a soul file by type.</summary>
    public async Task SaveFileAsync(SoulFileType type, string content, string? projectDir = null)
    {
        var path = GetFilePath(type, projectDir);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, content);
        _logger.LogInformation("Soul: saved {Type} to {Path}", type, path);
    }

    /// <summary>Get the file path for a soul file type.</summary>
    public string GetFilePath(SoulFileType type, string? projectDir = null)
    {
        return type switch
        {
            SoulFileType.Soul => SoulFilePath,
            SoulFileType.User => UserFilePath,
            SoulFileType.ProjectRules => projectDir is not null
                ? Path.Combine(_hermesHome, "projects", SanitizeDirName(projectDir), "AGENTS.md")
                : Path.Combine(_hermesHome, "AGENTS.md"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    // ── Mistake journal ──

    /// <summary>Record a mistake to the append-only journal.</summary>
    public async Task RecordMistakeAsync(MistakeEntry entry)
    {
        var json = JsonSerializer.Serialize(entry, JsonOpts);
        await File.AppendAllTextAsync(MistakesFilePath, json + "\n");
        _logger.LogInformation("Soul: recorded mistake — {Lesson}", entry.Lesson);
    }

    /// <summary>Load all mistakes from the journal.</summary>
    public async Task<List<MistakeEntry>> LoadMistakesAsync()
    {
        return await LoadJournalAsync<MistakeEntry>(MistakesFilePath);
    }

    // ── Habit journal ──

    /// <summary>Record a good habit to the append-only journal.</summary>
    public async Task RecordHabitAsync(HabitEntry entry)
    {
        var json = JsonSerializer.Serialize(entry, JsonOpts);
        await File.AppendAllTextAsync(HabitsFilePath, json + "\n");
        _logger.LogInformation("Soul: recorded habit — {Habit}", entry.Habit);
    }

    /// <summary>Load all habits from the journal.</summary>
    public async Task<List<HabitEntry>> LoadHabitsAsync()
    {
        return await LoadJournalAsync<HabitEntry>(HabitsFilePath);
    }

    // ── Assemble soul context for prompt injection ──

    /// <summary>
    /// Assemble the full soul context string for injection into the system prompt.
    /// Returns a single string containing identity, user profile, project rules,
    /// recent mistakes, and recent habits — capped at ~1500 tokens.
    /// </summary>
    public async Task<string> AssembleSoulContextAsync(string? projectDir = null)
    {
        var sb = new StringBuilder();

        // 1. Agent identity (SOUL.md) — always included in full
        var soul = await LoadFileAsync(SoulFileType.Soul);
        if (!string.IsNullOrWhiteSpace(soul))
        {
            sb.AppendLine("[Agent Identity]");
            sb.AppendLine(soul.Trim());
            sb.AppendLine();
        }

        // 2. User profile (USER.md) — truncated
        var user = await LoadFileAsync(SoulFileType.User);
        if (!string.IsNullOrWhiteSpace(user) && user.Trim().Length > 50) // Skip near-empty templates
        {
            sb.AppendLine("[User Profile]");
            sb.AppendLine(Truncate(user.Trim(), MaxUserChars));
            sb.AppendLine();
        }

        // 3. Project rules (AGENTS.md) — truncated
        var agents = await LoadFileAsync(SoulFileType.ProjectRules, projectDir);
        if (!string.IsNullOrWhiteSpace(agents) && agents.Trim().Length > 50)
        {
            sb.AppendLine("[Project Rules]");
            sb.AppendLine(Truncate(agents.Trim(), MaxAgentsChars));
            sb.AppendLine();
        }

        // 4. Recent mistakes (lesson only, last 5)
        var mistakes = await LoadMistakesAsync();
        if (mistakes.Count > 0)
        {
            sb.AppendLine("[Learned from Mistakes]");
            foreach (var m in mistakes.TakeLast(MaxJournalEntries))
            {
                sb.AppendLine($"- {m.Lesson}");
            }
            sb.AppendLine();
        }

        // 5. Recent habits (habit only, last 5)
        var habits = await LoadHabitsAsync();
        if (habits.Count > 0)
        {
            sb.AppendLine("[Good Habits]");
            foreach (var h in habits.TakeLast(MaxJournalEntries))
            {
                sb.AppendLine($"- {h.Habit}");
            }
            sb.AppendLine();
        }

        var result = sb.ToString();

        // Hard cap to prevent context bloat
        if (result.Length > MaxSoulContextChars)
        {
            result = result[..MaxSoulContextChars] + "\n[...soul context truncated]";
        }

        return result;
    }

    // ── Default templates ──

    private void EnsureDefaultTemplates()
    {
        if (!File.Exists(SoulFilePath))
        {
            File.WriteAllText(SoulFilePath, DefaultSoulTemplate);
            _logger.LogInformation("Soul: created default SOUL.md");
        }

        if (!File.Exists(UserFilePath))
        {
            File.WriteAllText(UserFilePath, DefaultUserTemplate);
            _logger.LogInformation("Soul: created default USER.md");
        }
    }

    // ── Helpers ──

    private static async Task<List<T>> LoadJournalAsync<T>(string path)
    {
        var entries = new List<T>();
        if (!File.Exists(path)) return entries;

        foreach (var line in await File.ReadAllLinesAsync(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<T>(line);
                if (entry is not null) entries.Add(entry);
            }
            catch
            {
                // Skip malformed lines — journal is append-only, don't break on bad data
            }
        }

        return entries;
    }

    private static string Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "\n[...truncated]";
    }

    private static string SanitizeDirName(string dir)
    {
        // Use last component of path, strip invalid chars
        var name = Path.GetFileName(dir) ?? dir;
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    // ── Default templates ──

    private const string DefaultSoulTemplate = @"# Hermes Agent Identity

You are **Hermes**, an AI coding agent built for desktop productivity.

## Personality
- Direct, efficient, and technically precise
- Proactive — anticipate what the user needs next
- Honest about limitations and uncertainties
- Warm but not verbose — respect the user's time

## Values
- **Accuracy over speed** — get it right the first time
- **Transparency** — explain reasoning, show your work
- **Safety** — never execute destructive actions without confirmation
- **Learning** — remember past mistakes and don't repeat them

## Communication Style
- Use clear, concise language
- Lead with the answer, then explain if needed
- Format code blocks with proper syntax highlighting
- When unsure, ask rather than guess

## Working Style
- Read files before editing them
- Test changes when possible
- Commit frequently with clear messages
- Respect existing code patterns and conventions
";

    private const string DefaultUserTemplate = @"# User Profile

## Expertise
<!-- What is the user's technical skill level? What languages/frameworks do they know? -->

## Preferences
<!-- How does the user prefer to work? What tools do they favor? -->

## Communication Style
<!-- Does the user prefer detailed explanations or just the answer? -->

## Past Corrections
<!-- Key corrections the user has made — patterns to remember. -->
";
}

using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VoiceBot;

/// <summary>Something the bot should do besides speaking: open an app or a web page.</summary>
public abstract record BotAction;
public sealed record OpenProgram(string FileName) : BotAction;
public sealed record OpenUrl(string Url) : BotAction;
public sealed record Quit : BotAction;

public sealed record Reply(string Text, BotAction? Action = null);

/// <summary>
/// Turns what the user said into a reply. Pure logic with an injectable clock,
/// so every command is unit tested without a microphone.
/// </summary>
public sealed class Brain
{
    private readonly Func<DateTime> _now;
    private readonly Random _random;
    private readonly List<string> _notes = new();

    public Brain(Func<DateTime>? now = null, int seed = 0)
    {
        _now = now ?? (() => DateTime.Now);
        _random = seed == 0 ? new Random() : new Random(seed);
    }

    public IReadOnlyList<string> Notes => _notes;

    /// <summary>Apps the bot can open by name, mapped to what Windows runs.</summary>
    public static readonly IReadOnlyDictionary<string, string> Programs = new Dictionary<string, string>
    {
        ["notepad"] = "notepad.exe",
        ["calculator"] = "calc.exe",
        ["paint"] = "mspaint.exe",
        ["file explorer"] = "explorer.exe",
        ["explorer"] = "explorer.exe",
        ["task manager"] = "taskmgr.exe",
        ["command prompt"] = "cmd.exe",
        ["settings"] = "ms-settings:",
        ["browser"] = "https://www.google.com",
    };

    private static readonly string[] Jokes =
    {
        "Why do programmers prefer dark mode? Because light attracts bugs.",
        "I would tell you a UDP joke, but you might not get it.",
        "There are 10 kinds of people: those who understand binary and those who don't.",
        "A SQL query walks into a bar, walks up to two tables and asks: can I join you?",
    };

    /// <summary>Phrases the speech recognizer listens for, so recognition stays accurate.</summary>
    public static IEnumerable<string> Phrases()
    {
        yield return "hello";
        yield return "what time is it";
        yield return "what is the date";
        yield return "what day is it";
        yield return "tell me a joke";
        yield return "who are you";
        yield return "help";
        yield return "read my notes";
        yield return "clear my notes";
        yield return "goodbye";
        foreach (var name in Programs.Keys) yield return "open " + name;
    }

    public Reply Respond(string input)
    {
        string text = Normalize(input);
        if (text.Length == 0) return new Reply("Sorry, I didn't catch that.");

        if (text is "goodbye" or "bye" or "exit" or "quit" or "stop")
            return new Reply("Goodbye!", new Quit());

        if (Regex.IsMatch(text, @"^(hi|hello|hey)\b"))
            return new Reply(Greeting());

        // Word boundaries, so "12 times 7" is math, not a time request.
        if (Regex.IsMatch(text, @"\btime\b"))
            return new Reply("It's " + _now().ToString("h:mm tt", CultureInfo.InvariantCulture) + ".");

        if (Regex.IsMatch(text, @"\bdate\b") || text.Contains("what day"))
            return new Reply("Today is " + _now().ToString("dddd, MMMM d", CultureInfo.InvariantCulture) + ".");

        if (text.Contains("joke"))
            return new Reply(Jokes[_random.Next(Jokes.Length)]);

        if (text.Contains("who are you") || text.Contains("your name"))
            return new Reply("I'm Voice Bot. I can tell the time, open apps, search the web, do math and keep notes.");

        if (text == "help" || text.Contains("what can you do"))
            return new Reply("Try: what time is it, open notepad, search for black holes, what is 12 times 7, note buy milk, read my notes, or goodbye.");

        var open = Regex.Match(text, @"^(open|launch|start) (.+)$");
        if (open.Success)
        {
            string name = open.Groups[2].Value;
            if (!Programs.TryGetValue(name, out var file))
                return new Reply($"I don't know how to open {name}. I can open {string.Join(", ", Programs.Keys.Take(6))} and more.");
            return file.StartsWith("http")
                ? new Reply($"Opening your {name}.", new OpenUrl(file))
                : new Reply($"Opening {name}.", new OpenProgram(file));
        }

        var search = Regex.Match(text, @"^(search|google|look up)( for)? (.+)$");
        if (search.Success)
        {
            string query = search.Groups[3].Value;
            return new Reply($"Searching for {query}.", new OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(query)));
        }

        var note = Regex.Match(text, @"^(note|remember|add note)( that)? (.+)$");
        if (note.Success)
        {
            _notes.Add(note.Groups[3].Value);
            return new Reply($"Noted. You have {_notes.Count} note{(_notes.Count == 1 ? "" : "s")}.");
        }
        if (text.Contains("read my notes") || text == "notes")
            return new Reply(_notes.Count == 0 ? "You have no notes." : "Your notes: " + string.Join(". ", _notes) + ".");
        if (text.Contains("clear my notes"))
        {
            _notes.Clear();
            return new Reply("Notes cleared.");
        }

        if (TryMath(text, out string? answer))
            return new Reply(answer!);

        return new Reply("I don't know that one yet. Say help to hear what I can do.");
    }

    private string Greeting()
    {
        int hour = _now().Hour;
        string part = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
        return part + "! How can I help?";
    }

    private static string Normalize(string input) =>
        Regex.Replace(input.ToLowerInvariant(), @"[^\w\s.+\-*/()]", "").Trim();

    /// <summary>Answers "what is 12 times 7" style questions with DataTable.Compute.</summary>
    private static bool TryMath(string text, out string? answer)
    {
        answer = null;
        var m = Regex.Match(text, @"^(what is|what's|calculate|whats)\s+(.+)$");
        if (!m.Success) return false;
        string expr = m.Groups[2].Value
            .Replace("plus", "+").Replace("minus", "-")
            .Replace("times", "*").Replace("multiplied by", "*")
            .Replace("divided by", "/").Replace("over", "/");
        // Only digits and operators reach Compute, so nothing else can be evaluated.
        if (!Regex.IsMatch(expr, @"^[\d\s.+\-*/()]+$")) return false;
        try
        {
            var value = Convert.ToDouble(new DataTable().Compute(expr, null), CultureInfo.InvariantCulture);
            if (double.IsInfinity(value) || double.IsNaN(value)) { answer = "That has no answer. Did you divide by zero?"; return true; }
            answer = $"That's {Math.Round(value, 4).ToString(CultureInfo.InvariantCulture)}.";
            return true;
        }
        catch (Exception e) when (e is EvaluateException or SyntaxErrorException or InvalidCastException or DivideByZeroException)
        {
            return false;
        }
    }
}

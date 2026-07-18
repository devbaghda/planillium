namespace MentorOverseer.App.Services;

/// <summary>
/// Window-title → app/sub-item parsing for reports. Port of main.py's
/// _strip_telegram_title / _normalise_title / _get_app_group / _get_app_sub,
/// so rows logged by either tracker group identically. Display format is
/// "App - name" (e.g. "Telegram - Моя Шушука", "Chrome - YouTube") — grouping
/// by that label folds e.g. every YouTube video into one "Chrome - YouTube".
/// </summary>
public static class AppNames
{
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "google chrome", "chrome", "mozilla firefox", "firefox",
        "microsoft edge", "edge", "safari", "opera",
        "brave", "brave browser", "chromium",
    };

    // Shared with ActivityTracker via MessengerApps.DisplayNames — see its doc comment
    // (round-5 audit finding #20; centralized round-7 to stop the two lists needing to
    // be kept in sync by hand).
    private static readonly IReadOnlySet<string> Messengers = MessengerApps.DisplayNames;

    // Verbose app suffixes → clean display names (superset of main.py's map:
    // browsers added so the label reads "Chrome - YouTube", not
    // "Google Chrome - YouTube").
    private static readonly Dictionary<string, string> Normalise = new(StringComparer.OrdinalIgnoreCase)
    {
        ["google chrome"] = "Chrome",
        ["microsoft edge"] = "Edge",
        ["mozilla firefox"] = "Firefox",
        ["brave browser"] = "Brave",
        ["visual studio code"] = "VS Code",
        ["adobe acrobat reader dc"] = "Adobe Acrobat",
        ["adobe acrobat pro dc"] = "Adobe Acrobat",
        ["adobe acrobat dc"] = "Adobe Acrobat",
        ["adobe acrobat reader"] = "Adobe Acrobat",
        ["adobe acrobat pro"] = "Adobe Acrobat",
        ["adobe photoshop 2024"] = "Photoshop",
        ["adobe photoshop 2025"] = "Photoshop",
        ["adobe photoshop 2026"] = "Photoshop",
        ["adobe photoshop"] = "Photoshop",
        ["adobe illustrator"] = "Illustrator",
        ["adobe indesign"] = "InDesign",
        ["adobe premiere pro"] = "Premiere Pro",
        ["adobe after effects"] = "After Effects",
        ["adobe lightroom classic"] = "Lightroom",
        ["adobe lightroom"] = "Lightroom",
        ["microsoft excel"] = "Excel",
        ["microsoft word"] = "Word",
        ["microsoft powerpoint"] = "PowerPoint",
    };

    // Apps whose open filename is a meaningful sub-item.
    private static readonly HashSet<string> FileSubApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "excel", "word", "powerpoint", "onenote", "publisher", "access",
        "adobe acrobat", "photoshop", "illustrator", "indesign",
        "premiere pro", "after effects", "lightroom",
    };

    private static readonly HashSet<string> VsCodeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "vs code", "visual studio code",
    };

    // The three dash characters this app treats as a title separator — previously listed
    // independently three times below (here padded as " x "; Telegram-title stripping as
    // " x ("; badge-count stripping as "x "), agreeing only by coincidence
    // (2026-07-18 audit finding R10-06, this project's recurring duplicated-lookup-table
    // pattern). One source, three derived variants.
    private static readonly char[] DashChars = { '-', '—', '–' };
    private static readonly string[] Separators = DashChars.Select(c => $" {c} ").ToArray();
    private const char Lrm = '‎';
    private const char Rlm = '‏';

    /// <summary>Group key + display label: "App - sub" when a sub exists.</summary>
    public static string Label(string window, int maxLen = 60)
    {
        var group = Group(window);
        if (group == "—") return "—";
        var sub = Sub(window);
        var label = sub is null ? group : $"{group} - {sub}";
        return label.Length <= maxLen ? label : label[..maxLen];
    }

    /// <summary>Base application name — the top-level group in reports.</summary>
    public static string Group(string window)
    {
        if (string.IsNullOrEmpty(window)) return "—";
        if (IsTelegramMarked(window)) return "Telegram";
        var norm = NormaliseTitle(window);
        if (norm.Length == 0) return "—";
        var parts = SplitParts(norm);
        var raw = parts.Length > 0 ? parts[^1] : norm;
        return Normalise.TryGetValue(raw, out var clean) ? clean : raw;
    }

    /// <summary>Chat / site / project / filename sub-item, or null.</summary>
    public static string? Sub(string window)
    {
        if (string.IsNullOrEmpty(window)) return null;
        if (IsTelegramMarked(window))
        {
            var chat = StripTelegramTitle(window);
            return chat.Length > 0 ? chat : null;
        }
        var norm = NormaliseTitle(window);
        if (norm.Length == 0) return null;
        var parts = SplitParts(norm);
        if (parts.Length < 2) return null;

        var rawLast = parts[^1];
        var appGroup = Normalise.TryGetValue(rawLast, out var clean) ? clean : rawLast;

        if (Browsers.Contains(rawLast)) return parts[^2];

        if (Messengers.Contains(rawLast))
        {
            // Badge-stripping at write time can miss a locale-formatted unread
            // count — strip again at read time so old rows group cleanly too.
            var sub = ActivityTracker.StripUnreadBadge(parts[0]);
            if (sub.Length == 0 || sub.Equals(rawLast, StringComparison.OrdinalIgnoreCase))
                return null;
            return sub;
        }

        if (VsCodeNames.Contains(appGroup))
        {
            var project = parts.Length >= 3 ? parts[^2] : parts[0];
            project = project.TrimStart('●', '•', '·').Trim();
            return project.Length > 0 ? project : null;
        }

        if (FileSubApps.Contains(appGroup))
        {
            var filename = parts[0];
            while (filename.StartsWith('[') && filename.Contains(']'))
                filename = filename[(filename.IndexOf(']') + 1)..].Trim();
            var atIdx = filename.IndexOf(" @", StringComparison.Ordinal);
            if (atIdx != -1) filename = filename[..atIdx].Trim();
            return filename.Length > 0 ? filename : null;
        }

        return null;
    }

    private static string[] SplitParts(string norm)
    {
        foreach (var sep in Separators)
            if (norm.Contains(sep))
                return norm.Split(sep, StringSplitOptions.RemoveEmptyEntries |
                                       StringSplitOptions.TrimEntries);
        return new[] { norm };
    }

    private static bool IsTelegramMarked(string title) =>
        title.Length > 0 && (title[0] == Lrm || title[0] == Rlm);

    /// <summary>Telegram prefixes titles with an LTR mark: "‎CHAT – (N)".</summary>
    private static string StripTelegramTitle(string title)
    {
        var t = title.TrimStart(Lrm, Rlm).Trim();
        foreach (var sep in DashChars.Select(c => $" {c} ("))
        {
            var idx = t.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx == -1) continue;
            var candidate = t[(idx + sep.Length)..].TrimEnd();
            if (candidate.EndsWith(')') && candidate[..^1].All(char.IsDigit))
                return t[..idx].Trim();
        }
        return t;
    }

    /// <summary>Unread-count changes must not split an app across groups.</summary>
    private static string NormaliseTitle(string title)
    {
        if (IsTelegramMarked(title)) return StripTelegramTitle(title);
        var t = title.Trim();
        if (t.StartsWith('(') && t.Contains(')'))
        {
            var inner = t[1..t.IndexOf(')')];
            if (inner.Length > 0 && inner.All(char.IsDigit))
            {
                var after = t[(t.IndexOf(')') + 1)..].Trim();
                foreach (var sep in DashChars.Select(c => $"{c} "))
                    if (after.StartsWith(sep, StringComparison.Ordinal))
                    {
                        after = after[sep.Length..].Trim();
                        break;
                    }
                return after;
            }
        }
        return t;
    }
}

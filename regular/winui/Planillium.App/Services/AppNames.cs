namespace Planillium.App.Services;

/// <summary>
/// Window-title → app/sub-item parsing for reports. Port of main.py's
/// _strip_telegram_title / _normalise_title / _get_app_group / _get_app_sub,
/// so rows logged by either tracker group identically. Display format is
/// "App - name" (e.g. "Telegram - Моя Шушука", "Chrome - YouTube") — grouping
/// by that label folds e.g. every YouTube video into one "Chrome - YouTube".
/// </summary>
public static class AppNames
{
    // internal, not private: ConfigService.LearnActivityRule reuses this exact set to refuse
    // teaching a bare browser name as an on-plan/off-plan keyword (2026-07-28 request) — a
    // browser hosts both on-plan and off-plan content depending on the tab, so "Chrome" alone
    // is too wide to mean anything as a classification rule, unlike "Chrome - LinkedIn".
    internal static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>The "Other" entry of the Diary's App filter (G10).</summary>
    public const string OtherGroup = "Other";

    /// <summary>
    /// App-filter key (G10): like <see cref="Group"/>, but a window title with no dash separator
    /// (so no program name could be read from it) and not a known program name is folded into
    /// <see cref="OtherGroup"/>. Telegram rows and known programs/browsers/messengers keep their own entry.
    /// "—" (no window text) is unchanged.
    /// </summary>
    public static string FilterGroup(string window)
    {
        var g = Group(window);
        if (g == "—" || IsTelegramMarked(window)) return g;
        if (SplitParts(NormaliseTitle(window)).Length > 1) return g;
        if (Normalise.ContainsKey(g) || Normalise.ContainsValue(g) || Browsers.Contains(g) || Messengers.Contains(g))
            return g;
        return OtherGroup;
    }

    /// <summary>
    /// 2026-11-05 (user: the apps filter listed names of dialog windows): a row whose title has no readable
    /// program name (<see cref="FilterGroup"/> = "Other") takes the app of the row just before it in time,
    /// since a dialog belongs to the app that opened it. The first row, idle rows and "—" rows keep their
    /// own value. Display and filtering only; nothing stored changes. Returns Id → app.
    /// </summary>
    public static Dictionary<long, string> InheritedFilterGroups(IEnumerable<(long Id, DateOnly Date, string Start, string Window)> rows)
    {
        var map = new Dictionary<long, string>();
        string? prev = null;
        foreach (var r in rows.OrderBy(x => x.Date).ThenBy(x => x.Start, StringComparer.Ordinal).ThenBy(x => x.Id))
        {
            var g = FilterGroup(r.Window);
            var skip = g == "—" || r.Window == "idle";
            if (!skip && g == OtherGroup && prev != null) g = prev;
            map[r.Id] = g;
            if (!skip) prev = g;
        }
        return map;
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

        // Windows 11's tabbed File Explorer titles as "FolderName - File Explorer"
        // (or "FolderName and 1 more tab - File Explorer") — the folder/tab name is
        // the sub-item, same role a filename plays for FileSubApps above. Without
        // this, every File Explorer row collapsed to the bare app name in reports,
        // even though the folder name was captured and stored (2026-07-20 report).
        if (appGroup.Equals("File Explorer", StringComparison.OrdinalIgnoreCase))
        {
            var location = parts[0];
            return location.Length > 0 ? location : null;
        }

        return null;
    }

    private static string[] SplitParts(string norm)
    {
        // G9a: a stored messenger title is "<chat> – Telegram". A chat name that itself contains a dash
        // ("Team - Sales", "Anna – work") used to be cut at its own dash, so the app came out as
        // "Sales – Telegram" instead of "Telegram". When the title ends in " <dash> <messenger>", cut at THAT
        // last separator only, so the whole chat name is kept as the first part.
        foreach (var sep in Separators)
        {
            var idx = norm.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx <= 0) continue;
            var tail = norm[(idx + sep.Length)..].Trim();
            if (Messengers.Contains(tail))
            {
                var head = norm[..idx].Trim();
                if (head.Length > 0) return new[] { head, tail };
            }
        }
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
        // G11: "- Telegram" / "– Telegram" is a messenger title with no chat name (the empty head was trimmed off),
        // so it is that messenger, not an unknown program called "- Telegram" (it landed under "Other").
        if (t.Length > 2 && Array.IndexOf(DashChars, t[0]) >= 0 && char.IsWhiteSpace(t[1])
            && t[1..].Trim() is var rest && Messengers.Contains(rest))
            return rest;
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

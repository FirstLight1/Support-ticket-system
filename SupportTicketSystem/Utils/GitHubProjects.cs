using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SupportTicketSystem.Utils;

/// <summary>
/// The author's GitHub projects a ticket can be associated with. The list is hardcoded on
/// purpose: it is small and changes rarely, so we keep it in code rather than calling the
/// GitHub API at runtime. Forks, school assignments and the profile repo are intentionally
/// left out. To add or remove a project, edit <see cref="All"/> — nothing else needs to change.
/// </summary>
public static class GitHubProjects
{
    /// <summary>A selectable project: its repo name and the GitHub URL we link to.</summary>
    public sealed record Project(string Name, string Url);

    private const string Owner = "https://github.com/FirstLight1";

    public static readonly IReadOnlyList<Project> All = new List<Project>
    {
        new("Support-ticket-system", $"{Owner}/Support-ticket-system"),
        new("tradeTracker", $"{Owner}/tradeTracker"),
        new("tradeTracker-private", $"{Owner}/tradeTracker-private"),
        new("pokemon_pricer", $"{Owner}/pokemon_pricer"),
        new("pokemon-card-merger", $"{Owner}/pokemon-card-merger"),
        new("pokemonTCGexpansions", $"{Owner}/pokemonTCGexpansions"),
        new("pokemonCardAutoPricer", $"{Owner}/pokemonCardAutoPricer"),
        new("cardmarket-autologin", $"{Owner}/cardmarket-autologin"),
        new("AIS-autologin", $"{Owner}/AIS-autologin"),
        new("dm-filter", $"{Owner}/dm-filter"),
        new("ExtractDataFromTable", $"{Owner}/ExtractDataFromTable"),
        new("kamenlukas.github.io", $"{Owner}/kamenlukas.github.io"),
        new("ps_scripts", $"{Owner}/ps_scripts"),
        new("nvim-config", $"{Owner}/nvim-config"),
        new("dot-files", $"{Owner}/dot-files"),
    };

    /// <summary>The GitHub URL for a project by name, or <c>null</c> if the name isn't a known project.</summary>
    public static string? UrlFor(string? name) =>
        string.IsNullOrEmpty(name) ? null : All.FirstOrDefault(p => p.Name == name)?.Url;

    /// <summary>
    /// Dropdown items for the project picker. The matching option is auto-selected by the
    /// <c>asp-for</c> tag helper from the bound model value, so no explicit selection is needed.
    /// </summary>
    public static List<SelectListItem> SelectList() =>
        All.Select(p => new SelectListItem(p.Name, p.Name)).ToList();
}

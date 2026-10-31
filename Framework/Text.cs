using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace HeirloomTools.Framework;

/// <summary>Builds the player-facing text for a tool's record.</summary>
public class Text
{
    /*********
    ** Fields
    *********/
    private readonly ITranslationHelper I18n;


    /*********
    ** Public methods
    *********/
    public Text(ITranslationHelper i18n)
    {
        this.I18n = i18n;
    }

    public string Get(string key, object? tokens = null)
    {
        return this.I18n.Get(key, tokens);
    }

    /// <summary>Format a day as <c>Fall 12, Y2</c>.</summary>
    public string Date(int daysSinceStart)
    {
        SDate date = SDate.FromDaysSinceStart(Math.Max(1, daysSinceStart));
        return this.I18n.Get("date", new { season = Utility.getSeasonNameFromNumber(date.SeasonIndex), day = date.Day, year = date.Year });
    }

    public string Number(long value)
    {
        return Utility.getNumberWithCommas((int)Math.Min(int.MaxValue, value));
    }

    /// <summary>A line for one stat, like <c>Tilled 18,402 tiles</c>.</summary>
    public string Stat(string stat, long count)
    {
        string key = count == 1 ? $"stat.{stat}.one" : $"stat.{stat}";
        return this.I18n.Get(key, new { count = this.Number(count) });
    }

    /// <summary>Get the stat lines for a tool, skipping empty stats.</summary>
    public List<string> StatLines(Tool tool)
    {
        List<string> lines = new();
        foreach (string stat in ToolStats.GetStatsFor(tool))
        {
            long count = HeirloomRecord.GetCount(tool, stat);
            if (count > 0)
                lines.Add(this.Stat(stat, count));
        }

        if (tool is FishingRod && HeirloomRecord.GetBestFish(tool) is { } best)
            lines.Add(this.I18n.Get("stat.BestFish", new { fish = ItemName(best.Id), size = this.FishSize(best.Size) }));

        if (tool is MeleeWeapon or Slingshot)
        {
            var top = HeirloomRecord.GetKillsBy(tool).OrderByDescending(p => p.Value).FirstOrDefault();
            if (top.Key != null && top.Value >= 10)
                lines.Add(this.I18n.Get("stat.TopFoe", new { monster = Monster.GetDisplayName(top.Key), count = this.Number(top.Value) }));
        }

        return lines;
    }

    /// <summary>A line for a moment, like <c>Found a Dinosaur Egg on Fall 12, Y2</c>.</summary>
    public string Moment(Tool tool, Moment moment)
    {
        string date = this.Date(moment.Day);
        string item = moment.Item != null ? ItemName(moment.Item) : "";
        return moment.Kind switch
        {
            MomentKinds.Milestone => this.I18n.Get($"moment.Milestone.{moment.Stat}", new { count = this.Number(moment.Value), date }),
            MomentKinds.Upgraded => this.I18n.Get("moment.Upgraded", new { item, date }),
            MomentKinds.Named => this.I18n.Get("moment.Named", new { date }),
            _ => this.I18n.Get($"moment.{moment.Kind}", new { item = this.WithArticle(item), date })
        };
    }

    /// <summary>The closing line, like <c>With you since Spring 1, Y1</c>.</summary>
    public string Since(Tool tool)
    {
        string key = HeirloomRecord.IsRetro(tool) ? "since.retro" : "since";
        return this.I18n.Get(key, new { date = this.Date(HeirloomRecord.GetSince(tool)) });
    }

    /// <summary>The HUD message for reaching a milestone.</summary>
    public string MilestoneMessage(Tool tool, string stat, long milestone)
    {
        return this.I18n.Get($"hud.Milestone.{stat}", new { tool = tool.DisplayName, count = this.Number(milestone) });
    }

    public static string ItemName(string qualifiedItemId)
    {
        return ItemRegistry.GetDataOrErrorItem(qualifiedItemId).DisplayName;
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Format a fish size the way the game does (inches, or centimetres in metric languages).</summary>
    private string FishSize(int inches)
    {
        if (LocalizedContentManager.CurrentLanguageCode != LocalizedContentManager.LanguageCode.en)
            return this.I18n.Get("size.cm", new { size = Math.Round(inches * 2.54) });
        return this.I18n.Get("size.in", new { size = inches });
    }

    /// <summary>Add "a" or "an" before an item name in English. Other languages put the name in their translation as is.</summary>
    private string WithArticle(string name)
    {
        if (LocalizedContentManager.CurrentLanguageCode != LocalizedContentManager.LanguageCode.en || name.Length == 0 || name.StartsWith("The ", StringComparison.Ordinal))
            return name;
        return ("AEIOU".IndexOf(char.ToUpperInvariant(name[0])) >= 0 ? "an " : "a ") + name;
    }
}

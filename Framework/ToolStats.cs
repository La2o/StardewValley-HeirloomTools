using System.Collections.Generic;
using StardewValley;
using StardewValley.Tools;

namespace HeirloomTools.Framework;

/// <summary>The counters a tool can keep, and which ones each kind of tool shows.</summary>
public static class ToolStats
{
    /*********
    ** Stat keys
    *********/
    public const string Tilled = "Tilled";
    public const string ArtifactSpots = "ArtifactSpots";
    public const string Watered = "Watered";
    public const string Refills = "Refills";
    public const string Rocks = "Rocks";
    public const string Geodes = "Geodes";
    public const string Trees = "Trees";
    public const string Stumps = "Stumps";
    public const string Fish = "Fish";
    public const string Perfect = "Perfect";
    public const string Treasure = "Treasure";
    public const string Trash = "Trash";
    public const string Kills = "Kills";
    public const string Grass = "Grass";
    public const string Weeds = "Weeds";
    public const string Pans = "Pans";
    public const string Milked = "Milked";
    public const string Sheared = "Sheared";
    public const string Warps = "Warps";

    /// <summary>Counts that mark a milestone moment when a tool's main stat reaches them.</summary>
    public static readonly long[] Milestones = { 100, 1_000, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, 1_000_000 };


    /*********
    ** Public methods
    *********/
    /// <summary>Whether the mod keeps a record for this item.</summary>
    public static bool IsTracked(Item? item)
    {
        return item is Hoe or Pickaxe or Axe or WateringCan or FishingRod or MeleeWeapon or Slingshot or Pan or MilkPail or Shears or Wand;
    }

    /// <summary>The stats shown for a tool, in display order. The first is its main stat, which gets milestone moments.</summary>
    public static IReadOnlyList<string> GetStatsFor(Item item)
    {
        return item switch
        {
            Hoe => new[] { Tilled, ArtifactSpots },
            WateringCan => new[] { Watered, Refills },
            Pickaxe => new[] { Rocks, Geodes },
            Axe => new[] { Trees, Stumps },
            FishingRod => new[] { Fish, Perfect, Treasure, Trash },
            MeleeWeapon weapon when weapon.isScythe() => new[] { Grass, Weeds, Kills },
            MeleeWeapon => new[] { Kills, Weeds },
            Slingshot => new[] { Kills },
            Pan => new[] { Pans },
            MilkPail => new[] { Milked },
            Shears => new[] { Sheared },
            Wand => new[] { Warps },
            _ => new string[0]
        };
    }

    /// <summary>Get the main stat for a tool, which gets milestone moments.</summary>
    public static string? GetMainStat(Item item)
    {
        IReadOnlyList<string> stats = GetStatsFor(item);
        return stats.Count > 0 ? stats[0] : null;
    }
}

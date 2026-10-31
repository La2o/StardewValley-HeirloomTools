using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Tools;

namespace HeirloomTools.Framework;

/// <summary>Writes what the player does with a tool into the tool's record.</summary>
public class Recorder
{
    /*********
    ** Fields
    *********/
    private readonly IMonitor Monitor;
    private readonly Func<ModConfig> Config;
    private readonly Text Text;

    /// <summary>Finds that always get a moment.</summary>
    private static readonly HashSet<string> RareFinds = new()
    {
        "(O)74",  // Prismatic Shard
        "(O)107", // Dinosaur Egg
        "(O)114", // Ancient Seed
        "(O)124", // Golden Mask
        "(O)125", // Golden Relic
        "(O)126", // Strange Doll (green)
        "(O)127", // Strange Doll (yellow)
        "(O)166", // Treasure Chest
        "(O)279", // Magic Rock Candy
        "(O)373", // Golden Pumpkin
        "(O)797"  // Pearl
    };

    /// <summary>Finds that get a moment the first time a tool finds them.</summary>
    private static readonly HashSet<string> FirstTimeFinds = new()
    {
        "(O)72" // Diamond
    };

    /// <summary>Geodes, which the pickaxe counts.</summary>
    private static readonly HashSet<string> Geodes = new()
    {
        "(O)535", // Geode
        "(O)536", // Frozen Geode
        "(O)537", // Magma Geode
        "(O)749", // Omni Geode
        "(O)275", // Artifact Trove
        "(O)MysteryBox",
        "(O)GoldenMysteryBox"
    };

    /// <summary>Legendary fish, for catches the game doesn't flag as boss fish.</summary>
    private static readonly HashSet<string> LegendaryFish = new()
    {
        "(O)159", "(O)160", "(O)163", "(O)682", "(O)775",
        "(O)898", "(O)899", "(O)900", "(O)901", "(O)902"
    };


    /*********
    ** Public methods
    *********/
    public Recorder(IMonitor monitor, Func<ModConfig> config, Text text)
    {
        this.Monitor = monitor;
        this.Config = config;
        this.Text = text;
    }

    /// <summary>Make sure a tool has a record before writing to it.</summary>
    /// <remarks>Every tool that existed before the mod was installed is stamped when the save loads, and new ones when they enter the inventory. One that reaches here without a record is new but was used before the inventory change was noticed.</remarks>
    public void EnsureStarted(Tool tool)
    {
        HeirloomRecord.Start(tool, HeirloomRecord.Today(), retro: false);
    }

    /// <summary>Add to one of a tool's counters.</summary>
    public void AddStat(Tool? tool, string stat, long amount = 1)
    {
        if (tool is null || amount <= 0 || !ToolStats.IsTracked(tool))
            return;

        this.EnsureStarted(tool);
        long before = HeirloomRecord.GetCount(tool, stat);
        long after = HeirloomRecord.AddCount(tool, stat, amount);

        if (stat != ToolStats.GetMainStat(tool))
            return;
        foreach (long milestone in ToolStats.Milestones)
        {
            if (before < milestone && after >= milestone)
            {
                HeirloomRecord.AddMoment(tool, new Moment { Kind = MomentKinds.Milestone, Stat = stat, Value = milestone, Day = HeirloomRecord.Today() });
                if (this.Config().MilestoneMessages)
                    Game1.addHUDMessage(new HUDMessage(this.Text.MilestoneMessage(tool, stat, milestone), HUDMessage.achievement_type));
            }
        }
    }

    /// <summary>Record items that a tool dug up, broke loose or knocked out of a monster.</summary>
    public void RecordFinds(Tool? tool, IEnumerable<string> qualifiedItemIds)
    {
        if (tool is null || !ToolStats.IsTracked(tool))
            return;

        foreach (string id in qualifiedItemIds)
        {
            if (tool is Pickaxe && Geodes.Contains(id))
                this.AddStat(tool, ToolStats.Geodes);

            if (RareFinds.Contains(id) || (FirstTimeFinds.Contains(id) && !HeirloomRecord.HasMoment(tool, MomentKinds.Found, id)))
            {
                this.AddMoment(tool, MomentKinds.Found, id);
                continue;
            }

            ParsedItemData? data = ItemRegistry.GetData(id);
            if (data?.ObjectType == "Arch" && !HeirloomRecord.HasMoment(tool, MomentKinds.FirstArtifact))
                this.AddMoment(tool, MomentKinds.FirstArtifact, id);
        }
    }

    /// <summary>Record a catch with a fishing rod.</summary>
    public void RecordCatch(FishingRod rod, string fishId, int size, bool perfect, bool treasure, bool bossFish, int count)
    {
        ParsedItemData? data = ItemRegistry.GetData(fishId);
        if (data is null)
            return;

        if (data.Category == StardewValley.Object.FishCategory)
        {
            bool first = HeirloomRecord.GetCount(rod, ToolStats.Fish) == 0;
            this.AddStat(rod, ToolStats.Fish, Math.Max(1, count));
            if (perfect)
                this.AddStat(rod, ToolStats.Perfect);
            HeirloomRecord.TrySetBestFish(rod, data.QualifiedItemId, size);

            if (bossFish || LegendaryFish.Contains(data.QualifiedItemId))
                this.AddMoment(rod, MomentKinds.Legendary, data.QualifiedItemId);
            else if (first)
                this.AddMoment(rod, MomentKinds.FirstFish, data.QualifiedItemId);
        }
        else if (data.Category == StardewValley.Object.junkCategory)
            this.AddStat(rod, ToolStats.Trash, Math.Max(1, count));
        else
            this.RecordFinds(rod, new[] { data.QualifiedItemId });

        if (treasure)
            this.AddStat(rod, ToolStats.Treasure);
    }

    /// <summary>Record a monster slain with a weapon.</summary>
    public void RecordKill(Tool weapon, string monsterName)
    {
        this.AddStat(weapon, ToolStats.Kills);
        if (!string.IsNullOrEmpty(monsterName))
            HeirloomRecord.AddKill(weapon, monsterName);
    }

    /// <summary>Carry the record over when Clint upgrades a tool, and note the upgrade.</summary>
    public void RecordUpgrade(Tool from, Tool to)
    {
        if (!HeirloomRecord.IsStarted(from))
            return;

        HeirloomRecord.CopyTo(from, to);
        HeirloomRecord.AddMoment(to, new Moment { Kind = MomentKinds.Upgraded, Item = to.QualifiedItemId, Value = to.UpgradeLevel, Day = HeirloomRecord.Today() });
    }

    /// <summary>Name a tool, or clear its name.</summary>
    public void Rename(Tool tool, string? name)
    {
        this.EnsureStarted(tool);
        string? oldName = HeirloomRecord.GetName(tool);
        HeirloomRecord.SetName(tool, name);

        string? newName = HeirloomRecord.GetName(tool);
        if (newName != null && oldName is null)
            HeirloomRecord.AddMoment(tool, new Moment { Kind = MomentKinds.Named, Day = HeirloomRecord.Today() });
    }


    /*********
    ** Private methods
    *********/
    private void AddMoment(Tool tool, string kind, string itemId)
    {
        this.EnsureStarted(tool);
        HeirloomRecord.AddMoment(tool, new Moment { Kind = kind, Item = itemId, Day = HeirloomRecord.Today() });
        this.Monitor.Log($"{tool.Name}: {kind} {itemId}");
    }
}

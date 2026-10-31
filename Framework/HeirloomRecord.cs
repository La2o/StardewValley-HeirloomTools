using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace HeirloomTools.Framework;

/// <summary>A notable moment in a tool's life, like a rare find or a milestone.</summary>
public class Moment
{
    /// <summary>What kind of moment this is (see <see cref="MomentKinds"/>).</summary>
    public string Kind { get; set; } = "";

    /// <summary>The qualified item ID involved, if any.</summary>
    public string? Item { get; set; }

    /// <summary>The stat involved, for milestones.</summary>
    public string? Stat { get; set; }

    /// <summary>A number for the moment, like the milestone count or the tool's new upgrade level.</summary>
    public long Value { get; set; }

    /// <summary>When it happened, as <see cref="SDate.DaysSinceStart"/>.</summary>
    public int Day { get; set; }
}

/// <summary>The kinds of <see cref="Moment"/>.</summary>
public static class MomentKinds
{
    public const string Found = "Found";
    public const string FirstArtifact = "FirstArtifact";
    public const string FirstFish = "FirstFish";
    public const string Legendary = "Legendary";
    public const string Milestone = "Milestone";
    public const string Upgraded = "Upgraded";
    public const string Named = "Named";
}

/// <summary>Reads and writes a tool's lifetime record, which lives in the tool's <see cref="Item.modData"/> so it's saved with the tool and moves wherever the tool goes.</summary>
public static class HeirloomRecord
{
    /*********
    ** Fields
    *********/
    public const string Prefix = "ygsnaffy.HeirloomTools/";
    private const string SinceKey = Prefix + "Since";
    private const string RetroKey = Prefix + "Retro";
    private const string NameKey = Prefix + "Name";
    private const string MomentsKey = Prefix + "Moments";
    private const string KillsByKey = Prefix + "KillsBy";
    private const string BestFishSizeKey = Prefix + "BestFishSize";
    private const string BestFishKey = Prefix + "BestFish";
    private const string CountPrefix = Prefix + "Count/";

    /// <summary>The most moments kept per tool. The oldest are dropped first.</summary>
    public const int MaxMoments = 40;

    /// <summary>A small cache of parsed JSON, since tooltips read the record every frame.</summary>
    private static string? CachedMomentsRaw;
    private static List<Moment> CachedMoments = new();
    private static string? CachedKillsRaw;
    private static Dictionary<string, long> CachedKills = new();


    /*********
    ** Public methods
    *********/
    /// <summary>Whether the tool has started its record.</summary>
    public static bool IsStarted(Item item)
    {
        return item.modData.ContainsKey(SinceKey);
    }

    /// <summary>Start the tool's record if it hasn't started yet.</summary>
    /// <param name="item">The tool.</param>
    /// <param name="day">The day it joined the player, as <see cref="SDate.DaysSinceStart"/>.</param>
    /// <param name="retro">Whether the tool was already owned before the mod was installed, so the date is when records began rather than when it joined.</param>
    public static bool Start(Item item, int day, bool retro)
    {
        if (IsStarted(item))
            return false;

        item.modData[SinceKey] = day.ToString(CultureInfo.InvariantCulture);
        if (retro)
            item.modData[RetroKey] = "1";
        return true;
    }

    public static int GetSince(Item item)
    {
        return item.modData.TryGetValue(SinceKey, out string raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day) ? day : 0;
    }

    public static bool IsRetro(Item item)
    {
        return item.modData.ContainsKey(RetroKey);
    }

    public static string? GetName(Item item)
    {
        return item.modData.TryGetValue(NameKey, out string name) && !string.IsNullOrWhiteSpace(name) ? name : null;
    }

    public static void SetName(Item item, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            item.modData.Remove(NameKey);
        else
            item.modData[NameKey] = name.Trim();
    }

    public static long GetCount(Item item, string stat)
    {
        return item.modData.TryGetValue(CountPrefix + stat, out string raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;
    }

    /// <summary>Add to a counter and return the new value.</summary>
    public static long AddCount(Item item, string stat, long amount)
    {
        long value = Math.Max(0, GetCount(item, stat) + amount);
        item.modData[CountPrefix + stat] = value.ToString(CultureInfo.InvariantCulture);
        return value;
    }

    public static IReadOnlyList<Moment> GetMoments(Item item)
    {
        if (!item.modData.TryGetValue(MomentsKey, out string raw) || string.IsNullOrEmpty(raw))
            return Array.Empty<Moment>();

        if (raw != CachedMomentsRaw)
        {
            CachedMoments = TryDeserialize<List<Moment>>(raw) ?? new List<Moment>();
            CachedMomentsRaw = raw;
        }
        return CachedMoments;
    }

    public static void AddMoment(Item item, Moment moment)
    {
        List<Moment> moments = new(GetMoments(item)) { moment };
        if (moments.Count > MaxMoments)
            moments.RemoveRange(0, moments.Count - MaxMoments);
        item.modData[MomentsKey] = JsonSerializer.Serialize(moments);
    }

    public static bool HasMoment(Item item, string kind, string? itemId = null)
    {
        foreach (Moment moment in GetMoments(item))
        {
            if (moment.Kind == kind && (itemId is null || moment.Item == itemId))
                return true;
        }
        return false;
    }

    public static IReadOnlyDictionary<string, long> GetKillsBy(Item item)
    {
        if (!item.modData.TryGetValue(KillsByKey, out string raw) || string.IsNullOrEmpty(raw))
            return new Dictionary<string, long>();

        if (raw != CachedKillsRaw)
        {
            CachedKills = TryDeserialize<Dictionary<string, long>>(raw) ?? new Dictionary<string, long>();
            CachedKillsRaw = raw;
        }
        return CachedKills;
    }

    public static void AddKill(Item item, string monsterName)
    {
        Dictionary<string, long> kills = new(GetKillsBy(item));
        kills[monsterName] = kills.TryGetValue(monsterName, out long count) ? count + 1 : 1;
        item.modData[KillsByKey] = JsonSerializer.Serialize(kills);
    }

    /// <summary>Get the biggest fish caught, if any.</summary>
    public static (string Id, int Size)? GetBestFish(Item item)
    {
        if (item.modData.TryGetValue(BestFishKey, out string id) && item.modData.TryGetValue(BestFishSizeKey, out string rawSize) && int.TryParse(rawSize, out int size))
            return (id, size);
        return null;
    }

    /// <summary>Record a catch and return whether it's the new biggest.</summary>
    public static bool TrySetBestFish(Item item, string fishId, int size)
    {
        if (size <= 0 || GetBestFish(item) is { } best && best.Size >= size)
            return false;

        item.modData[BestFishKey] = fishId;
        item.modData[BestFishSizeKey] = size.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Copy the record from one tool to another, for when Clint upgrades a tool.</summary>
    public static void CopyTo(Item from, Item to)
    {
        foreach (string key in from.modData.Keys)
        {
            if (key.StartsWith(Prefix, StringComparison.Ordinal))
                to.modData[key] = from.modData[key];
        }
    }

    /// <summary>Remove the whole record from a tool.</summary>
    public static void Clear(Item item)
    {
        List<string> keys = new();
        foreach (string key in item.modData.Keys)
        {
            if (key.StartsWith(Prefix, StringComparison.Ordinal))
                keys.Add(key);
        }
        foreach (string key in keys)
            item.modData.Remove(key);
    }

    public static int Today()
    {
        return SDate.Now().DaysSinceStart;
    }


    /*********
    ** Private methods
    *********/
    private static T? TryDeserialize<T>(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(raw);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

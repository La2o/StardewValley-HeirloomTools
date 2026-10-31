using System;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using HeirloomTools.Integrations;

namespace HeirloomTools.Framework;

/// <summary>Registers the config with Generic Mod Config Menu, if it's installed.</summary>
public static class GmcmIntegration
{
    public static void Register(IModHelper helper, IManifest manifest, Func<ModConfig> getConfig, Action reset, Action save)
    {
        IGenericModConfigMenuApi? api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
            return;

        ITranslationHelper i18n = helper.Translation;
        api.Register(manifest, reset, save);

        api.AddSectionTitle(manifest, () => i18n.Get("config.section.tooltip"));
        AddBool(api, manifest, i18n, "ShowInTooltip", () => getConfig().ShowInTooltip, value => getConfig().ShowInTooltip = value);
        api.AddNumberOption(
            manifest,
            getValue: () => getConfig().MomentsShown,
            setValue: value => getConfig().MomentsShown = (int)value,
            name: () => i18n.Get("config.MomentsShown.name"),
            tooltip: () => i18n.Get("config.MomentsShown.tooltip"),
            min: 0,
            max: 10,
            interval: 1
        );
        AddBool(api, manifest, i18n, "ShowNicknames", () => getConfig().ShowNicknames, value => getConfig().ShowNicknames = value);
        AddBool(api, manifest, i18n, "MilestoneMessages", () => getConfig().MilestoneMessages, value => getConfig().MilestoneMessages = value);

        api.AddSectionTitle(manifest, () => i18n.Get("config.section.controls"));
        AddKeybind(api, manifest, i18n, "ExpandKey", () => getConfig().ExpandKey, value => getConfig().ExpandKey = value);
        AddKeybind(api, manifest, i18n, "RenameKey", () => getConfig().RenameKey, value => getConfig().RenameKey = value);

        api.AddSectionTitle(manifest, () => i18n.Get("config.section.saves"));
        AddBool(api, manifest, i18n, "BackfillFromGameStats", () => getConfig().BackfillFromGameStats, value => getConfig().BackfillFromGameStats = value);
    }

    private static void AddBool(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper i18n, string option, Func<bool> getValue, Action<bool> setValue)
    {
        api.AddBoolOption(
            manifest,
            getValue: getValue,
            setValue: setValue,
            name: () => i18n.Get($"config.{option}.name"),
            tooltip: () => i18n.Get($"config.{option}.tooltip")
        );
    }

    private static void AddKeybind(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper i18n, string option, Func<KeybindList> getValue, Action<KeybindList> setValue)
    {
        api.AddKeybindList(
            manifest,
            getValue: getValue,
            setValue: setValue,
            name: () => i18n.Get($"config.{option}.name"),
            tooltip: () => i18n.Get($"config.{option}.tooltip")
        );
    }
}

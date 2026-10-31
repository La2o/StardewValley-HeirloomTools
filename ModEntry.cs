using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using HeirloomTools.Framework;

namespace HeirloomTools;

/// <summary>The mod entry point.</summary>
public class ModEntry : Mod
{
    /*********
    ** Fields
    *********/
    private ModConfig Config = null!;
    private Text Text = null!;
    private Recorder Recorder = null!;
    private Stamper Stamper = null!;


    /*********
    ** Public methods
    *********/
    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.Text = new Text(helper.Translation);
        this.Recorder = new Recorder(this.Monitor, () => this.Config, this.Text);
        this.Stamper = new Stamper(this.Monitor, () => this.Config);
        ToolNames.Enabled = () => this.Config.ShowNicknames;

        TooltipRenderer tooltip = new(this.Text, () => this.Config, () => this.Config.ExpandKey.IsDown());
        HarmonyPatches.Apply(new Harmony(this.ModManifest.UniqueID), this.Monitor, this.Recorder, tooltip);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.Stamper.InitializeIfNeeded(Game1.player);
        helper.Events.GameLoop.DayStarted += (_, _) => this.Stamper.StampInventory(Game1.player);
        helper.Events.Player.InventoryChanged += this.OnInventoryChanged;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

        new ConsoleCommands(this.Monitor, this.Text, this.Recorder).Register(helper.ConsoleCommands);
    }


    /*********
    ** Private methods
    *********/
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        GmcmIntegration.Register(
            this.Helper,
            this.ModManifest,
            getConfig: () => this.Config,
            reset: () => this.Config = new ModConfig(),
            save: () => this.Helper.WriteConfig(this.Config)
        );
    }

    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (!e.IsLocalPlayer)
            return;

        foreach (Item item in e.Added)
            this.Stamper.StampNew(item);
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady || !this.Config.RenameKey.JustPressed())
            return;

        // the tool being hovered in the inventory, or else the one in hand
        Tool? tool = null;
        if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.currentTab == GameMenu.inventoryTab && gameMenu.GetCurrentPage() is InventoryPage page)
            tool = page.hoveredItem as Tool;
        else if (Context.IsPlayerFree)
            tool = Game1.player.CurrentTool;

        if (tool is null || !ToolStats.IsTracked(tool))
            return;

        this.Helper.Input.SuppressActiveKeybinds(this.Config.RenameKey);
        this.OpenRenameMenu(tool);
    }

    private void OpenRenameMenu(Tool tool)
    {
        string realName = ToolNames.GetRealName(tool);
        IClickableMenu? previousMenu = Game1.activeClickableMenu;

        NamingMenu menu = new(
            name =>
            {
                this.Recorder.Rename(tool, name.Trim() == realName ? null : name);
                Game1.playSound("newArtifact");
                Game1.exitActiveMenu();
                if (previousMenu != null)
                    Game1.activeClickableMenu = previousMenu;
            },
            this.Text.Get("rename.title", new { tool = realName }),
            HeirloomRecord.GetName(tool) ?? realName
        );
        menu.textBox.textLimit = 32;

        // widen the name box (the default fits pet names, not "Grandpa's Iridium Hoe")
        const int extraWidth = 224;
        menu.textBox.X -= extraWidth / 2;
        menu.textBox.Width += extraWidth;
        menu.textBoxCC.bounds.X -= extraWidth / 2;
        menu.textBoxCC.bounds.Width += extraWidth;
        menu.doneNamingButton.bounds.X += extraWidth / 2;
        menu.randomButton.bounds.X += extraWidth / 2;
        menu.textBox.Text = HeirloomRecord.GetName(tool) ?? realName; // the text box trimmed it to the old width

        Game1.activeClickableMenu = menu;
    }
}

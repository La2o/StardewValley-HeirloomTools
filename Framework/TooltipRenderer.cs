using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace HeirloomTools.Framework;

/// <summary>Adds a tool's record to the bottom of its item tooltip.</summary>
public class TooltipRenderer
{
    /*********
    ** Fields
    *********/
    private readonly Text Text;
    private readonly Func<ModConfig> Config;
    private readonly Func<bool> IsExpanded;

    /// <summary>The space above the record, where the divider is drawn.</summary>
    private const int TopPadding = 12;

    /// <summary>The narrowest tooltip the record allows.</summary>
    private const int MinWidth = 304;

    /// <summary>The widest the record can make a tooltip, so moments fit on one line where possible.</summary>
    private const int MaxWidth = 560;

    /// <summary>The space below the record.</summary>
    private const int BottomPadding = 4;

    private static readonly Color StatColor = Game1.textColor;
    private static readonly Color MomentColor = new(132, 72, 12);
    private static readonly Color SinceColor = new(110, 90, 70);

    /// <summary>The layout from the last measure, reused when drawing the same tooltip.</summary>
    private Layout? LastLayout;


    /*********
    ** Public methods
    *********/
    public TooltipRenderer(Text text, Func<ModConfig> config, Func<bool> isExpanded)
    {
        this.Text = text;
        this.Config = config;
        this.IsExpanded = isExpanded;
    }

    /// <summary>Get the size the tooltip needs to fit the record, given its current width.</summary>
    /// <returns>The tooltip width to use (never narrower than <paramref name="tooltipWidth"/>) and the extra height needed, or null if there's no record to show.</returns>
    public (int Width, int Height)? Measure(Tool tool, SpriteFont font, int tooltipWidth)
    {
        Layout? layout = this.GetLayout(tool, font, tooltipWidth);
        return layout != null ? (layout.Width, layout.Height) : null;
    }

    /// <summary>Draw the record at the given position and move <paramref name="y"/> past it.</summary>
    public void Draw(SpriteBatch b, Tool tool, SpriteFont font, int x, ref int y, float alpha)
    {
        Layout? layout = this.LastLayout != null && this.LastLayout.Matches(tool, font) ? this.LastLayout : this.GetLayout(tool, font, 0);
        if (layout is null)
            return;

        int top = y + 16 + TopPadding / 2;
        b.Draw(Game1.staminaRect, new Rectangle(x + 20, top, layout.Width - 40, 2), Game1.textColor * 0.25f * alpha);
        y += TopPadding;

        foreach ((string line, Color color) in layout.Lines)
        {
            Utility.drawTextWithShadow(b, line, font, new Vector2(x + 16, y + 16 + 4), color * 0.9f * alpha);
            y += (int)font.MeasureString(line).Y;
        }
        y += BottomPadding;
    }


    /*********
    ** Private methods
    *********/
    private Layout? GetLayout(Tool tool, SpriteFont font, int tooltipWidth)
    {
        if (!this.Config().ShowInTooltip || !HeirloomRecord.IsStarted(tool))
            return null;

        // collect the text first, then size the tooltip so most lines fit without wrapping
        List<(string Text, Color Color)> texts = new();
        void Add(string text, Color color) => texts.Add((text, color));

        string? name = HeirloomRecord.GetName(tool);
        if (name != null && this.Config().ShowNicknames)
            Add(this.Text.Get("tooltip.realName", new { name = ToolNames.GetRealName(tool) }), SinceColor);

        foreach (string line in this.Text.StatLines(tool))
            Add(line, StatColor);

        IReadOnlyList<Moment> moments = HeirloomRecord.GetMoments(tool);
        int maxMoments = this.IsExpanded() ? HeirloomRecord.MaxMoments : this.Config().MomentsShown;
        foreach (Moment moment in moments.Skip(Math.Max(0, moments.Count - maxMoments)).Reverse())
            Add(this.Text.Moment(tool, moment), MomentColor);
        if (!this.IsExpanded() && moments.Count > maxMoments && maxMoments > 0)
            Add(this.Text.Get("tooltip.more", new { count = moments.Count - maxMoments, key = this.Config().ExpandKey.ToString() }), SinceColor);

        Add(this.Text.Since(tool), SinceColor);

        int widest = texts.Max(text => (int)font.MeasureString(text.Text).X);
        int width = Math.Max(tooltipWidth, Math.Max(MinWidth, Math.Min(widest + 32, MaxWidth)));
        List<(string, Color)> lines = new();
        foreach ((string text, Color color) in texts)
        {
            foreach (string line in Game1.parseText(text, font, width - 32).Split('\n'))
                lines.Add((line, color));
        }

        int height = TopPadding + BottomPadding + lines.Sum(line => (int)font.MeasureString(line.Item1).Y);
        this.LastLayout = new Layout(tool, font, lines, width, height);
        return this.LastLayout;
    }

    private sealed class Layout
    {
        public readonly WeakReference<Tool> Tool;
        public readonly SpriteFont Font;
        public readonly List<(string Text, Color Color)> Lines;
        public readonly int Width;
        public readonly int Height;
        private readonly int Tick = Game1.ticks;

        public Layout(Tool tool, SpriteFont font, List<(string, Color)> lines, int width, int height)
        {
            this.Tool = new WeakReference<Tool>(tool);
            this.Font = font;
            this.Lines = lines;
            this.Width = width;
            this.Height = height;
        }

        /// <summary>Whether this layout was measured for the same tooltip this frame.</summary>
        public bool Matches(Tool tool, SpriteFont font)
        {
            return this.Font == font && Game1.ticks - this.Tick <= 1 && this.Tool.TryGetTarget(out Tool? target) && ReferenceEquals(target, tool);
        }
    }
}

using CrystalDefense.Game.Flow;
using OpenTK.Mathematics;

namespace CrystalDefense.Game.UI;

// A rectangle on the screen, in pixels.
public readonly record struct UiRect(float X, float Y, float Width, float Height)
{
    public bool Contains(Vector2 point) =>
        point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
}

// What the HUD shows during the game.
public readonly record struct HudInfo(
    int Health,
    int MaxHealth,
    bool KnockedOut,
    float DashCharge,
    int Wave,
    int TotalWaves,
    int Score,
    int EnemiesAlive,
    bool MouseMode);

// Draws every screen of the game (HUD, menu, pause, upgrade choice, victory/defeat) with the UiRenderer.
// All sizes are relative to the window height, so the interface looks the same in every resolution.
public sealed class GameUi
{
    private static readonly Vector4 Gold = new(0.95f, 0.78f, 0.38f, 1.0f);
    private static readonly Vector4 White = new(0.95f, 0.94f, 0.9f, 1.0f);
    private static readonly Vector4 Dim = new(0.75f, 0.72f, 0.66f, 1.0f);
    private static readonly Vector4 Red = new(0.86f, 0.18f, 0.16f, 1.0f);
    private static readonly Vector4 DarkRed = new(0.25f, 0.05f, 0.05f, 0.9f);
    private static readonly Vector4 Panel = new(0.06f, 0.05f, 0.07f, 0.82f);
    private static readonly Vector4 PanelHighlight = new(0.2f, 0.14f, 0.06f, 0.92f);
    private static readonly Vector4 Overlay = new(0.0f, 0.0f, 0.0f, 0.55f);
    private static readonly Vector4 Cyan = new(0.45f, 0.9f, 1.0f, 1.0f);

    private readonly UiRenderer _ui;

    public GameUi(UiRenderer ui) => _ui = ui;

    private float W => _ui.ScreenSize.X;
    private float H => _ui.ScreenSize.Y;

    // Size of one font pixel for normal text (4 screen pixels in a 720 pixels high window).
    private float S => H / 180.0f;

    // ===================================================== HUD

    public void DrawHud(HudInfo info)
    {
        // --- player: health (hearts) and dash, under the crystal health bar (top left) ---
        float x = W * 0.04f;
        float y = H * 0.11f;
        _ui.TextWithShadow("Knight", x, y, S * 0.8f, Gold);
        y += S * 8;

        if (info.KnockedOut)
        {
            _ui.TextWithShadow("Knocked out - back in a moment...", x, y, S * 0.8f, Red);
        }
        else
        {
            float heart = S * 3.2f;
            for (int i = 0; i < info.MaxHealth; i++)
            {
                float hx = x + i * (heart + S * 1.2f);
                _ui.Rect(hx - S * 0.4f, y - S * 0.4f, heart + S * 0.8f, heart + S * 0.8f, new Vector4(0, 0, 0, 0.6f));
                _ui.Rect(hx, y, heart, heart, i < info.Health ? Red : DarkRed);
            }
        }
        y += S * 6;

        _ui.TextWithShadow("Dash", x, y, S * 0.7f, Dim);
        float barX = x + _ui.Measure("Dash ", S * 0.7f);
        float barWidth = S * 24;
        _ui.Rect(barX, y, barWidth, S * 4.2f, new Vector4(0, 0, 0, 0.6f));
        _ui.Rect(barX + S * 0.5f, y + S * 0.5f, (barWidth - S) * Math.Clamp(info.DashCharge, 0, 1), S * 3.2f,
                 info.DashCharge >= 1.0f ? Cyan : new Vector4(0.25f, 0.45f, 0.55f, 1.0f));

        // --- wave and enemies (top centre) ---
        _ui.TextCentered($"Wave {info.Wave} / {info.TotalWaves}", W / 2, H * 0.03f, S * 1.4f, Gold);
        _ui.TextCentered($"Enemies: {info.EnemiesAlive}", W / 2, H * 0.03f + S * 12, S * 0.8f, White);

        // --- score (top right) ---
        string score = info.Score.ToString("D6");
        float scoreSize = S * 1.4f;
        float right = W * 0.96f;
        _ui.TextWithShadow("Score", right - _ui.Measure("Score", S * 0.8f), H * 0.03f, S * 0.8f, Dim);
        _ui.TextWithShadow(score, right - _ui.Measure(score, scoreSize), H * 0.03f + S * 7, scoreSize, White);

        // --- controls reminder (bottom) ---
        string controls = info.MouseMode
            ? "Left click: shoot   Right click: sword   Space: dash   Esc: pause"
            : "Arrows: shoot   Enter: sword   Space: dash   Esc: pause";
        _ui.TextCentered(controls, W / 2, H - S * 9, S * 0.65f, new Vector4(Dim.X, Dim.Y, Dim.Z, 0.85f));
    }

    // A big "Wave 2" for the first seconds of every wave; during the first wave, also a short reminder
    // of the goal and the controls, so that a new player has time to learn them.
    public void DrawWaveStart(int wave, float secondsSinceStart, bool mouseMode)
    {
        if (secondsSinceStart < 3.0f)
        {
            float alpha = Math.Clamp(3.0f - secondsSinceStart, 0.0f, 1.0f);
            _ui.TextCentered($"Wave {wave}", W / 2, H * 0.24f, S * 3.0f, new Vector4(Gold.X, Gold.Y, Gold.Z, alpha));
        }

        if (wave == 1 && secondsSinceStart < 14.0f)
        {
            string[] tips =
            {
                "Protect the crystal from the enemies!",
                "Move: W A S D      Dash: Space",
                mouseMode ? "Shoot: left click      Sword: right click"
                          : "Shoot: arrow keys      Sword: Enter / K",
            };
            DrawPanelWithLines(tips, H * 0.66f, S * 0.85f);
        }
    }

    // ===================================================== MAIN MENU

    public void DrawMainMenu(float time)
    {
        _ui.Rect(0, 0, W, H, Overlay);

        _ui.TextCentered("Crystal Defense", W / 2, H * 0.14f, S * 3.4f, Gold);
        _ui.TextCentered("Defend the crystal against the waves of enemies", W / 2, H * 0.14f + S * 30, S * 0.9f, White);

        // blinking invitation
        if ((int)(time * 1.6f) % 2 == 0)
            _ui.TextCentered("Press Enter or click to play", W / 2, H * 0.4f, S * 1.3f, White);

        string[] lines =
        {
            "Move: W A S D  (Z Q S D on AZERTY)",
            "Shoot: arrow keys  or  left click",
            "Sword: Enter / K  or  right click",
            "Dash: Space",
            "Camera: Q / E rotate   R / F zoom",
            "Pause: Esc",
        };
        DrawPanelWithLines(lines, H * 0.52f, S * 0.85f);

        _ui.TextCentered("Esc: quit", W / 2, H - S * 10, S * 0.75f, Dim);
    }

    // ===================================================== PAUSE

    public void DrawPause()
    {
        _ui.Rect(0, 0, W, H, Overlay);
        float panelWidth = W * 0.4f;
        float panelHeight = H * 0.36f;
        float px = (W - panelWidth) / 2;
        float py = H * 0.28f;
        _ui.Rect(px, py, panelWidth, panelHeight, Panel);
        _ui.Frame(px, py, panelWidth, panelHeight, S * 0.6f, Gold);

        _ui.TextCentered("Paused", W / 2, py + S * 7, S * 2.6f, Gold);
        _ui.TextCentered("Esc or P: resume", W / 2, py + S * 34, S * 0.95f, White);
        _ui.TextCentered("M: main menu", W / 2, py + S * 46, S * 0.95f, White);
    }

    // ===================================================== UPGRADE CHOICE

    // The three cards, side by side in the middle of the screen (also used to test mouse clicks).
    public UiRect[] GetUpgradeCardRects(int count)
    {
        float cardWidth = W * 0.25f;
        float cardHeight = H * 0.36f;
        float gap = W * 0.03f;
        float total = count * cardWidth + (count - 1) * gap;
        float startX = (W - total) / 2;
        var rects = new UiRect[count];
        for (int i = 0; i < count; i++)
            rects[i] = new UiRect(startX + i * (cardWidth + gap), H * 0.34f, cardWidth, cardHeight);
        return rects;
    }

    public void DrawUpgradeChoice(int finishedWave, IReadOnlyList<Upgrade> choices, int selected)
    {
        _ui.Rect(0, 0, W, H, Overlay);
        _ui.TextCentered($"Wave {finishedWave} cleared!", W / 2, H * 0.1f, S * 2.2f, Gold);
        _ui.TextCentered("Choose an upgrade", W / 2, H * 0.1f + S * 22, S * 1.0f, White);

        UiRect[] cards = GetUpgradeCardRects(choices.Count);
        for (int i = 0; i < choices.Count; i++)
        {
            UiRect r = cards[i];
            bool isSelected = i == selected;
            _ui.Rect(r.X, r.Y, r.Width, r.Height, isSelected ? PanelHighlight : Panel);
            _ui.Frame(r.X, r.Y, r.Width, r.Height, isSelected ? S * 0.9f : S * 0.4f, isSelected ? Gold : Dim);

            float centerX = r.X + r.Width / 2;
            _ui.TextCentered((i + 1).ToString(), centerX, r.Y + S * 5, S * 2.2f, isSelected ? Gold : Dim);

            // the title must fit in the card
            string title = choices[i].Title;
            float titleSize = MathF.Min(S * 1.15f, r.Width * 0.88f / Math.Max(1, title.Length * BitmapFont.Advance));
            _ui.TextCentered(title, centerX, r.Y + S * 25, titleSize, White);

            float lineY = r.Y + S * 40;
            float textSize = S * 0.75f;
            foreach (string line in WrapText(choices[i].Description, r.Width * 0.86f, textSize))
            {
                _ui.TextCentered(line, centerX, lineY, textSize, Dim);
                lineY += textSize * 11;
            }
        }

        _ui.TextCentered("1, 2, 3  or  arrows + Enter  or  click", W / 2, H * 0.78f, S * 0.85f, White);
    }

    // ===================================================== VICTORY / DEFEAT

    public void DrawEndScreen(bool victory, int score, int wave, int totalWaves, int kills)
    {
        _ui.Rect(0, 0, W, H, new Vector4(0, 0, 0, 0.65f));
        _ui.TextCentered(victory ? "Victory!" : "Defeat", W / 2, H * 0.16f, S * 3.4f, victory ? Gold : Red);
        _ui.TextCentered(victory ? "The crystal is safe" : "The crystal was destroyed",
                         W / 2, H * 0.16f + S * 30, S * 1.1f, White);

        string[] lines =
        {
            $"Score: {score}",
            $"Waves: {wave} / {totalWaves}",
            $"Enemies defeated: {kills}",
        };
        DrawPanelWithLines(lines, H * 0.44f, S * 1.1f);

        _ui.TextCentered("Enter: play again      Esc: menu", W / 2, H * 0.8f, S * 1.0f, White);
    }

    // ===================================================== HELPERS

    private void DrawPanelWithLines(string[] lines, float top, float textSize)
    {
        float lineHeight = textSize * 12;
        float width = lines.Max(line => _ui.Measure(line, textSize)) + S * 16;
        float height = lines.Length * lineHeight + S * 8;
        float x = (W - width) / 2;
        _ui.Rect(x, top, width, height, Panel);
        _ui.Frame(x, top, width, height, S * 0.4f, new Vector4(Gold.X, Gold.Y, Gold.Z, 0.7f));
        for (int i = 0; i < lines.Length; i++)
            _ui.TextCentered(lines[i], W / 2, top + S * 5 + i * lineHeight, textSize, White);
    }

    // Splits a text into lines that fit into the given width.
    private IEnumerable<string> WrapText(string text, float maxWidth, float pixelSize)
    {
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && _ui.Measure(candidate, pixelSize) > maxWidth)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
            yield return line;
    }
}

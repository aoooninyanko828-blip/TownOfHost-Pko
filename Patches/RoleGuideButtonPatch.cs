using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;
using UnityEngine.UI;
using static TownOfHost.Translator;

namespace TownOfHost;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
public static class RoleGuideButtonPatch
{
    private static GameObject guidePanel;
    public static bool isPanelOpen = false;
    private static GuideTab currentTab = GuideTab.MyRole;

    private static Sprite _squareSprite;
    private static Sprite SquareSprite
    {
        get
        {
            if (_squareSprite != null) return _squareSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // 背景が米粒にならないよう 1f に設定
            _squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _squareSprite;
        }
    }

    private enum GuideTab { MyRole, RoleList }

    public static void Postfix(HudManager __instance)
    {
        _ = new LateTask(() => CreateGuideButton(__instance), 0.5f, "RoleGuide.Create", true);
    }

    private static void CreateGuideButton(HudManager hud)
    {
        try
        {
            if (hud == null) return;
            var old = hud.transform.Find("RoleGuideButton");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);

            var settingsButton = hud.SettingsButton;
            if (settingsButton == null) return;

            var settingsPassiveBtn = settingsButton.GetComponent<PassiveButton>();
            if (settingsPassiveBtn != null)
                settingsPassiveBtn.OnClick.AddListener((UnityEngine.Events.UnityAction)ClosePanel);

            var btnObj = new GameObject("RoleGuideButton");
            btnObj.transform.SetParent(hud.transform);
            btnObj.layer = 5;

            var settingsPos = settingsButton.transform.localPosition;
            btnObj.transform.localPosition = new Vector3(settingsPos.x - 9.0f, settingsPos.y, settingsPos.z);
            var originalScale = new Vector3(0.45f, 0.45f, 1f);
            btnObj.transform.localScale = originalScale;

            var sr = btnObj.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            sr.sortingOrder = 10;
            sr.sprite = CreateButtonIcon();

            var col = btnObj.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var btn = btnObj.AddComponent<PassiveButton>();
            btn.Colliders = new Collider2D[] { col };
            btn.OnClick = new Button.ButtonClickedEvent();

            // クリック時の「押した感」アニメーション
            btn.OnClick.AddListener((UnityEngine.Events.UnityAction)(() => {
                btnObj.transform.localScale = originalScale * 0.9f;
                _ = new LateTask(() => {
                    if (btnObj) btnObj.transform.localScale = originalScale;
                    TogglePanel();
                }, 0.05f, "RoleGuide.ClickAnim", true);
            }));

            btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() => { if (sr) sr.color = new Color(0.8f, 0.9f, 1f); }));
            btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() => { if (sr) sr.color = Color.white; }));
        }
        catch (System.Exception e)
        {
            Logger.Error(e.ToString(), "RoleGuideButton");
        }
    }

    private static Sprite CreateButtonIcon()
    {
        int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);

        for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                float cx = x - 64f, cy = y - 64f;
                float rx = Mathf.Abs(cx) - 50f;
                float ry = Mathf.Abs(cy) - 50f;
                float cornerDist = Mathf.Sqrt(Mathf.Max(0, rx) * Mathf.Max(0, rx) + Mathf.Max(0, ry) * Mathf.Max(0, ry));
                if (cornerDist > 14f) { tex.SetPixel(x, y, Color.clear); continue; }
                if (cornerDist > 11f) { tex.SetPixel(x, y, new Color(0.31f, 0.31f, 0.66f, 0.9f)); continue; }
                tex.SetPixel(x, y, new Color(0.07f, 0.07f, 0.18f, 0.92f));
            }

        (Color col, int barY, int barW)[] bars =
        {
            (new Color(1f, 0.27f, 0.27f, 1f),    82, 70),
            (new Color(0.31f, 0.77f, 0.97f, 1f), 62, 70),
            (new Color(1f, 0.92f, 0.23f, 1f),     42, 54),
        };
        foreach (var (c, barY, barW) in bars)
        {
            for (int bx = 14; bx < 22; bx++)
                for (int by = barY - 3; by < barY + 5; by++)
                    tex.SetPixel(bx, by, c);
            for (int bx = 28; bx < 28 + barW; bx++)
                for (int by = barY - 1; by < barY + 4; by++)
                    tex.SetPixel(bx, by, c);
        }
        for (int x = 10; x < 118; x++)
            tex.SetPixel(x, 26, new Color(0.27f, 0.27f, 0.55f, 0.8f));

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void TogglePanel()
    {
        if (isPanelOpen) ClosePanel();
        else OpenPanel();
    }

    public static void OpenPanel()
    {
        ClosePanel();
        isPanelOpen = true;
        currentTab = GuideTab.MyRole;
        BuildPanel();
    }

    public static void ClosePanel()
    {
        if (guidePanel != null) UnityEngine.Object.Destroy(guidePanel);
        guidePanel = null;
        isPanelOpen = false;
    }

    private static void BuildPanel()
    {
        if (guidePanel != null) UnityEngine.Object.Destroy(guidePanel);

        guidePanel = new GameObject("RoleGuidePanel");
        guidePanel.transform.SetParent(HudManager.Instance.transform);
        guidePanel.transform.localPosition = new Vector3(0f, 0f, -20f);
        guidePanel.transform.localScale = Vector3.one;
        guidePanel.layer = 5;

        // ★パネル全体のサイズと位置を黄金比に再計算
        MakeSprite(guidePanel, "BG", Vector3.zero, new Vector3(8.6f, 5.6f, 1f),
            new Color(0.05f, 0.05f, 0.1f, 0.96f), 4);

        MakeSprite(guidePanel, "TitleBG", new Vector3(0f, 2.45f, -0.5f), new Vector3(8.6f, 0.65f, 1f),
            new Color(0.15f, 0.2f, 0.35f, 1f), 5);

        MakeText(guidePanel, "Title", new Vector3(0f, 2.45f, -1f),
            "<color=#ffffff>役職ガイド</color>", 2.6f, TextAlignmentOptions.Center, null, new Vector2(0.5f, 0.5f));

        MakeSprite(guidePanel, "LeftBG", new Vector3(-3.3f, -0.15f, -0.5f),
            new Vector3(2.0f, 4.85f, 1f), new Color(0.1f, 0.12f, 0.25f, 1f), 5);

        var tabs = new (string label, GuideTab tab, Color color)[]
        {
            ("自分の役職", GuideTab.MyRole,   new Color(0.31f, 0.77f, 0.97f, 1f)),
            ("配役情報",   GuideTab.RoleList,  new Color(1f, 0.4f, 0.4f, 1f)),
        };

        float tabY = 1.5f;
        foreach (var (label, tab, color) in tabs)
        {
            var t = tab;
            MakeTabButton(guidePanel, label, new Vector3(-3.3f, tabY, -1f), color, tab == currentTab,
                () => { currentTab = t; BuildPanel(); });
            tabY -= 0.95f;
        }

        MakeSprite(guidePanel, "ContentBG", new Vector3(1.0f, -0.15f, -0.5f),
            new Vector3(6.6f, 4.85f, 1f), new Color(0.06f, 0.06f, 0.12f, 1f), 5);

        BuildContent();
    }

    private static void BuildContent()
    {
        switch (currentTab)
        {
            case GuideTab.MyRole: BuildMyRoleContent(); break;
            case GuideTab.RoleList: BuildRoleListContent(); break;
        }
    }

    private static void BuildMyRoleContent()
    {
        var localPc = PlayerControl.LocalPlayer;
        if (localPc == null) return;

        var role = localPc.GetCustomRole();
        string roleColorStr = ColorUtility.ToHtmlStringRGBA(localPc.GetRoleColor());

        string content;
        if (role is CustomRoles.Crewmate or CustomRoles.Impostor)
        {
            content = $"<size=150%><color=#{roleColorStr}>{GetString(role.ToString())}</color></size>\n\n" +
                      $"<size=85%><color=#ffffff>{localPc.GetRoleDesc(true)}</color></size>";
        }
        else
        {
            content = role.GetRoleInfo()?.Description?.FullFormatHelp
                ?? $"<size=150%><color=#{roleColorStr}>{GetString(role.ToString())}</color></size>\n\n" +
                   $"<size=85%><color=#ffffff>{localPc.GetRoleDesc(true)}</color></size>";
        }

        // ★超重要：元のテキストに埋め込まれている「中央揃え」の命令を強制的に「左揃え」に書き換える！
        content = content.Replace("<align=center>", "<align=left>");

        // ★文字を大きく（2.0f）し、右側の広いパネルの左端（-2.0f）から綺麗に開始させる
        MakeText(guidePanel, "MyRole", new Vector3(-2.0f, 2.0f, -1f), content, 2.0f,
            TextAlignmentOptions.TopLeft, new Vector2(6.0f, 4.6f), new Vector2(0f, 1f));
    }

    private static void BuildRoleListContent()
    {
        string content = "";
        try
        {
            System.Reflection.MethodInfo targetMethod = null;
            var typesToSearch = new System.Type[] { typeof(UtilsRoleText), typeof(Utils) };
            foreach (var t in typesToSearch)
            {
                var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var m in methods)
                {
                    if (m.ReturnType == typeof(string) && m.Name.Contains("Role") && !m.Name.Contains("Show"))
                    {
                        targetMethod = m; break;
                    }
                }
                if (targetMethod != null) break;
            }
            if (targetMethod != null)
            {
                object[] args = targetMethod.GetParameters().Length == 1 ? new object[] { PlayerControl.LocalPlayer.PlayerId } : null;
                content = (string)targetMethod.Invoke(null, args);
            }
        }
        catch { }

        if (string.IsNullOrEmpty(content)) content = "<color=#888888>配役情報なし</color>";

        // ★配役情報も同様に強制左揃え
        content = content.Replace("<align=center>", "<align=left>");

        MakeText(guidePanel, "RoleList", new Vector3(-2.0f, 2.0f, -1f), content, 1.8f,
            TextAlignmentOptions.TopLeft, new Vector2(6.0f, 4.6f), new Vector2(0f, 1f));
    }

    private static void MakeSprite(GameObject parent, string name, Vector3 pos, Vector3 scale, Color color, int order = 5)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.layer = 5;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = color;
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = order;
    }

    // ★ピボット（基準点）を自由に設定できるように引数を追加
    private static TextMeshPro MakeText(GameObject parent, string name, Vector3 pos, string text,
        float size, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Vector2? rectSize = null, Vector2? pivot = null)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;
        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.sortingOrder = 11;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.fontStyle = FontStyles.Bold;

        if (rectSize.HasValue) tmp.rectTransform.sizeDelta = rectSize.Value;

        // ピボットの指定があれば適用し、なければ中央(0.5, 0.5)にする
        tmp.rectTransform.pivot = pivot ?? new Vector2(0.5f, 0.5f);

        return tmp;
    }

    private static void MakeTabButton(GameObject parent, string label, Vector3 pos,
        Color accentColor, bool isSelected, System.Action onClick)
    {
        var obj = new GameObject($"Tab_{label}");
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;

        Color bgColor = isSelected ? new Color(accentColor.r * 0.35f, accentColor.g * 0.35f, accentColor.b * 0.35f, 0.95f) : new Color(0.12f, 0.15f, 0.30f, 0.95f);
        var bg = new GameObject("BG");
        bg.transform.SetParent(obj.transform);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(1.8f, 0.75f, 1f); // タブ幅を少し広げた
        bg.layer = 5;
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = SquareSprite;
        bgSr.color = bgColor;
        bgSr.material = new Material(Shader.Find("Sprites/Default"));
        bgSr.sortingOrder = 6;

        var bar = new GameObject("Bar");
        bar.transform.SetParent(obj.transform);
        bar.transform.localPosition = new Vector3(-0.85f, 0f, -0.1f); // バーの位置を左端にピタッと合わせる
        bar.transform.localScale = new Vector3(0.055f, 0.75f, 1f);
        bar.layer = 5;
        var barSr = bar.AddComponent<SpriteRenderer>();
        barSr.sprite = SquareSprite;
        barSr.color = accentColor;
        barSr.material = new Material(Shader.Find("Sprites/Default"));
        barSr.sortingOrder = 7;

        // ★タブの文字が下にズレていたバグを修正（Y座標を 0f に）
        var tmp = MakeText(obj, "Lbl", new Vector3(0f, 0f, -0.2f), label, 1.9f, TextAlignmentOptions.Center, null, new Vector2(0.5f, 0.5f));
        tmp.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.8f, 0.75f);
        var btn = obj.AddComponent<PassiveButton>();
        btn.Colliders = new Collider2D[] { col };
        btn.OnClick.AddListener((UnityEngine.Events.UnityAction)onClick);
        btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() => { if (!isSelected && bgSr) bgSr.color = new Color(0.2f, 0.25f, 0.40f, 0.95f); }));
        btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() => { if (!isSelected && bgSr) bgSr.color = bgColor; }));
    }
}

// ＝＝＝ 自動クローズ用のパッチ ＝＝＝
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class AutoClosePanelPatch { public static void Postfix() { if (RoleGuideButtonPatch.isPanelOpen && Minigame.Instance != null) RoleGuideButtonPatch.ClosePanel(); } }

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SetVisible))]
public static class AutoCloseOnChatPatch { public static void Postfix(bool visible) { if (visible && RoleGuideButtonPatch.isPanelOpen) RoleGuideButtonPatch.ClosePanel(); } }

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class ClosePanelOnMeetingPatch { public static void Postfix() => RoleGuideButtonPatch.ClosePanel(); }

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
public static class ClosePanelOnGameEndPatch { public static void Prefix() => RoleGuideButtonPatch.ClosePanel(); }
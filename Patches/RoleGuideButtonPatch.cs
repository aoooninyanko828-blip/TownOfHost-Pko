using System;
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
    private static CustomRoles selectedRole = CustomRoles.NotAssigned;

    // スクロール
    private static float scrollY = 0f;
    private static float maxScrollY = 0f;
    private static GameObject scrollContent;
    private static GameObject scrollThumb;       // スクロールバーのつまみ
    private static bool scrollBarDragging = false;
    private static float scrollBarDragStartMouseY = 0f;
    private static float scrollBarDragStartScrollY = 0f;

    // ボタンアニメ
    private static SpriteRenderer _btnRenderer;
    private static float _btnAnimTimer = 0f;
    private static bool _btnAnimActive = false;

    // パネル定数
    private const float PanelW = 8.6f;
    private const float PanelH = 5.6f;
    // リスト領域
    private const float ListLeft = -2.3f;   // リスト左端X（パネル相対）
    private const float ListW = 3.6f;        // リスト幅
    private const float ListTop = 2.0f;      // リスト上端Y
    private const float ListH = 4.4f;        // リスト高さ
    private const float ItemH = 0.44f;
    // 詳細領域
    private const float DetailX = 0.8f;
    private const float DetailW = 4.2f;
    // スクロールバー
    private const float SbarX = -0.05f;     // リスト右端から少し外
    private const float SbarW = 0.18f;
    private const float SbarH = ListH;

    private static Sprite _squareSprite;
    private static Sprite SquareSprite
    {
        get
        {
            if (_squareSprite != null) return _squareSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
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
            btnObj.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

            var sr = btnObj.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            sr.sortingOrder = 10;
            sr.sprite = CreateButtonIcon();
            _btnRenderer = sr;

            var col = btnObj.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var btn = btnObj.AddComponent<PassiveButton>();
            btn.Colliders = new Collider2D[] { col };
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                _btnAnimActive = true;
                _btnAnimTimer = 0f;
                TogglePanel();
            }));
            btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (sr && !_btnAnimActive) sr.color = new Color(0.8f, 0.9f, 1f);
            }));
            btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (sr && !_btnAnimActive) sr.color = Color.white;
            }));
        }
        catch (Exception e) { Logger.Error(e.ToString(), "RoleGuideButton"); }
    }

    private static Sprite CreateButtonIcon()
    {
        int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                float cx = x - 64f, cy = y - 64f;
                float rx = Mathf.Abs(cx) - 50f, ry = Mathf.Abs(cy) - 50f;
                float cd = Mathf.Sqrt(Mathf.Max(0, rx) * Mathf.Max(0, rx) + Mathf.Max(0, ry) * Mathf.Max(0, ry));
                if (cd > 14f) { tex.SetPixel(x, y, Color.clear); continue; }
                if (cd > 11f) { tex.SetPixel(x, y, new Color(0.31f, 0.31f, 0.66f, 0.9f)); continue; }
                tex.SetPixel(x, y, new Color(0.07f, 0.07f, 0.18f, 0.92f));
            }
        (Color c, int bY, int bW)[] bars = {
            (new Color(1f,0.27f,0.27f,1f),82,70),
            (new Color(0.31f,0.77f,0.97f,1f),62,70),
            (new Color(1f,0.92f,0.23f,1f),42,54),
        };
        foreach (var (c, bY, bW) in bars)
        {
            for (int bx = 14; bx < 22; bx++) for (int by = bY - 3; by < bY + 5; by++) tex.SetPixel(bx, by, c);
            for (int bx = 28; bx < 28 + bW; bx++) for (int by = bY - 1; by < bY + 4; by++) tex.SetPixel(bx, by, c);
        }
        for (int x = 10; x < 118; x++) tex.SetPixel(x, 26, new Color(0.27f, 0.27f, 0.55f, 0.8f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void TogglePanel()
    {
        if (isPanelOpen) ClosePanel(); else OpenPanel();
    }

    public static void OpenPanel()
    {
        ClosePanel();
        isPanelOpen = true;
        currentTab = GuideTab.MyRole;
        selectedRole = CustomRoles.NotAssigned;
        scrollY = 0f;
        BuildPanel();
    }

    public static void ClosePanel()
    {
        if (guidePanel != null) UnityEngine.Object.Destroy(guidePanel);
        guidePanel = null;
        scrollContent = null;
        scrollThumb = null;
        isPanelOpen = false;
        scrollBarDragging = false;
    }

    private static void BuildPanel()
    {
        if (guidePanel != null) UnityEngine.Object.Destroy(guidePanel);
        scrollContent = null;
        scrollThumb = null;

        guidePanel = new GameObject("RoleGuidePanel");
        guidePanel.transform.SetParent(HudManager.Instance.transform);
        guidePanel.transform.localPosition = new Vector3(0f, 0f, -20f);
        guidePanel.transform.localScale = Vector3.one;
        guidePanel.layer = 5;

        // 背景
        MakeSprite(guidePanel, "BG", Vector3.zero,
            new Vector3(PanelW, PanelH, 1f), new Color(0.05f, 0.05f, 0.1f, 0.96f), 4);
        MakeSprite(guidePanel, "TitleBG", new Vector3(0f, 2.45f, -0.5f),
            new Vector3(PanelW, 0.65f, 1f), new Color(0.15f, 0.2f, 0.35f, 1f), 5);
        MakeText(guidePanel, "Title", new Vector3(0f, 2.45f, -1f),
            "<color=#ffffff>役職ガイド</color>", 2.6f, TextAlignmentOptions.Center);

        // 左タブ背景
        MakeSprite(guidePanel, "LeftBG", new Vector3(-3.3f, -0.15f, -0.5f),
            new Vector3(2.0f, 4.85f, 1f), new Color(0.1f, 0.12f, 0.25f, 1f), 5);

        var tabs = new (string label, GuideTab tab, Color color)[]
        {
            ("自分の役職", GuideTab.MyRole,  new Color(0.31f, 0.77f, 0.97f, 1f)),
            ("配役情報",   GuideTab.RoleList, new Color(1f, 0.4f, 0.4f, 1f)),
        };
        float tabY = 1.5f;
        foreach (var (label, tab, color) in tabs)
        {
            var t = tab;
            MakeTabButton(guidePanel, label, new Vector3(-3.3f, tabY, -1f), color, tab == currentTab,
                () => { currentTab = t; selectedRole = CustomRoles.NotAssigned; scrollY = 0f; BuildPanel(); });
            tabY -= 0.95f;
        }

        // コンテンツ背景
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
        if (localPc == null) { MakeText(guidePanel, "t", Vector3.zero, "情報なし", 1.9f); return; }

        var role = localPc.GetCustomRole();
        var roleClass = localPc.GetRoleClass();
        if (localPc.Is(CustomRoles.Amnesia))
            role = localPc.Is(CustomRoleTypes.Crewmate) ? CustomRoles.Crewmate : CustomRoles.Impostor;
        if (localPc.GetMisidentify(out var missrole)) role = missrole;
        if (role is CustomRoles.Amnesiac && roleClass is Amnesiac amnesiac && !amnesiac.Realized)
            role = Amnesiac.IsWolf ? CustomRoles.WolfBoy : CustomRoles.Sheriff;

        string roleColorStr = ColorUtility.ToHtmlStringRGBA(localPc.GetRoleColor());
        string content;
        if (role is CustomRoles.Crewmate or CustomRoles.Impostor)
        {
            content = $"<line-height=2.0pic><size=130%><color=#{roleColorStr}>{GetString(role.ToString())}</color></size>\n" +
                      $"<size=70%><line-height=1.8pic><color=#ffffff>{localPc.GetRoleDesc(true)}</color></size>";
        }
        else
        {
            content = role.GetRoleInfo()?.Description?.FullFormatHelp
                ?? $"<line-height=2.0pic><size=130%><color=#{roleColorStr}>{GetString(role.ToString())}</color></size>\n" +
                   $"<size=70%><line-height=1.8pic><color=#ffffff>{localPc.GetRoleDesc(true)}</color></size>";
        }

        MakeText(guidePanel, "MyRole", new Vector3(-1.5f, 2.0f, -1f), content, 1.95f,
            TextAlignmentOptions.TopLeft, new Vector2(6.2f, 4.6f));
    }

    private static void BuildRoleListContent()
    {
        // ★ スクロールコンテナ（移動する親オブジェクト）
        var container = new GameObject("ScrollContent");
        container.transform.SetParent(guidePanel.transform);
        container.layer = 5;
        scrollContent = container;

        // 有効な役職を収集
        var roles = Options.CustomRoleCounts.Keys
            .Where(r => r.IsEnable() && r != CustomRoles.GM && r != CustomRoles.NotAssigned)
            .OrderBy(r => (int)r.GetCustomRoleTypes())
            .ThenBy(r => r.ToString())
            .ToList();

        float y = 0f;
        CustomRoleTypes lastType = (CustomRoleTypes)(-1);

        foreach (var role in roles)
        {
            var t = role.GetCustomRoleTypes();
            // 陣営ヘッダー
            if (t != lastType)
            {
                lastType = t;
                string headerLabel = t switch
                {
                    CustomRoleTypes.Impostor => "<color=#ff4444>☆Impostors</color>",
                    CustomRoleTypes.Madmate => "<color=#ff9966>☆MadMates</color>",
                    CustomRoleTypes.Crewmate => "<color=#8cffff>☆CrewMates</color>",
                    CustomRoleTypes.Neutral => "<color=#cccccc>☆Neutrals</color>",
                    _ => t.ToString()
                };
                var headerTmp = MakeText(container, "Header_" + t,
                    new Vector3(ListLeft, y, 0f),
                    headerLabel, 1.4f, TextAlignmentOptions.TopLeft, new Vector2(ListW, ItemH));
                headerTmp.sortingOrder = 12;
                y -= ItemH;
            }

            var r = role;
            var colorCode = UtilsRoleText.GetRoleColorCode(r);
            var roleName = UtilsRoleText.GetRoleName(r);
            bool isSelected = selectedRole == r;

            var itemObj = new GameObject("Item_" + r);
            itemObj.transform.SetParent(container.transform);
            itemObj.transform.localPosition = new Vector3(0f, y, 0f);
            itemObj.layer = 5;

            // 選択ハイライト
            if (isSelected)
            {
                float bgWidth = ListW - 1f;
                var selBg = MakeSpriteChild(itemObj, "SelBG",
                    new Vector3(ListLeft + bgWidth * 0.5f + 0.1f, -ItemH * 0.5f + 0.03f, 0.1f),
                    new Vector3(bgWidth, ItemH * 0.88f, 1f),
                    new Color(0.2f, 0.3f, 0.55f, 0.9f), 11);
            }

            var txt = MakeText(itemObj, "Lbl",
                new Vector3(ListLeft + 0.1f, 0f, 0f),
                $"<color={colorCode}>{roleName}</color>  <size=75%><color=#aaaaaa>x{r.GetCount()}</color></size>",
                1.5f, TextAlignmentOptions.TopLeft, new Vector2(ListW - 0.15f, ItemH));
            txt.sortingOrder = 13;

            var col = itemObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(ListW, ItemH);
            col.offset = new Vector2(ListLeft + ListW * 0.5f, -ItemH * 0.5f);

            var btn = itemObj.AddComponent<PassiveButton>();
            btn.Colliders = new Collider2D[] { col };
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                selectedRole = r;
                var savedScroll = scrollY;
                BuildPanel();
                scrollY = savedScroll;
                ApplyScroll();
            }));
            btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOut = new UnityEngine.Events.UnityEvent();

            y -= ItemH;
        }

        // スクロール最大値（コンテンツ全高 - 表示高さ）
        float totalH = -y;
        maxScrollY = Mathf.Max(0f, totalH - ListH);

        // ★ スクロールバー背景
        float sbarCenterX = ListLeft + ListW + SbarW * 0.5f + -0.8f;
        float sbarCenterY = ListTop - SbarH * 0.5f;
        MakeSpriteOnPanel("SbarBG", new Vector3(sbarCenterX, sbarCenterY, -0.5f),
            new Vector3(SbarW, SbarH, 1f), new Color(0.1f, 0.1f, 0.2f, 0.9f), 8);

        // ★ スクロールバーつまみ
        float thumbH = maxScrollY > 0f
            ? Mathf.Max(0.3f, SbarH * (ListH / (totalH + 0.001f)))
            : SbarH;

        var thumbObj = new GameObject("SbarThumb");
        thumbObj.transform.SetParent(guidePanel.transform);
        thumbObj.transform.localScale = new Vector3(SbarW * 0.7f, thumbH, 1f);
        thumbObj.layer = 5;
        var thumbSr = thumbObj.AddComponent<SpriteRenderer>();
        thumbSr.sprite = SquareSprite;
        thumbSr.color = new Color(0.5f, 0.6f, 0.9f, 0.95f);
        thumbSr.material = new Material(Shader.Find("Sprites/Default"));
        thumbSr.sortingOrder = 9;
        scrollThumb = thumbObj;

        // つまみにコライダーとボタンをつける（長押しドラッグ用）
        var thumbCol = thumbObj.AddComponent<BoxCollider2D>();
        thumbCol.size = new Vector2(1f, 1f);
        var thumbBtn = thumbObj.AddComponent<PassiveButton>();
        thumbBtn.Colliders = new Collider2D[] { thumbCol };
        thumbBtn.OnClick = new Button.ButtonClickedEvent();
        thumbBtn.OnMouseOver = new UnityEngine.Events.UnityEvent();
        thumbBtn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (thumbSr) thumbSr.color = new Color(0.7f, 0.8f, 1f, 1f);
        }));
        thumbBtn.OnMouseOut = new UnityEngine.Events.UnityEvent();
        thumbBtn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (thumbSr) thumbSr.color = new Color(0.5f, 0.6f, 0.9f, 0.95f);
        }));

        // バー上部・下部の長押しボタン
        float arrowH = 0.25f;
        MakeScrollArrowButton("SbarUp", new Vector3(sbarCenterX, ListTop + arrowH * 0.5f, -0.6f),
            new Vector3(SbarW, arrowH, 1f), "▲", -1f);
        MakeScrollArrowButton("SbarDown", new Vector3(sbarCenterX, ListTop - SbarH - arrowH * 0.5f, -0.6f),
            new Vector3(SbarW, arrowH, 1f), "▼", 1f);

        // 初期スクロール適用
        ApplyScroll();

        // 詳細エリア
        BuildRoleDetailArea();
    }

    private static void MakeScrollArrowButton(string name, Vector3 pos, Vector3 scale,
        string label, float scrollDir)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(guidePanel.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.layer = 5;

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = new Color(0.2f, 0.2f, 0.4f, 0.9f);
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 9;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        var btn = obj.AddComponent<PassiveButton>();
        btn.Colliders = new Collider2D[] { col };
        btn.OnClick = new Button.ButtonClickedEvent();
        btn.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            scrollY += scrollDir * 0.44f;
            ApplyScroll();
        }));
        btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() => sr.color = new Color(0.4f, 0.4f, 0.7f, 1f)));
        btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() => sr.color = new Color(0.2f, 0.2f, 0.4f, 0.9f)));
    }

    private static void ApplyScroll()
    {
        if (scrollContent == null) return;
        scrollY = Mathf.Clamp(scrollY, 0f, Mathf.Max(0f, maxScrollY));

        // ★ コンテナを上下にずらすだけ（SetActiveは使わない）
        scrollContent.transform.localPosition =
            new Vector3(0f, scrollY, 0f);

        // ★ スクロールバーつまみ位置更新
        UpdateScrollThumb();
    }

    private static void UpdateScrollThumb()
    {
        if (scrollThumb == null) return;

        float sbarCenterX = ListLeft + ListW + SbarW * 0.5f + -0.8f;
        float thumbH = scrollThumb.transform.localScale.y;
        float trackH = SbarH - thumbH;
        float t = maxScrollY > 0f ? scrollY / maxScrollY : 0f;
        float thumbY = ListTop - thumbH * 0.5f - t * trackH;
        scrollThumb.transform.localPosition = new Vector3(sbarCenterX, thumbY, -0.6f);

        // スクロールバードラッグ処理
        if (scrollBarDragging)
        {
            if (!Input.GetMouseButton(0))
            {
                scrollBarDragging = false;
                return;
            }
            var cam = Camera.main;
            if (cam == null) return;
            float mouseY = cam.ScreenToWorldPoint(Input.mousePosition).y;
            float delta = mouseY - scrollBarDragStartMouseY;
            // デルタをスクロール量に変換（トラック長に対する比率）
            float scrollDelta = trackH > 0.001f ? -(delta / trackH) * maxScrollY : 0f;
            scrollY = Mathf.Clamp(scrollBarDragStartScrollY + scrollDelta, 0f, maxScrollY);
            ApplyScroll();
        }
        else if (Input.GetMouseButton(0))
        {
            // つまみをクリックしたらドラッグ開始
            var cam = Camera.main;
            if (cam == null) return;
            var worldMouse = cam.ScreenToWorldPoint(Input.mousePosition);
            // パネルローカル座標に変換
            if (guidePanel == null) return;
            var localMouse = guidePanel.transform.InverseTransformPoint(worldMouse);
            float tx = scrollThumb.transform.localPosition.x;
            float ty = scrollThumb.transform.localPosition.y;
            float hw = SbarW * 0.5f, hh = thumbH * 0.5f;
            if (localMouse.x >= tx - hw && localMouse.x <= tx + hw &&
                localMouse.y >= ty - hh && localMouse.y <= ty + hh)
            {
                scrollBarDragging = true;
                scrollBarDragStartMouseY = worldMouse.y;
                scrollBarDragStartScrollY = scrollY;
            }
        }
    }

    private static void BuildRoleDetailArea()
    {
        if (selectedRole == CustomRoles.NotAssigned)
        {
            MakeText(guidePanel, "DetailHint",
                new Vector3(DetailX, 0.5f, -1f),
                "<color=#555555>← 役職を選択</color>",
                1.5f, TextAlignmentOptions.Center, new Vector2(DetailW, 1f));
            return;
        }

        var role = selectedRole;
        var colorCode = UtilsRoleText.GetRoleColorCode(role);
        var roleName = UtilsRoleText.GetRoleName(role);
        var info = role.GetRoleInfo();

        string detail;
        if (info?.Description != null)
        {
            detail = info.Description.FullFormatHelp;
        }
        else
        {
            string desc = GetString($"{role}Info");
            if (string.IsNullOrEmpty(desc) || desc == $"{role}Info")
                desc = GetString($"{role}InfoLong");
            if (string.IsNullOrEmpty(desc) || desc == $"{role}InfoLong")
                desc = "説明なし";
            detail = $"<size=120%><color={colorCode}><b>{roleName}</b></color></size>\n\n" +
                     $"<size=80%><color=#dddddd>{desc}</color></size>";
        }

        MakeText(guidePanel, "Detail", new Vector3(DetailX, 2.0f, -1f),
            detail, 1.55f, TextAlignmentOptions.TopLeft, new Vector2(DetailW, 4.6f));
    }

    // ヘルパー：パネル直下にスプライト
    private static void MakeSpriteOnPanel(string name, Vector3 pos, Vector3 scale, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(guidePanel.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.layer = 5;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = color;
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = order;
    }

    private static GameObject MakeSpriteChild(GameObject parent, string name, Vector3 localPos,
        Vector3 scale, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = scale;
        obj.layer = 5;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = color;
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = order;
        return obj;
    }

    private static void MakeSprite(GameObject parent, string name, Vector3 pos, Vector3 scale,
        Color color, int order = 5)
        => MakeSpriteChild(parent, name, pos, scale, color, order);

    private static TextMeshPro MakeText(GameObject parent, string name, Vector3 pos, string text,
        float size, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Vector2? rectSize = null)
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
        if (rectSize.HasValue)
        {
            tmp.rectTransform.sizeDelta = rectSize.Value;
            tmp.rectTransform.pivot = align == TextAlignmentOptions.Top || align == TextAlignmentOptions.Center
                ? new Vector2(0.5f, 1f) : new Vector2(0f, 1f);
        }
        return tmp;
    }

    private static void MakeTabButton(GameObject parent, string label, Vector3 pos,
        Color accentColor, bool isSelected, Action onClick)
    {
        var obj = new GameObject($"Tab_{label}");
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;

        Color bgColor = isSelected
            ? new Color(accentColor.r * 0.35f, accentColor.g * 0.35f, accentColor.b * 0.35f, 0.95f)
            : new Color(0.12f, 0.15f, 0.30f, 0.95f);

        var bg = MakeSpriteChild(obj, "BG", Vector3.zero, new Vector3(1.7f, 0.75f, 1f), bgColor, 6);
        var bgSr = bg.GetComponent<SpriteRenderer>();
        MakeSpriteChild(obj, "Bar", new Vector3(-0.77f, 0f, -0.1f),
            new Vector3(0.055f, 0.75f, 1f), accentColor, 7);

        var tmp = MakeText(obj, "Lbl", new Vector3(0.05f, -0.3f, -0.2f), label, 1.9f, TextAlignmentOptions.Center);
        tmp.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.7f, 0.75f);
        var btn = obj.AddComponent<PassiveButton>();
        btn.Colliders = new Collider2D[] { col };
        btn.OnClick = new Button.ButtonClickedEvent();
        btn.OnClick.AddListener((UnityEngine.Events.UnityAction)onClick);
        btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (!isSelected && bgSr) bgSr.color = new Color(0.2f, 0.25f, 0.40f, 0.95f);
        }));
        btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (!isSelected && bgSr) bgSr.color = bgColor;
        }));
    }

    public static void UpdateTick()
    {
        // H キーで開閉
        if (Input.GetKeyDown(KeyCode.H)) TogglePanel();

        if (!isPanelOpen) return;

        // マウスホイールでスクロール
        if (currentTab == GuideTab.RoleList && scrollContent != null)
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f)
            {
                scrollY -= wheel * 3.0f;
                ApplyScroll();
            }

            // スクロールバードラッグの継続処理
            UpdateScrollThumb();
        }

        // クリックアニメ
        if (_btnAnimActive && _btnRenderer != null)
        {
            _btnAnimTimer += Time.deltaTime;
            float t = _btnAnimTimer / 0.3f;
            float scale = 1f + 0.35f * Mathf.Sin(t * Mathf.PI);
            var go = _btnRenderer.gameObject;
            if (go != null) go.transform.localScale = new Vector3(0.45f * scale, 0.45f * scale, 1f);
            if (_btnAnimTimer >= 0.3f)
            {
                _btnAnimActive = false;
                if (go != null) go.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                _btnRenderer.color = Color.white;
            }
        }
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class RoleGuideUpdatePatch
{
    public static void Postfix()
    {
        if (!GameStates.IsInTask) return;
        RoleGuideButtonPatch.UpdateTick();
        if (RoleGuideButtonPatch.isPanelOpen && Minigame.Instance != null)
            RoleGuideButtonPatch.ClosePanel();
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SetVisible))]
public static class AutoCloseOnChatPatch
{
    public static void Postfix(bool visible)
    {
        if (visible && RoleGuideButtonPatch.isPanelOpen)
            RoleGuideButtonPatch.ClosePanel();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class ClosePanelOnMeetingPatch
{
    public static void Postfix() => RoleGuideButtonPatch.ClosePanel();
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
public static class ClosePanelOnGameEndPatch
{
    public static void Prefix() => RoleGuideButtonPatch.ClosePanel();
}
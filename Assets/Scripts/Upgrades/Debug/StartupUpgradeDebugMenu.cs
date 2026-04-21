#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class StartupUpgradeDebugMenu : MonoBehaviour
{
    private static StartupUpgradeDebugMenu _instance;

    private const float WindowMargin = 16f;
    private const float WindowBorderPadding = 8f;
    private const float MinUiScale = 0.45f;
    private const float ScaleStep = 0.05f;

    private const float BaseHeaderHeight = 62f;
    private const float BaseHeaderLineHeight = 18f;
    private const float BaseEntryHeight = 52f;
    private const float BaseEntryMinWidth = 150f;
    private const float BaseEntryGap = 6f;
    private const float BaseValueWidth = 76f;
    private const float BaseAdjustButtonWidth = 22f;
    private const float BaseAdjustButtonHeight = 18f;
    private const float BaseActionButtonHeight = 24f;
    private const float BaseActionButtonMinWidth = 92f;
    private const float BaseActionGap = 6f;

    private static readonly DebugAction[] s_actions =
    {
        new("Clear All", ClearAllUpgrades),
        new("Inf HP", ToggleInfiniteHealth),
        new("Full Heal", FullHeal),
        new("Spawn 1", instance => SpawnEnemies(instance, 1)),
        new("Spawn 5", instance => SpawnEnemies(instance, 5)),
        new("Spawn 10", instance => SpawnEnemies(instance, 10)),
        new("Floor 1", JumpToFloorOne),
        new("World 2", JumpToWorldTwo),
        new("Boss", JumpToBoss),
        new("Hide", instance => instance.HideMenu()),
    };

    private Rect _windowRect = new(24f, 24f, 400f, 620f);
    private bool _visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (_instance != null)
            return;

        var go = new GameObject(nameof(StartupUpgradeDebugMenu));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<StartupUpgradeDebugMenu>();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame)
            return;

        _visible = !_visible;
        if (_visible)
            StartupUpgradeDebugState.AlignWithUpgradeManager(UpgradeManager.Instance);
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        UpdateWindowRect();
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Debug Upgrades");
    }

    private void DrawWindow(int windowId)
    {
        Rect bodyRect = new(
            WindowBorderPadding,
            24f,
            _windowRect.width - WindowBorderPadding * 2f,
            _windowRect.height - 24f - WindowBorderPadding);

        UpgradeManager manager = UpgradeManager.Instance;
        List<UpgradeDisplaySO> displays = manager != null ? manager.GetAllUpgradeDisplays() : null;
        int displayCount = displays != null ? CountValidDisplays(displays) : 0;
        LayoutMetrics metrics = BuildLayoutMetrics(bodyRect.width, bodyRect.height, displayCount, s_actions.Length);

        DrawHeader(bodyRect, metrics, manager, displays);

        if (manager == null)
        {
            Rect infoRect = new(
                bodyRect.x,
                bodyRect.y + metrics.HeaderHeight,
                bodyRect.width,
                Mathf.Max(metrics.EntryHeight, bodyRect.height - metrics.HeaderHeight - metrics.FooterHeight));
            GUI.Label(infoRect, "UpgradeManager not available.");
            DrawFooterButtons(bodyRect, metrics);
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
            return;
        }

        DrawUpgradeGrid(bodyRect, metrics, manager, displays);
        DrawFooterButtons(bodyRect, metrics);
        GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
    }

    private void DrawHeader(Rect bodyRect, LayoutMetrics metrics, UpgradeManager manager, List<UpgradeDisplaySO> displays)
    {
        float lineHeight = BaseHeaderLineHeight * metrics.Scale;
        float lineGap = BaseEntryGap * metrics.Scale;
        float y = bodyRect.y;

        GUI.Label(new Rect(bodyRect.x, y, bodyRect.width, lineHeight), $"Scene: {SceneManager.GetActiveScene().name}");
        y += lineHeight + lineGap;

        if (manager == null || displays == null)
        {
            GUI.Label(new Rect(bodyRect.x, y, bodyRect.width, lineHeight), "UpgradeManager not available.");
            return;
        }

        int desiredTotal = 0;
        for (int i = 0; i < displays.Count; i++)
        {
            UpgradeDisplaySO display = displays[i];
            if (display == null)
                continue;
            desiredTotal += StartupUpgradeDebugState.GetCount(display.upgradeID);
        }

        GUI.Label(
            new Rect(bodyRect.x, y, bodyRect.width, lineHeight),
            $"Desired stacks: {desiredTotal} | Floor: {ResolveFloorLabel()}");
        y += lineHeight + lineGap;

        GUI.Label(
            new Rect(bodyRect.x, y, bodyRect.width, lineHeight),
            $"{(manager.CurrentPlayer != null ? "Live sync: on" : "Live sync: waiting for player")} | Inf HP: {(StartupUpgradeDebugState.InfiniteHealthEnabled ? "on" : "off")}");
    }

    private void DrawUpgradeGrid(Rect bodyRect, LayoutMetrics metrics, UpgradeManager manager, List<UpgradeDisplaySO> displays)
    {
        Rect gridRect = new(
            bodyRect.x,
            bodyRect.y + metrics.HeaderHeight,
            bodyRect.width,
            Mathf.Max(0f, bodyRect.height - metrics.HeaderHeight - metrics.FooterHeight));

        int displayIndex = 0;
        float y = gridRect.y;
        for (int row = 0; row < metrics.EntryRows && displayIndex < displays.Count; row++)
        {
            float x = gridRect.x;
            for (int column = 0; column < metrics.EntryColumns && displayIndex < displays.Count; column++)
            {
                UpgradeDisplaySO display = displays[displayIndex++];
                if (display == null)
                {
                    x += metrics.EntryWidth + metrics.EntryGap;
                    continue;
                }

                DrawEntry(new Rect(x, y, metrics.EntryWidth, metrics.EntryHeight), metrics, manager, display);
                x += metrics.EntryWidth + metrics.EntryGap;
            }

            y += metrics.EntryHeight + metrics.EntryGap;
        }
    }

    private void DrawEntry(Rect rect, LayoutMetrics metrics, UpgradeManager manager, UpgradeDisplaySO display)
    {
        int current = StartupUpgradeDebugState.GetCount(display.upgradeID);
        int maxStacks = manager.GetMaxStacksForUpgrade(display.upgradeID);
        bool atCap = maxStacks != -1 && current >= maxStacks;

        GUI.Box(rect, GUIContent.none);

        float innerPadding = 4f * metrics.Scale;
        float controlsHeight = metrics.AdjustButtonHeight;
        float controlsTop = rect.y + rect.height - innerPadding - controlsHeight;
        float titleHeight = Mathf.Max(14f * metrics.Scale, controlsTop - rect.y - innerPadding * 2f);
        float buttonStripWidth = metrics.AdjustButtonWidth * 2f + innerPadding;

        GUIStyle wrappedLabelStyle = GetWrappedEntryLabelStyle(metrics);
        Rect titleRect = new(
            rect.x + innerPadding,
            rect.y + innerPadding,
            rect.width - innerPadding * 2f,
            titleHeight);
        GUI.Label(titleRect, display.upgradeName, wrappedLabelStyle);

        Rect valueRect = new(
            rect.x + innerPadding,
            controlsTop,
            Mathf.Max(36f, rect.width - buttonStripWidth - innerPadding * 3f),
            metrics.AdjustButtonHeight);
        GUI.Label(valueRect, $"D {current} / C {manager.GetStack(display.upgradeID)}");

        float buttonY = controlsTop;
        float plusX = rect.x + rect.width - innerPadding - metrics.AdjustButtonWidth;
        float minusX = plusX - metrics.AdjustButtonWidth - innerPadding;

        if (GUI.Button(new Rect(minusX, buttonY, metrics.AdjustButtonWidth, metrics.AdjustButtonHeight), "-"))
            ChangeDesiredCount(manager, display.upgradeID, current - 1);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !atCap;
        if (GUI.Button(new Rect(plusX, buttonY, metrics.AdjustButtonWidth, metrics.AdjustButtonHeight), "+"))
            ChangeDesiredCount(manager, display.upgradeID, current + 1);
        GUI.enabled = previousEnabled;
    }

    private void DrawFooterButtons(Rect bodyRect, LayoutMetrics metrics)
    {
        Rect footerRect = new(
            bodyRect.x,
            bodyRect.y + bodyRect.height - metrics.FooterHeight,
            bodyRect.width,
            metrics.FooterHeight);

        int actionIndex = 0;
        float y = footerRect.y;
        for (int row = 0; row < metrics.ActionRows && actionIndex < s_actions.Length; row++)
        {
            int buttonsInRow = Mathf.Min(metrics.ActionColumns, s_actions.Length - actionIndex);
            float rowWidth = buttonsInRow * metrics.ActionButtonWidth + Mathf.Max(0, buttonsInRow - 1) * metrics.ActionGap;
            float x = footerRect.x + (footerRect.width - rowWidth) * 0.5f;

            for (int column = 0; column < buttonsInRow; column++)
            {
                DebugAction action = s_actions[actionIndex++];
                if (GUI.Button(new Rect(x, y, metrics.ActionButtonWidth, metrics.ActionButtonHeight), action.Label))
                    action.Callback(this);

                x += metrics.ActionButtonWidth + metrics.ActionGap;
            }

            y += metrics.ActionButtonHeight + metrics.ActionGap;
        }
    }

    private LayoutMetrics BuildLayoutMetrics(float availableWidth, float availableHeight, int displayCount, int actionCount)
    {
        float clampedWidth = Mathf.Max(220f, availableWidth);
        float clampedHeight = Mathf.Max(180f, availableHeight);

        for (float scale = 1f; scale >= MinUiScale; scale -= ScaleStep)
        {
            LayoutMetrics metrics = CreateMetricsForScale(clampedWidth, clampedHeight, displayCount, actionCount, scale);
            if (metrics.Fits)
                return metrics;
        }

        return CreateMetricsForScale(clampedWidth, clampedHeight, displayCount, actionCount, MinUiScale);
    }

    private LayoutMetrics CreateMetricsForScale(float availableWidth, float availableHeight, int displayCount, int actionCount, float scale)
    {
        float entryGap = BaseEntryGap * scale;
        float entryHeight = BaseEntryHeight * scale;
        float actionGap = BaseActionGap * scale;
        float actionButtonHeight = BaseActionButtonHeight * scale;
        float headerHeight = BaseHeaderHeight * scale;

        int actionColumns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + actionGap) / (BaseActionButtonMinWidth * scale + actionGap)));
        actionColumns = Mathf.Min(actionColumns, Mathf.Max(1, actionCount));
        int actionRows = Mathf.Max(1, Mathf.CeilToInt(actionCount / (float)actionColumns));

        float footerHeight = actionRows * actionButtonHeight + Mathf.Max(0, actionRows - 1) * actionGap;
        float gridHeight = Mathf.Max(entryHeight, availableHeight - headerHeight - footerHeight - actionGap);
        int maxRows = Mathf.Max(1, Mathf.FloorToInt((gridHeight + entryGap) / (entryHeight + entryGap)));

        int maxColumns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + entryGap) / (BaseEntryMinWidth * scale + entryGap)));
        int entryColumns = Mathf.Clamp(maxColumns, 1, Mathf.Max(1, displayCount));
        int entryRows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, displayCount) / (float)entryColumns));

        bool fits = entryRows <= maxRows;
        if (!fits)
            entryRows = maxRows;

        float entryWidth = (availableWidth - Mathf.Max(0, entryColumns - 1) * entryGap) / entryColumns;
        float actionButtonWidth = (availableWidth - Mathf.Max(0, actionColumns - 1) * actionGap) / actionColumns;

        return new LayoutMetrics
        {
            Fits = fits,
            Scale = scale,
            HeaderHeight = headerHeight,
            FooterHeight = footerHeight,
            EntryColumns = entryColumns,
            EntryRows = Mathf.Max(1, entryRows),
            EntryWidth = entryWidth,
            EntryHeight = entryHeight,
            EntryGap = entryGap,
            ActionColumns = actionColumns,
            ActionRows = actionRows,
            ActionButtonWidth = actionButtonWidth,
            ActionButtonHeight = actionButtonHeight,
            ActionGap = actionGap,
            AdjustButtonWidth = BaseAdjustButtonWidth * scale,
            AdjustButtonHeight = BaseAdjustButtonHeight * scale,
        };
    }

    private void UpdateWindowRect()
    {
        float width = Mathf.Max(280f, Screen.width - WindowMargin * 2f);
        float height = Mathf.Max(220f, Screen.height - WindowMargin * 2f);

        _windowRect.width = width;
        _windowRect.height = height;
        _windowRect.x = WindowMargin;
        _windowRect.y = WindowMargin;
    }

    private static int CountValidDisplays(List<UpgradeDisplaySO> displays)
    {
        int count = 0;
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null)
                count++;
        }

        return count;
    }

    private static string ResolveFloorLabel()
    {
        return GameplayHandler.Instance != null
            ? (GameplayHandler.Instance.CurrentFloorIndex + 1).ToString()
            : "-";
    }

    private static void ChangeDesiredCount(UpgradeManager manager, string upgradeId, int desiredCount)
    {
        StartupUpgradeDebugState.SetCount(upgradeId, desiredCount);
        SyncConfiguredUpgradesToActiveRun(manager);
    }

    private static void SyncConfiguredUpgradesToActiveRun(UpgradeManager manager)
    {
        if (manager == null)
            return;

        PlayerController player = ResolvePlayerController();
        if (player == null)
            return;

        List<UpgradeDisplaySO> displays = manager.GetAllUpgradeDisplays();
        foreach (var display in displays)
        {
            if (display == null || string.IsNullOrEmpty(display.upgradeID))
                continue;

            int desired = StartupUpgradeDebugState.GetCount(display.upgradeID);
            int current = manager.GetStack(display.upgradeID);

            while (current < desired)
            {
                if (!manager.ApplyUpgrade(display.upgradeID, player))
                    break;
                current++;
            }

            while (current > desired)
            {
                manager.RevokeUpgrade(display.upgradeID, player);
                current--;
            }
        }
    }

    private static void ClearAllUpgrades(StartupUpgradeDebugMenu _)
    {
        StartupUpgradeDebugState.Clear();
        SyncConfiguredUpgradesToActiveRun(UpgradeManager.Instance);
    }

    private static void ToggleInfiniteHealth(StartupUpgradeDebugMenu _)
    {
        StartupUpgradeDebugState.InfiniteHealthEnabled = !StartupUpgradeDebugState.InfiniteHealthEnabled;
    }

    private static void FullHeal(StartupUpgradeDebugMenu _)
    {
        PlayerHealth playerHealth = ResolvePlayerHealth();
        if (playerHealth == null)
            return;

        PlayerStats playerStats = playerHealth.GetComponent<PlayerStats>();
        if (playerStats == null)
            return;

        playerHealth.Heal(playerStats.MaxHealth);
    }

    private static void JumpToFloorOne(StartupUpgradeDebugMenu _)
    {
        GameplayHandler.DebugJumpToGameplayFloor(0);
    }

    private static void JumpToWorldTwo(StartupUpgradeDebugMenu _)
    {
        GameplayHandler.DebugJumpToGameplayFloor(WorldProgression.WorldTwoStartFloorIndex);
    }

    private static void JumpToBoss(StartupUpgradeDebugMenu _)
    {
        GameplayHandler.DebugJumpToBoss();
    }

    private static void SpawnEnemies(StartupUpgradeDebugMenu _, int count)
    {
        if (count <= 0)
            return;

        PlayerController player = ResolvePlayerController();
        MapSpawner spawner = MapSpawner.Instance;
        if (player == null || spawner == null)
            return;

        float radius = Mathf.Lerp(1.5f, 4f, Mathf.InverseLerp(1f, 10f, count));
        Vector2 center = player.transform.position;
        for (int i = 0; i < count; i++)
            spawner.SpawnDebugRandomEnemyNear(center, radius);
    }

    private void HideMenu()
    {
        _visible = false;
    }

    private static PlayerHealth ResolvePlayerHealth()
    {
        PlayerController player = ResolvePlayerController();
        return player != null ? player.GetComponent<PlayerHealth>() : null;
    }

    private static PlayerController ResolvePlayerController()
    {
        UpgradeManager manager = UpgradeManager.Instance;
        if (manager != null && manager.CurrentPlayer != null)
            return manager.CurrentPlayer;

        return FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    private static GUIStyle GetWrappedEntryLabelStyle(LayoutMetrics metrics)
    {
        GUIStyle style = new(GUI.skin.label)
        {
            wordWrap = true,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.UpperLeft,
            fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * metrics.Scale))
        };
        return style;
    }

    private readonly struct DebugAction
    {
        public DebugAction(string label, Action<StartupUpgradeDebugMenu> callback)
        {
            Label = label;
            Callback = callback;
        }

        public string Label { get; }
        public Action<StartupUpgradeDebugMenu> Callback { get; }
    }

    private struct LayoutMetrics
    {
        public bool Fits;
        public float Scale;
        public float HeaderHeight;
        public float FooterHeight;
        public int EntryColumns;
        public int EntryRows;
        public float EntryWidth;
        public float EntryHeight;
        public float EntryGap;
        public int ActionColumns;
        public int ActionRows;
        public float ActionButtonWidth;
        public float ActionButtonHeight;
        public float ActionGap;
        public float AdjustButtonWidth;
        public float AdjustButtonHeight;
    }
}
#endif

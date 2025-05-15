using UnityEngine;
using System.Collections.Generic;

public class LootHighlighterDebugMenu : MonoBehaviour
{
    // Reference to main plugin
    private static LootHighlight _mainPlugin;

    // Debug menu settings
    private static bool _showDebugMenu = false;
    private static Rect _windowRect = new Rect(320, 20, 300, 400); // Moved right to avoid overlapping with other debug menus
    private static int _windowId = 10234;

    // Display categories
    private static Dictionary<string, bool> _categoryEnabled = new Dictionary<string, bool>()
    {
        { "Items", true },
        { "Containers", true },
        { "Corpses", true }
    };

    // Category colors
    private static Dictionary<string, Color> _categoryColors = new Dictionary<string, Color>()
    {
        { "Items", Color.red },
        { "Containers", Color.green },
        { "Corpses", Color.yellow }
    };

    // Settings
    private static float _detectionRadius = 10f;
    private static float _checkInterval = 0.5f;

    // Statistics
    private static int _totalHighlighted = 0;
    private static Dictionary<string, int> _categoryStats = new Dictionary<string, int>();

    // GUI styles
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _buttonStyle;
    private Vector2 _scrollPosition = Vector2.zero;
    private Color _defaultGuiColor;

    // Registered to global debug system
    private static bool _registeredToGlobalDebug = false;

    public static void Initialize(LootHighlight plugin)
    {
        _mainPlugin = plugin;
    }

    void Start()
    {
        // Initialize GUI styles on start
        InitializeGUIStyles();
    }

    void InitializeGUIStyles()
    {
        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.normal.textColor = Color.white;
        _labelStyle.fontSize = 12;

        _headerStyle = new GUIStyle(GUI.skin.label);
        _headerStyle.normal.textColor = Color.yellow;
        _headerStyle.fontSize = 14;
        _headerStyle.fontStyle = FontStyle.Bold;

        _buttonStyle = new GUIStyle(GUI.skin.button);
        _buttonStyle.normal.textColor = Color.white;
        _buttonStyle.fontSize = 12;

        _defaultGuiColor = GUI.color;
    }

    void Update()
    {
        // Register with global debug system if available
        if (!_registeredToGlobalDebug)
        {
            RegisterWithGlobalDebugSystem();
        }

        // Toggle debug menu with F12
        if (Input.GetKeyDown(KeyCode.F12))
        {
            _showDebugMenu = !_showDebugMenu;
        }
    }

    private void RegisterWithGlobalDebugSystem()
    {
        // Try to find global debug controller if it exists
        GameObject debugController = GameObject.Find("DebugMenuController");

        if (debugController != null)
        {
            // If found, we could register with it here
            // This is just a placeholder - implement based on the actual debug system
            Debug.Log("LootHighlighter: Registered with global debug system");
            _registeredToGlobalDebug = true;
        }
        else
        {
            // If not found, we'll just use our standalone implementation
            _registeredToGlobalDebug = true; // Mark as registered anyway to stop checking
        }
    }

    void OnGUI()
    {
        if (!_showDebugMenu)
            return;

        // Create debug window
        _windowRect = GUI.Window(_windowId, _windowRect, DrawDebugWindow, "Loot Highlighter Debug");
    }

    void DrawDebugWindow(int windowID)
    {
        // Make sure we have GUI styles
        if (_labelStyle == null)
            InitializeGUIStyles();

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        // Status section
        GUILayout.Label("Status", _headerStyle);

        // Display plugin status
        GUILayout.BeginHorizontal();
        GUILayout.Label("Plugin status:", _labelStyle, GUILayout.Width(120));
        GUI.color = _mainPlugin.IsEnabled ? Color.green : Color.red;
        if (GUILayout.Button(_mainPlugin.IsEnabled ? "Enabled" : "Disabled", _buttonStyle))
        {
            _mainPlugin.ToggleHighlighting();
        }
        GUI.color = _defaultGuiColor;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Settings section
        GUILayout.Label("Settings", _headerStyle);

        // Detection radius slider
        GUILayout.BeginHorizontal();
        GUILayout.Label("Detection radius:", _labelStyle, GUILayout.Width(120));
        _detectionRadius = GUILayout.HorizontalSlider(_detectionRadius, 5f, 30f, GUILayout.Width(100));
        GUILayout.Label($"{_detectionRadius:F1}m", _labelStyle, GUILayout.Width(50));
        GUILayout.EndHorizontal();

        // Check interval slider
        GUILayout.BeginHorizontal();
        GUILayout.Label("Refresh interval:", _labelStyle, GUILayout.Width(120));
        _checkInterval = GUILayout.HorizontalSlider(_checkInterval, 0.1f, 2.0f, GUILayout.Width(100));
        GUILayout.Label($"{_checkInterval:F1}s", _labelStyle, GUILayout.Width(50));
        GUILayout.EndHorizontal();

        // Show distance toggle
        GUILayout.BeginHorizontal();
        GUILayout.Label("Show distance:", _labelStyle, GUILayout.Width(120));
        _mainPlugin.ShowDistanceInLabel = GUILayout.Toggle(_mainPlugin.ShowDistanceInLabel, "", GUILayout.Width(20));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Categories section
        GUILayout.Label("Categories", _headerStyle);

        foreach (string category in _categoryEnabled.Keys)
        {
            GUILayout.BeginHorizontal();

            // Enable/disable category
            GUILayout.Label($"{category}:", _labelStyle, GUILayout.Width(80));
            bool wasEnabled = _categoryEnabled[category];
            _categoryEnabled[category] = GUILayout.Toggle(_categoryEnabled[category], "", GUILayout.Width(20));

            // Refresh if category toggled
            if (wasEnabled != _categoryEnabled[category])
            {
                _mainPlugin.ForceRefresh();
            }

            // Category color picker
            GUI.color = _categoryColors[category];
            if (GUILayout.Button("Color", _buttonStyle, GUILayout.Width(50)))
            {
                // Cycle through some preset colors
                _categoryColors[category] = CycleColor(_categoryColors[category]);
                _mainPlugin.ForceRefresh();
            }
            GUI.color = _defaultGuiColor;

            // Show count if we have stats
            if (_categoryStats.ContainsKey(category))
            {
                GUILayout.Label($"Count: {_categoryStats[category]}", _labelStyle);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // Statistics section
        GUILayout.Label("Statistics", _headerStyle);
        GUILayout.Label($"Total objects highlighted: {_totalHighlighted}", _labelStyle);

        GUILayout.Space(10);

        // Action buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Now", _buttonStyle))
        {
            _mainPlugin.ForceRefresh();
        }

        if (GUILayout.Button("Clear All", _buttonStyle))
        {
            _mainPlugin.ClearAllHighlights();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Hotkey info
        GUILayout.Label("Hotkeys", _headerStyle);
        GUILayout.Label("F1: Toggle highlighting", _labelStyle);
        GUILayout.Label("F12: Toggle debug menu", _labelStyle);

        GUILayout.EndScrollView();

        // Allow window to be dragged
        GUI.DragWindow();
    }

    private Color CycleColor(Color currentColor)
    {
        // Simple color cycle through presets
        if (currentColor == Color.red) return Color.green;
        if (currentColor == Color.green) return Color.blue;
        if (currentColor == Color.blue) return Color.yellow;
        if (currentColor == Color.yellow) return Color.cyan;
        if (currentColor == Color.cyan) return Color.magenta;
        if (currentColor == Color.magenta) return Color.white;
        return Color.red;
    }

    // Public static methods to get values from outside
    public static float GetDetectionRadius()
    {
        return _detectionRadius;
    }

    public static float GetCheckInterval()
    {
        return _checkInterval;
    }

    public static bool IsCategoryEnabled(string category)
    {
        if (_categoryEnabled.ContainsKey(category))
            return _categoryEnabled[category];
        return false;
    }

    public static Color GetCategoryColor(string category)
    {
        if (_categoryColors.ContainsKey(category))
            return _categoryColors[category];
        return Color.white;
    }

    public static void UpdateStats(int totalCount, Dictionary<string, int> categoryStats)
    {
        _totalHighlighted = totalCount;
        _categoryStats = categoryStats;
    }

    // For external control
    public static void SetVisibility(bool visible)
    {
        _showDebugMenu = visible;
    }
}
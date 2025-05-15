using BepInEx;
using UnityEngine;
using EFT.Interactive;
using System.Collections.Generic;
using System;
using EFT;

[BepInPlugin("com.karmaMGL.loothighlight", "Loot Highlighter", "0.1.0")]
public class LootHighlight : BaseUnityPlugin
{
    // Config settings
    private float detectionRadius = 10f;
    private float checkInterval = 0.5f; 
    private float lastCheckTime = 0f;
    private bool isEnabled = true;
    public bool ShowDistanceInLabel = false;

    // Static instance for access from other components
    public static LootHighlight Instance { get; private set; }

    // Track highlighted objects to manage cleanup
    private Dictionary<int, HighlightInfo> highlightedObjects = new Dictionary<int, HighlightInfo>();
    private Dictionary<string, int> categoryStats = new Dictionary<string, int>()
    {
        { "Items", 0 },
        { "Containers", 0 },
        { "Corpses", 0 }
    };

    // Debug menu reference
    private LootHighlighterDebugMenu debugMenu;
    private GameObject debugMenuObj;

    // Public property for Debug Menu
    public bool IsEnabled => isEnabled;

    // Debug integration variables
    private GameObject globalDebugController;
    private bool registeredWithGlobalDebug = false;

    // Class to store highlight information
    private class HighlightInfo
    {
        public GameObject LabelObject;
        public Light HighlightLight;
        public float LastSeenTime;
        public string Category;

        public HighlightInfo(GameObject label, Light light, string category)
        {
            LabelObject = label;
            HighlightLight = light;
            LastSeenTime = Time.time;
            Category = category;
        }
    }

    void Awake()
    {
        // Set static instance
        Instance = this;

        // Log plugin initialization
        Logger.LogInfo("Loot Highlighter plugin is starting...");
    }

    void Start()
    {
        // Initialize debug menu
        debugMenuObj = new GameObject("LootHighlighterDebugMenu");
        debugMenu = debugMenuObj.AddComponent<LootHighlighterDebugMenu>();
        LootHighlighterDebugMenu.Initialize(this);
        DontDestroyOnLoad(debugMenuObj);

        // Try to find global debug controller
        TryRegisterWithGlobalDebug();

        Logger.LogInfo("Loot Highlighter plugin initialized. Press F1 to toggle highlighting, F12 for debug menu.");
    }

    private void TryRegisterWithGlobalDebug()
    {
        // Look for a global debug controller - this is just a placeholder implementation
        // You would need to adapt this to work with whatever debug system is actually used
        globalDebugController = GameObject.Find("DebugMenuController");

        if (globalDebugController != null)
        {
            registeredWithGlobalDebug = true;
            Logger.LogInfo("Registered with global debug system");
        }
    }

    void Update()
    {
        // Toggle highlight system with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ToggleHighlighting();
        }

        if (!isEnabled) return;

        // Update settings from debug menu
        detectionRadius = LootHighlighterDebugMenu.GetDetectionRadius();
        checkInterval = LootHighlighterDebugMenu.GetCheckInterval();

        // Check for loot periodically
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            DetectNearbyLoot();
            CleanupStaleHighlights();
            UpdateStats();
        }
    }

    // Update statistics for debug menu
    private void UpdateStats()
    {
        // Reset counts
        foreach (var category in categoryStats.Keys)
        {
            categoryStats[category] = 0;
        }

        // Count by category
        foreach (var highlight in highlightedObjects.Values)
        {
            if (categoryStats.ContainsKey(highlight.Category))
            {
                categoryStats[highlight.Category]++;
            }
        }

        // Update debug menu
        LootHighlighterDebugMenu.UpdateStats(highlightedObjects.Count, categoryStats);
    }

    // Public method for debug menu to call
    public void ToggleHighlighting()
    {
        isEnabled = !isEnabled;
        Logger.LogInfo($"Loot Highlighting {(isEnabled ? "enabled" : "disabled")}");

        // If disabled, clean up all highlights
        if (!isEnabled)
        {
            CleanupAllHighlights();
        }
    }

    // Force refresh from debug menu
    public void ForceRefresh()
    {
        CleanupAllHighlights();
        DetectNearbyLoot();
        UpdateStats();
    }

    // Public method to clear all highlights
    public void ClearAllHighlights()
    {
        CleanupAllHighlights();
        UpdateStats();
    }

    private void DetectNearbyLoot()
    {
        if (Camera.main == null) return;

        Vector3 playerPos = Camera.main.transform.position;

        Collider[] colliders = Physics.OverlapSphere(playerPos, detectionRadius);
        HashSet<int> foundObjects = new HashSet<int>();

        foreach (var collider in colliders)
        {
            if (collider == null || collider.gameObject == null) continue;

            int objectId = collider.gameObject.GetInstanceID();
            foundObjects.Add(objectId);

            // Update last seen time if already tracked
            if (highlightedObjects.ContainsKey(objectId))
            {
                highlightedObjects[objectId].LastSeenTime = Time.time;
                continue;
            }

            // Skip objects in inventories or UI
            if (IsInPlayerInventory(collider.gameObject))
            {
                continue;
            }

            // Check various loot types and create labels if needed
            var lootItem = collider.GetComponent<LootItem>();
            if (lootItem != null && LootHighlighterDebugMenu.IsCategoryEnabled("Items"))
            {
                string displayName = lootItem.Item?.Template?.Name?.Localized() ?? "Item";

                if (ShowDistanceInLabel)
                {
                    float distance = Vector3.Distance(playerPos, collider.transform.position);
                    displayName += $" ({distance:F1}m)";
                }
                CreateHighlight(lootItem.gameObject, displayName, objectId, "Items");
                continue;
            }

            var corpse = collider.GetComponent<ObservedCorpse>() ?? collider.GetComponent<Corpse>();
            if (corpse != null && LootHighlighterDebugMenu.IsCategoryEnabled("Corpses"))
            {
                string displayName = "Corpse";
                if (ShowDistanceInLabel)
                {
                    float distance = Vector3.Distance(playerPos, corpse.transform.position);
                    displayName += $" ({distance:F1}m)";
                }
                CreateHighlight(corpse.gameObject, displayName, objectId, "Corpses");
                continue;
            }

            var container = collider.GetComponent<LootableContainer>();
            if (container != null && LootHighlighterDebugMenu.IsCategoryEnabled("Containers"))
            {
                string containerName = container.gameObject.name.ToLower(); // or container.transform.root.name

                string containerType = DetermineContainerType(containerName);
                string displayName = $"Container ({containerType})";

                if (ShowDistanceInLabel)
                {
                    float distance = Vector3.Distance(playerPos, container.transform.position);
                    displayName += $" ({distance:F1}m)";
                }
                CreateHighlight(container.gameObject, displayName, objectId, "Containers");
                continue;
            }
        }

        // Mark objects no longer visible for cleanup
        foreach (var objectId in highlightedObjects.Keys)
        {
            if (!foundObjects.Contains(objectId))
            {
                highlightedObjects[objectId].LastSeenTime = -1; // Mark for immediate cleanup
            }
        }
    }

    private string DetermineContainerType(string containerName)
    {
        if (containerName.Contains("med"))
            return "Medical";
        else if (containerName.Contains("tech"))
            return "Tech";
        else if (containerName.Contains("ammo"))
            return "Ammo";
        else if (containerName.Contains("weapon"))
            return "Weapon";
        else if (containerName.Contains("tool"))
            return "Toolbox";
        else if (containerName.Contains("duffle"))
            return "Duffle Bag";
        else if (containerName.Contains("jacket"))
            return "Jacket";
        else if (containerName.Contains("cash"))
            return "Cash";
        else if (containerName.Contains("safe"))
            return "Safe";
        else if (containerName.Contains("drawer"))
            return "Drawer";
        else if (containerName.Contains("pc") || containerName.Contains("computer"))
            return "Computer";

        return "Unknown";
    }

    private bool IsInPlayerInventory(GameObject obj)
    {
        // Check if the object is in player inventory (implement based on EFT's inventory system)
        // This is a placeholder - you'll need to implement the actual check according to EFT's structure
        return obj.transform.root.name.Contains("Inventory") ||
               obj.transform.root.name.Contains("UI") ||
               obj.activeInHierarchy == false;
    }

    private void CreateHighlight(GameObject target, string labelText, int objectId, string category)
    {
        if (target == null) return;

        // Create label object
        GameObject labelObj = new GameObject("LootLabel");
        labelObj.transform.SetParent(target.transform);
        labelObj.transform.localPosition = new Vector3(0, 0.2f, 0);

        // Add text component
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = labelText;
        textMesh.characterSize = 0.03f;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;

        // Set color based on loot type from debug menu
        Color labelColor = LootHighlighterDebugMenu.GetCategoryColor(category);

        if (category == "Items" && target.TryGetComponent<LootItem>(out var loot))
        {
            string templateId = loot.Item?.TemplateId ?? "";
            labelColor = GetRarityColor(templateId);
        }

        textMesh.color = labelColor;

        // Make text face camera
        labelObj.AddComponent<FaceCamera>();

        // Add highlight light
        Light light = target.AddComponent<Light>();
        light.range = 2f;
        light.intensity = 1.5f;
        light.type = LightType.Point;
        light.color = labelColor;

        // Track this highlight for later cleanup
        highlightedObjects[objectId] = new HighlightInfo(labelObj, light, category);
    }

    private void CleanupStaleHighlights()
    {
        // Get list of keys to remove (can't modify dictionary during enumeration)
        List<int> objectsToRemove = new List<int>();
        float staleThreshold = 2.0f; // Remove highlights after 2 seconds of not seeing them

        foreach (var kvp in highlightedObjects)
        {
            // Remove immediately marked objects or those not seen for a while
            if (kvp.Value.LastSeenTime < 0 || (Time.time - kvp.Value.LastSeenTime > staleThreshold))
            {
                objectsToRemove.Add(kvp.Key);
            }
        }

        // Clean up all flagged objects
        foreach (int objectId in objectsToRemove)
        {
            CleanupHighlight(objectId);
        }
    }

    private void CleanupHighlight(int objectId)
    {
        if (!highlightedObjects.ContainsKey(objectId)) return;

        var info = highlightedObjects[objectId];

        // Clean up label
        if (info.LabelObject != null)
        {
            Destroy(info.LabelObject);
        }

        // Clean up light
        if (info.HighlightLight != null)
        {
            Destroy(info.HighlightLight);
        }

        // Remove from tracking
        highlightedObjects.Remove(objectId);
    }

    private void CleanupAllHighlights()
    {
        foreach (var objectId in new List<int>(highlightedObjects.Keys))
        {
            CleanupHighlight(objectId);
        }
        highlightedObjects.Clear();
    }

    void OnDestroy()
    {
        // Clean up everything when the plugin is unloaded
        CleanupAllHighlights();

        // Clean up debug menu
        if (debugMenuObj != null)
        {
            Destroy(debugMenuObj);
        }

        Logger.LogInfo("Loot Highlighter plugin unloaded");
    }

    private Color GetRarityColor(string templateId)
    {
        // Enhanced rarity system with more tiers
        if (string.IsNullOrEmpty(templateId))
            return Color.white;

        // Legendary/Ultra Rare (specific high-value items)
        if (templateId.StartsWith("5c0") || templateId.StartsWith("5fc"))
            return new Color(1f, 0f, 1f); // Purple for ultra rare

        // Rare items
        if (templateId.StartsWith("5c") || templateId.StartsWith("5a"))
            return Color.yellow;

        // Uncommon items
        if (templateId.StartsWith("59") || templateId.StartsWith("56"))
            return Color.cyan;

        // Common items
        if (templateId.StartsWith("54") || templateId.StartsWith("57"))
            return Color.white;

        // Default for unknown template IDs
        return Color.white;
    }
}
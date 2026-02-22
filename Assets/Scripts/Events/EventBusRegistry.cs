using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks every EventBus<T> type that has been used this session
/// and clears them all when a new scene loads.
/// </summary>
public class EventBusRegistry : MonoBehaviour
{
    public static EventBusRegistry Instance { get; private set; }

    /// <summary>
    /// Add bus types here that should NOT be cleared on scene load.
    /// Ex: typeof(EventBus<GameStartedEvent>) if you raise it before
    /// the gameplay scene is loaded.
    /// </summary>
    [SerializeField]
    private bool clearOnSceneLoad = true;

    // All EventBus<T> Clear() methods found via reflection at startup
    private static readonly List<MethodInfo> _clearMethods = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        DiscoverBuses();

        if (clearOnSceneLoad)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Don't clear on additive loads (Ex: UI scenes loaded on top)
        if (mode == LoadSceneMode.Additive) return;
        ClearAll();
    }
    
    // I am not entirely sure how this works, but google says it does.
    // Will figure it out and provide documentation later.

    /// <summary>
    /// Uses reflection to find all EventBus<T> types in the assembly
    /// and cache their Clear() methods. Called once at startup.
    /// </summary>
    private static void DiscoverBuses()
    {
        _clearMethods.Clear();
        var busGenericType = typeof(EventBus<>);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                // Look for static classes named EventBus<T> that have a Clear method
                if (!type.IsGenericType) continue;
                if (type.GetGenericTypeDefinition() != busGenericType) continue;

                var clearMethod = type.GetMethod("Clear",
                    BindingFlags.Public | BindingFlags.Static);

                if (clearMethod != null)
                    _clearMethods.Add(clearMethod);
            }
        }

        Debug.Log($"[EventBusRegistry] Discovered {_clearMethods.Count} event buses.");
    }

    /// <summary>Clears all known event buses. Safe to call manually (Ex: run reset).</summary>
    public static void ClearAll()
    {
        foreach (var method in _clearMethods)
            method.Invoke(null, null);

        Debug.Log("[EventBusRegistry] All event buses cleared.");
    }
}
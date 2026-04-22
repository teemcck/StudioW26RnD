using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EventBusRegistry : MonoBehaviour
{
    public static EventBusRegistry Instance { get; private set; }

    [SerializeField] private bool clearOnSceneLoad = true;

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
        if (mode == LoadSceneMode.Additive) return;
        ClearAll();
    }

    private static void DiscoverBuses()
    {
        _clearMethods.Clear();
        var busGenericType = typeof(EventBus<>);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsGenericType) continue;
                if (type.ContainsGenericParameters) continue;
                if (type.GetGenericTypeDefinition() != busGenericType) continue;

                var clearMethod = type.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
                if (clearMethod != null)
                    _clearMethods.Add(clearMethod);
            }
        }
    }

    public static void ClearAll()
    {
        foreach (var method in _clearMethods)
            method.Invoke(null, null);
    }
}

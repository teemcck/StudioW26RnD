using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic static event bus. One exists per event type T.
///
/// Usage:
///   Raise:       EventBus<PlayerDiedEvent>.Raise(new PlayerDiedEvent { ... });
///   Subscribe:   EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
///   Unsubscribe: EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
///
/// Always unsubscribe in OnDestroy to avoid stale references.
/// Use EventBusCleaner on a persistent GameObject to auto-clear on scene load.
/// </summary>
public static class EventBus<T> where T : struct
{
    private static readonly HashSet<IEventBinding<T>> _bindings = new();
    private static readonly List<IEventBinding<T>>    _toAdd    = new();
    private static readonly List<IEventBinding<T>>    _toRemove = new();

    private static bool _raising = false;   // guard against mid-raise mutation
    
    // Subscribe / Unsubscribe

    public static void Subscribe(Action<T> handler)
    {
        var binding = new EventBinding<T>(handler);
        if (_raising) _toAdd.Add(binding);
        else          _bindings.Add(binding);
    }

    /// <summary>
    /// Preferred subscription method, returns the binding so you can
    /// unsubscribe precisely even if the same handler is registered twice.
    /// </summary>
    public static IEventBinding<T> Register(Action<T> handler)
    {
        var binding = new EventBinding<T>(handler);
        if (_raising) _toAdd.Add(binding);
        else          _bindings.Add(binding);
        return binding;
    }

    public static void Unsubscribe(Action<T> handler)
    {
        foreach (var b in _bindings)
        {
            if (b.Matches(handler))
            {
                if (_raising) _toRemove.Add(b);
                else          _bindings.Remove(b);
                return;
            }
        }
    }

    public static void Unsubscribe(IEventBinding<T> binding)
    {
        if (_raising) _toRemove.Add(binding);
        else          _bindings.Remove(binding);
    }
    
    // Raise

    public static void Raise(T evt)
    {
        _raising = true;

        foreach (var binding in _bindings)
        {
            try   { binding.Invoke(evt); }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus<{typeof(T).Name}>] Handler threw: {e}");
            }
        }

        _raising = false;

        // Apply deferred mutations
        foreach (var b in _toAdd)    _bindings.Add(b);
        foreach (var b in _toRemove) _bindings.Remove(b);
        _toAdd.Clear();
        _toRemove.Clear();
    }
    
    // Cleanup

    /// <summary>Clears ALL subscribers. Called by EventBusRegistry on scene load.</summary>
    public static void Clear() => _bindings.Clear();

    public static int SubscriberCount => _bindings.Count;
}

// Binding interface + implementation

public interface IEventBinding<T>
{
    void Invoke(T evt);
    bool Matches(Action<T> handler);
}

public class EventBinding<T> : IEventBinding<T> where T : struct
{
    private readonly Action<T> _handler;
    public EventBinding(Action<T> handler) => _handler = handler;
    public void Invoke(T evt) => _handler?.Invoke(evt);
    public bool Matches(Action<T> handler) => _handler == handler;
}
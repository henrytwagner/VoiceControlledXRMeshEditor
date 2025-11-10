using System.Collections.Generic;
using System.Reflection;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Safeguards Meta's ActiveStateTracker components that are missing their required ActiveState reference.
/// Without a valid ActiveState, the stock component throws a NullReferenceException every frame.
/// This helper disables any broken tracker early in the scene so the log does not get flooded.
/// </summary>
public class MetaActiveStateGuard : MonoBehaviour
{
    private static FieldInfo _activeStateField;
    private static FieldInfo _gameObjectsField;
    private static FieldInfo _monoBehavioursField;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var guardHost = new GameObject("MetaActiveStateGuard");
        DontDestroyOnLoad(guardHost);
        guardHost.AddComponent<MetaActiveStateGuard>();
    }

    private void Awake()
    {
        CacheReflectionFields();
        SceneManager.sceneLoaded += OnSceneLoaded;
        FixTrackers();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FixTrackers();
    }

    private void FixTrackers()
    {
        var trackers = Resources.FindObjectsOfTypeAll<ActiveStateTracker>();
        foreach (var tracker in trackers)
        {
            if (tracker == null)
                continue;

            if (TrackerMissingActiveState(tracker))
            {
                Debug.LogWarning(
                    $"[MetaActiveStateGuard] Disabling ActiveStateTracker on '{tracker.gameObject.name}' because no ActiveState is assigned.");
                tracker.enabled = false;
                continue;
            }

            EnsureListsInitialized(tracker);
        }
    }

    private static void CacheReflectionFields()
    {
        if (_activeStateField != null)
            return;

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        _activeStateField = typeof(ActiveStateTracker).GetField("_activeState", flags);
        _gameObjectsField = typeof(ActiveStateTracker).GetField("_gameObjects", flags);
        _monoBehavioursField = typeof(ActiveStateTracker).GetField("_monoBehaviours", flags);
    }

    private static bool TrackerMissingActiveState(ActiveStateTracker tracker)
    {
        if (_activeStateField == null)
            return false;

        var activeState = _activeStateField.GetValue(tracker) as Object;
        return activeState == null;
    }

    private static void EnsureListsInitialized(ActiveStateTracker tracker)
    {
        if (_gameObjectsField != null && _gameObjectsField.GetValue(tracker) == null)
        {
            _gameObjectsField.SetValue(tracker, new List<GameObject>());
        }

        if (_monoBehavioursField != null && _monoBehavioursField.GetValue(tracker) == null)
        {
            _monoBehavioursField.SetValue(tracker, new List<MonoBehaviour>());
        }
    }
}


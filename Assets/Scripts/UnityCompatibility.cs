using UnityEngine;

/// <summary>
/// Compatibility helpers so Unity 6-style object lookup APIs work in Unity 2022.
/// </summary>
public static class UnityCompatibility
{
    /// <summary>
    /// Find any object of type T (works in both Unity 2022 and Unity 6).
    /// </summary>
    public static T FindAnyObject<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    public static T FindAnyObject<T>(bool includeInactive) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<T>(includeInactive);
#endif
    }

    public static T FindFirstObject<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    public static T FindFirstObject<T>(bool includeInactive) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<T>(includeInactive);
#endif
    }

    public static T[] FindAllObjects<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }

    public static T[] FindAllObjects<T>(bool includeInactive) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>(includeInactive);
#endif
    }
}


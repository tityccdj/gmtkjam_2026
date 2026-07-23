using UnityEngine;

/// <summary>
/// Singleton pattern for MonoBehaviour classes.
/// Usage: public class MyClass : Singleton<MyClass> { }
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static readonly object lockObject = new object();
    private static bool applicationIsQuitting = false;

    /// <summary>
    /// Gets the singleton instance. Creates one if it doesn't exist.
    /// </summary>
    public static T Instance
    {
        get
        {
            if (applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] Instance of {typeof(T)} already destroyed. Returning null.");
                return null;
            }

            lock (lockObject)
            {
                if (instance == null)
                {
                    // Try to find existing instance in scene
                    instance = FindFirstObjectByType<T>();

                    if (instance == null)
                    {
                        // Create new instance
                        GameObject singletonObject = new GameObject();
                        instance = singletonObject.AddComponent<T>();
                        singletonObject.name = $"{typeof(T).Name} (Singleton)";

                        Debug.Log($"[Singleton] Created instance of {typeof(T)}");
                    }
                }

                return instance;
            }
        }
    }

    /// <summary>
    /// Check if singleton instance exists without creating it
    /// </summary>
    public static bool HasInstance => instance != null;

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Debug.LogWarning($"[Singleton] Duplicate instance of {typeof(T)} found. Destroying duplicate.");
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        applicationIsQuitting = true;
    }
}

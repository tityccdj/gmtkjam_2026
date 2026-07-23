using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Effect
{
    public string name;
    public GameObject prefab;
    public int poolSize = 5;
    public bool autoReturnToPool = true;
    public float lifetime = 3f;
}

public class EffectManager : Singleton<EffectManager>
{
    [Header("Effect Library")]
    [SerializeField] private Effect[] effects;
    
    [Header("Pool Settings")]
    [SerializeField] private Transform poolParent;
    [SerializeField] private int defaultPoolSize = 10;
    
    private Dictionary<string, Effect> effectDictionary;
    private Dictionary<string, Queue<GameObject>> effectPools;
    private Dictionary<GameObject, string> activeEffects;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Create pool parent if not assigned
        if (poolParent == null)
        {
            GameObject poolObj = new GameObject("EffectPool");
            poolObj.transform.SetParent(transform);
            poolParent = poolObj.transform;
        }
        
        // Initialize dictionaries
        effectDictionary = new Dictionary<string, Effect>();
        effectPools = new Dictionary<string, Queue<GameObject>>();
        activeEffects = new Dictionary<GameObject, string>();
        
        // Build effect dictionary and pools
        foreach (Effect effect in effects)
        {
            if (!effectDictionary.ContainsKey(effect.name))
            {
                effectDictionary.Add(effect.name, effect);
                CreatePool(effect);
            }
            else
            {
                Debug.LogWarning($"Duplicate effect name: {effect.name}");
            }
        }
    }
    
    /// <summary>
    /// Create object pool for an effect
    /// </summary>
    private void CreatePool(Effect effect)
    {
        Queue<GameObject> pool = new Queue<GameObject>();
        int size = effect.poolSize > 0 ? effect.poolSize : defaultPoolSize;
        
        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(effect.prefab, poolParent);
            obj.name = $"{effect.name}_{i}";
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
        
        effectPools.Add(effect.name, pool);
    }
    
    /// <summary>
    /// Get effect from pool
    /// </summary>
    private GameObject GetFromPool(string effectName)
    {
        if (!effectPools.ContainsKey(effectName))
        {
            Debug.LogWarning($"Effect pool '{effectName}' not found!");
            return null;
        }
        
        Queue<GameObject> pool = effectPools[effectName];
        GameObject obj;
        
        // Get available object from pool
        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
        }
        else
        {
            // Expand pool if needed
            Effect effect = effectDictionary[effectName];
            obj = Instantiate(effect.prefab, poolParent);
            obj.name = $"{effectName}_expanded";
            Debug.Log($"Pool expanded for effect: {effectName}");
        }
        
        return obj;
    }
    
    /// <summary>
    /// Return effect to pool
    /// </summary>
    private void ReturnToPool(GameObject obj, string effectName)
    {
        if (obj == null) return;
        
        obj.SetActive(false);
        obj.transform.SetParent(poolParent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        
        if (effectPools.ContainsKey(effectName))
        {
            effectPools[effectName].Enqueue(obj);
        }
        
        if (activeEffects.ContainsKey(obj))
        {
            activeEffects.Remove(obj);
        }
    }
    
    #region Play Effects
    
    /// <summary>
    /// Play effect at position
    /// </summary>
    public GameObject PlayEffect(string effectName, Vector3 position)
    {
        return PlayEffect(effectName, position, Quaternion.identity, null);
    }
    
    /// <summary>
    /// Play effect at position with rotation
    /// </summary>
    public GameObject PlayEffect(string effectName, Vector3 position, Quaternion rotation)
    {
        return PlayEffect(effectName, position, rotation, null);
    }
    
    /// <summary>
    /// Play effect with parent transform
    /// </summary>
    public GameObject PlayEffect(string effectName, Transform parent)
    {
        return PlayEffect(effectName, parent.position, parent.rotation, parent);
    }
    
    /// <summary>
    /// Play effect with full control
    /// </summary>
    public GameObject PlayEffect(string effectName, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (!effectDictionary.ContainsKey(effectName))
        {
            Debug.LogWarning($"Effect '{effectName}' not found!");
            return null;
        }
        
        Effect effect = effectDictionary[effectName];
        GameObject effectObj = GetFromPool(effectName);
        
        if (effectObj == null) return null;
        
        // Setup transform
        effectObj.transform.position = position;
        effectObj.transform.rotation = rotation;
        effectObj.transform.SetParent(parent);
        
        // Activate effect
        effectObj.SetActive(true);
        
        // Start particle systems
        ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Clear();
            ps.Play();
        }
        
        // Track active effect
        activeEffects[effectObj] = effectName;
        
        // Auto return to pool
        if (effect.autoReturnToPool)
        {
            float duration = GetEffectDuration(effectObj, effect.lifetime);
            LeanTween.delayedCall(duration, () => ReturnToPool(effectObj, effectName));
        }
        
        return effectObj;
    }
    
    /// <summary>
    /// Play effect and follow transform
    /// </summary>
    public GameObject PlayEffectFollowTarget(string effectName, Transform target)
    {
        GameObject effectObj = PlayEffect(effectName, target.position, target.rotation, null);
        if (effectObj != null)
        {
            EffectFollower follower = effectObj.AddComponent<EffectFollower>();
            follower.Initialize(target);
        }
        return effectObj;
    }
    
    #endregion
    
    #region Stop Effects
    
    /// <summary>
    /// Stop specific effect instance
    /// </summary>
    public void StopEffect(GameObject effectObj)
    {
        if (effectObj != null && activeEffects.ContainsKey(effectObj))
        {
            string effectName = activeEffects[effectObj];
            
            // Stop all particle systems
            ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in particleSystems)
            {
                ps.Stop();
            }
            
            LeanTween.cancel(effectObj);
            ReturnToPool(effectObj, effectName);
        }
    }
    
    /// <summary>
    /// Stop all effects with given name
    /// </summary>
    public void StopAllEffects(string effectName)
    {
        List<GameObject> toRemove = new List<GameObject>();
        
        foreach (var kvp in activeEffects)
        {
            if (kvp.Value == effectName)
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (GameObject obj in toRemove)
        {
            StopEffect(obj);
        }
    }
    
    /// <summary>
    /// Stop all active effects
    /// </summary>
    public void StopAllEffects()
    {
        List<GameObject> toRemove = new List<GameObject>(activeEffects.Keys);
        
        foreach (GameObject obj in toRemove)
        {
            StopEffect(obj);
        }
    }
    
    #endregion
    
    #region Utility
    
    /// <summary>
    /// Get effect duration from particle systems
    /// </summary>
    private float GetEffectDuration(GameObject effectObj, float defaultDuration)
    {
        float maxDuration = 0f;
        
        ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particleSystems)
        {
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            if (duration > maxDuration)
            {
                maxDuration = duration;
            }
        }
        
        return maxDuration > 0 ? maxDuration : defaultDuration;
    }
    
    /// <summary>
    /// Check if effect exists in library
    /// </summary>
    public bool HasEffect(string effectName)
    {
        return effectDictionary.ContainsKey(effectName);
    }
    
    /// <summary>
    /// Get number of active effects
    /// </summary>
    public int GetActiveEffectCount()
    {
        return activeEffects.Count;
    }
    
    /// <summary>
    /// Get number of active effects by name
    /// </summary>
    public int GetActiveEffectCount(string effectName)
    {
        int count = 0;
        foreach (var kvp in activeEffects)
        {
            if (kvp.Value == effectName)
            {
                count++;
            }
        }
        return count;
    }
    
    #endregion
}

/// <summary>
/// Helper component to make effect follow a target
/// </summary>
public class EffectFollower : MonoBehaviour
{
    private Transform target;
    
    public void Initialize(Transform followTarget)
    {
        target = followTarget;
    }
    
    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position;
            transform.rotation = target.rotation;
        }
        else
        {
            Destroy(this);
        }
    }
}

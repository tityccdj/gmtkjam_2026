using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    [SerializeField]
    private AudioManager audioManager;
    [SerializeField]
    private EffectManager effectManager;
    [SerializeField]
    private SceneLoader sceneLoader;

    void Awake()
    {
        Instantiate(audioManager);
        Instantiate(effectManager);
        Instantiate(sceneLoader);
        Utility.Setup(this);
    }
}

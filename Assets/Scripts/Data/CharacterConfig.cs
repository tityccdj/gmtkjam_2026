using UnityEngine;

[CreateAssetMenu(fileName = "CharacterConfig", menuName = "GMTK/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "FIGHTER";

    [Header("Selection")]
    // Card art drawn on the character selection panel.
    public Sprite cardSprite;

    [Header("Game Scene")]
    // Spawned in place of the character sitting under PlayerPanel / EnemyPanel in
    // the Game scene. Leave empty to keep whatever the scene already has.
    public GameObject characterPrefab;
}

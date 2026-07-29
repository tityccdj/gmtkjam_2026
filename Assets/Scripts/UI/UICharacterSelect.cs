using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICharacterSelect : MonoBehaviour
{
    public struct Param
    {
        public Action onBack;
        public Action<CharacterConfig, int> onCharacterSelected;
    }

    [SerializeField]
    private SlidePanel slidePanel;
    [SerializeField]
    private Button backButton;
    [SerializeField]
    private TMP_Text titleText;
    [SerializeField]
    private Transform charactersContainer;
    [SerializeField]
    private GameObject cardTemplate;
    [SerializeField]
    private CharacterConfig[] characters;

    public CharacterConfig[] Characters => characters;

    // Local versus and free play pick the opponent automatically: whoever the
    // player did not take.
    public CharacterConfig GetOpponentOf(int index)
    {
        if (characters == null || characters.Length == 0)
        {
            return null;
        }
        return characters[(index + 1) % characters.Length];
    }

    public void Setup(Param param)
    {
        backButton.AddClickSound();
        backButton.onClick.AddListener(() => param.onBack?.Invoke());
        PopulateCharacters(param.onCharacterSelected);
    }

    private void PopulateCharacters(Action<CharacterConfig, int> onCharacterSelected)
    {
        for (int i = charactersContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(charactersContainer.GetChild(i).gameObject);
        }

        if (characters == null)
        {
            return;
        }

        for (int i = 0; i < characters.Length; i++)
        {
            CharacterConfig character = characters[i];
            if (character == null)
            {
                continue;
            }

            int index = i;
            GameObject card = Instantiate(cardTemplate, charactersContainer);
            card.name = character.name;
            card.SetActive(true);

            Image cardImage = card.transform.Find("Card")?.GetComponent<Image>()
                ?? card.GetComponent<Image>();
            if (cardImage != null && character.cardSprite != null)
            {
                cardImage.sprite = character.cardSprite;
            }

            var nameText = card.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (nameText != null)
            {
                nameText.text = character.displayName;
            }

            Button btn = card.GetComponent<Button>();
            btn.AddClickSound();
            btn.onClick.AddListener(() => onCharacterSelected?.Invoke(character, index));
        }
    }

    public void SetTitle(string text)
    {
        if (titleText != null)
        {
            titleText.text = text;
        }
    }

    public void SlideIn()
    {
        slidePanel.SlideIn();
        UIUtils.SelectFirstInteractable(charactersContainer, backButton);
    }

    public void SlideOut(Action onComplete = null) => slidePanel.SlideOut(onComplete);
}

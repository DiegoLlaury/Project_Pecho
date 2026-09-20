using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a simple end-of-combat screen when the fishing session ends.
/// </summary>
public sealed class FishingCombatResultScreen : MonoBehaviour
{
    private const string VictoryTitle = "VICTOIRE";
    private const string DefeatTitle = "DÉFAITE";
    private const string VictoryMessage = "Poisson capturé !";
    private const string DefeatMessage = "Le poisson s'est échappé.";
    private const string ContinueLabel = "CONTINUER";
    private const string ScreenName = "Combat Result Screen";
    private const string PanelName = "Dimmed Background";
    private const string CardName = "Result Card";
    private const string TitleName = "Result Title";
    private const string MessageName = "Result Message";
    private const string ContinueButtonName = "Continue Button";
    private const string ContinueTextName = "Text";
    private const int ResultSortingOrder = 200;
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float CardWidth = 560f;
    private const float CardHeight = 310f;
    private const float TitleFontSize = 52f;
    private const float MessageFontSize = 28f;
    private const float ButtonFontSize = 25f;
    private const float ButtonWidth = 260f;
    private const float ButtonHeight = 62f;
    private const float TitleVerticalOffset = 65f;
    private const float MessageVerticalOffset = -5f;
    private const float ButtonVerticalOffset = -90f;

    [Header("Required References")]
    [SerializeField] private FishingSessionController fishingSessionController;
    [SerializeField] private FishingInputReader fishingInputReader;
    [SerializeField] private FishingPlayerController fishingPlayerController;
    [SerializeField] private PlayerSpellController playerSpellController;

    private Canvas resultCanvas;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI messageText;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisibility;
    private bool wasFishingInputReaderEnabled;
    private bool wasFishingPlayerControllerEnabled;
    private bool wasPlayerSpellControllerEnabled;

    private void Awake()
    {
        ResolveReferences();
        CreateResultScreen();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (fishingSessionController != null)
        {
            fishingSessionController.StateChanged += HandleSessionStateChanged;
            HandleSessionStateChanged(fishingSessionController.State);
        }
    }

    private void OnDisable()
    {
        if (fishingSessionController != null)
        {
            fishingSessionController.StateChanged -= HandleSessionStateChanged;
        }
    }

    private void HandleSessionStateChanged(FishingState state)
    {
        if (state == FishingState.Caught)
        {
            ShowResult(VictoryTitle, VictoryMessage, new Color(0.3f, 0.95f, 0.55f, 1f));
        }
        else if (state == FishingState.Escaped)
        {
            ShowResult(DefeatTitle, DefeatMessage, new Color(1f, 0.42f, 0.42f, 1f));
        }
    }

    /// <summary>
    /// Closes the result screen and resets the fishing combat for another attempt.
    /// </summary>
    public void ContinueCombat()
    {
        if (resultCanvas != null)
        {
            resultCanvas.gameObject.SetActive(false);
        }

        SetGameplayControllersEnabled(true);
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisibility;

        playerSpellController?.ResetCombatState();

        if (fishingSessionController != null)
        {
            fishingSessionController.ResetSession();
        }
    }

    private void ShowResult(string title, string message, Color titleColor)
    {
        if (resultCanvas == null)
        {
            return;
        }

        previousCursorLockMode = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        SetGameplayControllersEnabled(false);
        titleText.text = title;
        titleText.color = titleColor;
        messageText.text = message;
        resultCanvas.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResolveReferences()
    {
        if (fishingSessionController == null)
        {
            fishingSessionController = FindFirstObjectByType<FishingSessionController>();
        }

        if (fishingInputReader == null)
        {
            fishingInputReader = FindFirstObjectByType<FishingInputReader>();
        }

        if (fishingPlayerController == null)
        {
            fishingPlayerController = FindFirstObjectByType<FishingPlayerController>();
        }

        if (playerSpellController == null)
        {
            playerSpellController = FindFirstObjectByType<PlayerSpellController>();
        }
    }

    private void SetGameplayControllersEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            if (fishingInputReader != null)
            {
                fishingInputReader.enabled = wasFishingInputReaderEnabled;
            }

            if (fishingPlayerController != null)
            {
                fishingPlayerController.enabled = wasFishingPlayerControllerEnabled;
            }

            if (playerSpellController != null)
            {
                playerSpellController.enabled = wasPlayerSpellControllerEnabled;
            }

            return;
        }

        if (fishingInputReader != null)
        {
            wasFishingInputReaderEnabled = fishingInputReader.enabled;
            fishingInputReader.enabled = false;
        }

        if (fishingPlayerController != null)
        {
            wasFishingPlayerControllerEnabled = fishingPlayerController.enabled;
            fishingPlayerController.enabled = false;
        }

        if (playerSpellController != null)
        {
            wasPlayerSpellControllerEnabled = playerSpellController.enabled;
            playerSpellController.enabled = false;
        }
    }

    private void CreateResultScreen()
    {
        GameObject canvasObject = new GameObject(
            ScreenName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        resultCanvas = canvasObject.GetComponent<Canvas>();
        resultCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        resultCanvas.sortingOrder = ResultSortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        canvasScaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage(PanelName, canvasObject.transform, new Color(0f, 0f, 0f, 0.65f));
        StretchToParent(background.rectTransform);

        Image card = CreateImage(CardName, background.transform, new Color(0.07f, 0.12f, 0.18f, 0.97f));
        ConfigureCenteredElement(card.rectTransform, new Vector2(CardWidth, CardHeight), Vector2.zero);

        titleText = CreateText(TitleName, card.transform, TitleFontSize, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Center;
        ConfigureCenteredElement(titleText.rectTransform, new Vector2(CardWidth - 40f, 70f), new Vector2(0f, TitleVerticalOffset));

        messageText = CreateText(MessageName, card.transform, MessageFontSize, FontStyles.Normal);
        messageText.alignment = TextAlignmentOptions.Center;
        ConfigureCenteredElement(messageText.rectTransform, new Vector2(CardWidth - 50f, 50f), new Vector2(0f, MessageVerticalOffset));

        Button continueButton = CreateContinueButton(card.transform);
        ConfigureCenteredElement(continueButton.GetComponent<RectTransform>(), new Vector2(ButtonWidth, ButtonHeight), new Vector2(0f, ButtonVerticalOffset));

        resultCanvas.gameObject.SetActive(false);
    }

    private Button CreateContinueButton(Transform parent)
    {
        Image buttonImage = CreateImage(ContinueButtonName, parent, new Color(0.16f, 0.48f, 0.8f, 1f));
        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(ContinueCombat);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.75f, 0.9f, 1f, 1f);
        colors.pressedColor = new Color(0.55f, 0.75f, 0.95f, 1f);
        button.colors = colors;

        TextMeshProUGUI buttonText = CreateText(ContinueTextName, buttonImage.transform, ButtonFontSize, FontStyles.Bold);
        buttonText.text = ContinueLabel;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;
        StretchToParent(buttonText.rectTransform);

        return button;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void ConfigureCenteredElement(RectTransform rectTransform, Vector2 size, Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
    }
}

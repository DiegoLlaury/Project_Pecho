using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public class CharData
{
    public string CharName;
    public List<string> CharDialogues = new List<string>();
    public Sprite SpriteNeutral;
    public Sprite SpriteSpeaking;
}

[System.Serializable]
public class SequenceDialogue
{
    public int CharID;
    public int DialogueID;
    public List<int> CharactersOnScreen = new List<int>();
}

public class UDS : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI CharNameText;
    public TextMeshProUGUI CharDialogueText;
    public Image SpriteLeft;
    public Image SpriteRight;
    public RectTransform SlotLeft;
    public RectTransform SlotRight;

    [Header("Sprite Feedback (Speaking / not speaking)")]
    public Vector3 ScaleSpeaking = Vector3.one;
    public Vector3 ScaleNotSpeaking = new Vector3(0.85f, 0.85f, 0.85f);
    public Color ColorSpeaking = Color.white;
    public Color ColorNotSpeaking = new Color(0.6f, 0.6f, 0.6f, 1f);
    public float TransitionDuration = 0.25f;

    [Header("Characters config")]
    public List<CharData> charList = new List<CharData>();

    [Header("Dialogue Timeline")]
    public List<SequenceDialogue> dialogueTimeline = new List<SequenceDialogue>();

    private int currentDialogueTimeStep = 0;
    private bool isDialogueActive = true;

    private Coroutine lerpLeftCoroutine;
    private Coroutine lerpRightCoroutine;

    void Start()
    {
        StartDialogue();
    }

    void Update()
    {
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame && isDialogueActive)
        {
            NextDialogue();
        }
    }

    void StartDialogue()
    {
        currentDialogueTimeStep = 0;
        isDialogueActive = true;

        if (dialogueTimeline.Count > 0)
        {
            ReadTimelineStep(currentDialogueTimeStep);
        }
        else
        {
            Debug.LogWarning("La liste dialogueTimeline est vide !");
        }
    }

    void ReadTimelineStep(int step)
    {
        int targetCharID = dialogueTimeline[step].CharID;
        int targetDialogueID = dialogueTimeline[step].DialogueID;
        List<int> onScreen = dialogueTimeline[step].CharactersOnScreen;

        DisplayCharDialogue(targetCharID, targetDialogueID, onScreen);
    }

    public void DisplayCharDialogue(int CharID, int DialogueID, List<int> charactersOnScreen)
    {
        if (CharID < charList.Count)
        {
            CharData UsedChar = charList[CharID];

            if (DialogueID < UsedChar.CharDialogues.Count)
            {
                CharNameText.text = UsedChar.CharName;
                CharDialogueText.text = UsedChar.CharDialogues[DialogueID];
            }
        }

        UpdateSprites(CharID, charactersOnScreen);
    }

    void UpdateSprites(int speakingCharID, List<int> charactersOnScreen)
    {
        SpriteLeft.gameObject.SetActive(false);
        SpriteRight.gameObject.SetActive(false);

        Image[] slots = { SpriteLeft, SpriteRight };
        RectTransform[] cadres = { SlotLeft, SlotRight };

        for (int i = 0; i < charactersOnScreen.Count && i < slots.Length; i++)
        {
            int charID = charactersOnScreen[i];

            if (charID < charList.Count)
            {
                CharData c = charList[charID];
                bool isSpeaking = (charID == speakingCharID);

                slots[i].gameObject.SetActive(true);
                slots[i].sprite = isSpeaking ? c.SpriteSpeaking : c.SpriteNeutral;

                Vector3 targetScale = isSpeaking ? ScaleSpeaking : ScaleNotSpeaking;
                Color targetColor = isSpeaking ? ColorSpeaking : ColorNotSpeaking;

                if (i == 0)
                {
                    if (lerpLeftCoroutine != null) StopCoroutine(lerpLeftCoroutine);
                    lerpLeftCoroutine = StartCoroutine(LerpSprite(slots[i], cadres[i], targetScale, targetColor));
                }
                else
                {
                    if (lerpRightCoroutine != null) StopCoroutine(lerpRightCoroutine);
                    lerpRightCoroutine = StartCoroutine(LerpSprite(slots[i], cadres[i], targetScale, targetColor));
                }
            }
        }
    }

    IEnumerator LerpSprite(Image image, RectTransform cadre, Vector3 targetScale, Color targetColor)
    {
        Vector3 startScale = cadre.localScale;
        Color startColor = image.color;
        float t = 0f;

        while (t < TransitionDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / TransitionDuration);

            cadre.localScale = Vector3.Lerp(startScale, targetScale, progress);
            image.color = Color.Lerp(startColor, targetColor, progress);

            yield return null;
        }

        cadre.localScale = targetScale;
        image.color = targetColor;
    }

    void NextDialogue()
    {
        currentDialogueTimeStep++;

        if (currentDialogueTimeStep < dialogueTimeline.Count)
        {
            ReadTimelineStep(currentDialogueTimeStep);
        }
        else
        {
            EndDialogue();
        }
    }

    void EndDialogue()
    {
        isDialogueActive = false;
        CharNameText.text = "";
        CharDialogueText.text = "Fin de la discussion.";
        SpriteLeft.gameObject.SetActive(false);
        SpriteRight.gameObject.SetActive(false);
        Debug.Log("La discussion est terminée !");
    }
}
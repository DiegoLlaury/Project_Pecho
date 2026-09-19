using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public class CharData
{
    public string CharName;
    public List<string> CharDialogues = new List<string>();
}

[System.Serializable]
public class SequenceDialogue
{
    public int CharID;
    public int DialogueID;
}

public class UDS : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI CharNameText;
    public TextMeshProUGUI CharDialogueText;

    [Header("Characters config")]
    public List<CharData> charList = new List<CharData>();

    [Header("Dialogue Timeline")]
    public List<SequenceDialogue> dialogueTimeline = new List<SequenceDialogue>();

    private int currentDialogueTimeStep = 0;
    private bool isDialogueActive = true;

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

        DisplayCharDialogue(targetCharID, targetDialogueID);
    }

    public void DisplayCharDialogue(int CharID, int DialogueID)
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
        Debug.Log("La discussion est terminée !");
    }
}

using UnityEngine;
using TMPro;

public class DialogueUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject DialoguePanel;
    public TMP_Text   SpeakerLabel;
    public TMP_Text   BodyText;
    public TMP_Text   ContinuePrompt;

    [Header("Strings")]
    public string ContinueString = "[E] Continue";
    public string CloseString    = "[E] Close";

    void OnEnable()
    {
        DialogueInteractable.OnLineShown    += HandleLine;
        DialogueInteractable.OnDialogueEnded += HandleEnd;
    }

    void OnDisable()
    {
        DialogueInteractable.OnLineShown    -= HandleLine;
        DialogueInteractable.OnDialogueEnded -= HandleEnd;
    }

    void Start()
    {
        if (DialoguePanel) DialoguePanel.SetActive(false);
    }

    void HandleLine(DialogueLine line, int index, int total, string formatted)
    {
        if (DialoguePanel) DialoguePanel.SetActive(true);

        if (SpeakerLabel != null)
        {
            bool has = !string.IsNullOrWhiteSpace(line.SpeakerName);
            SpeakerLabel.gameObject.SetActive(has);
            if (has) SpeakerLabel.text = line.SpeakerName;
        }

        if (BodyText != null)
            BodyText.text = SpeakerLabel != null ? line.Text : formatted;

        if (ContinuePrompt != null)
            ContinuePrompt.text = (index == total - 1) ? CloseString : ContinueString;
    }

    void HandleEnd()
    {
        if (DialoguePanel) DialoguePanel.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  DialogueUI
//
//  Drop this on a Canvas or UI manager GameObject.
//  It listens to DialogueInteractable's static events and drives a simple
//  dialogue box — no changes needed to DialogueInteractable.
//
//  Inspector setup
//  ───────────────
//  • Dialogue Panel   — the root panel GameObject (will be shown/hidden)
//  • Speaker Label    — TMP_Text for the speaker name  (can be null)
//  • Body Text        — TMP_Text for the line content
//  • Continue Prompt  — TMP_Text that says "[E] Continue" / "[E] Close"
//
//  The panel starts hidden and is shown automatically when a line fires.
// ─────────────────────────────────────────────────────────────────────────────
public class DialogueUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Root panel to show/hide.")]
    public GameObject DialoguePanel;

    [Tooltip("Displays the speaker name. Leave unassigned to hide speaker label.")]
    public TMP_Text SpeakerLabel;

    [Tooltip("Displays the line text.")]
    public TMP_Text BodyText;

    [Tooltip("Prompt shown at the bottom, e.g. '[E] Continue'.")]
    public TMP_Text ContinuePrompt;

    [Header("Strings")]
    public string ContinueString = "[E] Continue";
    public string CloseString    = "[E] Close";

    // ─────────────────────────────────────────────────────────────────────────

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
        if (DialoguePanel != null)
            DialoguePanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────

    void HandleLine(DialogueLine line, int index, int total)
    {
        if (DialoguePanel != null) DialoguePanel.SetActive(true);

        // Speaker
        if (SpeakerLabel != null)
        {
            bool hasSpeaker = !string.IsNullOrEmpty(line.SpeakerName);
            SpeakerLabel.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker) SpeakerLabel.text = line.SpeakerName;
        }

        // Body
        if (BodyText != null)
            BodyText.text = line.Text;

        // Prompt — show "Close" on the last line
        if (ContinuePrompt != null)
            ContinuePrompt.text = index == total - 1 ? CloseString : ContinueString;
    }

    void HandleEnd()
    {
        if (DialoguePanel != null)
            DialoguePanel.SetActive(false);
    }
}

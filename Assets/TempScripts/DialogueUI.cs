// ─────────────────────────────────────────────────────────────────────────────
//  DialogueUI.cs
//
//  CHANGES FROM ORIGINAL:
//    • Extends ObserverMonoBehaviour instead of MonoBehaviour.
//    • The raw OnEnable/OnDisable subscription block is replaced by
//      Subscribe() and Unsubscribe(). This is the exact scenario
//      ObserverMonoBehaviour was designed for: a View that listens to
//      static events fired by a ViewModel/Interactable and needs those
//      subscriptions to be lifecycle-safe (no subscribe before Start,
//      no double-subscribe on re-enable, no leaks on disable).
//
//  MVVM role: VIEW
//    • Receives data from DialogueInteractable (ViewModel) via static events.
//    • Never owns state; only renders what it's told.
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using TMPro;

namespace Game.Runtime
{
    public class DialogueUI : ObserverMonoBehaviour
    {
        [Header("UI References")]
        public GameObject DialoguePanel;
        public TMP_Text   SpeakerLabel;
        public TMP_Text   BodyText;
        public TMP_Text   ContinuePrompt;

        [Header("Strings")]
        public string ContinueString = "[E] Continue";
        public string CloseString    = "[E] Close";

        protected override void Start()
        {
            base.Start();   // lets ObserverMonoBehaviour set _didCallStart before TrySubscribe
            if (DialoguePanel) DialoguePanel.SetActive(false);
        }

        // ── ObserverMonoBehaviour contract ────────────────────────────────────

        /// <summary>
        /// Called once after Start() and on every subsequent re-enable.
        /// Hooks into the static events fired by DialogueInteractable.
        /// </summary>
        protected override void Subscribe()
        {
            DialogueInteractable.OnLineShown    += HandleLine;
            DialogueInteractable.OnDialogueEnded += HandleEnd;
        }

        /// <summary>
        /// Called on disable and destroy. Mirrors Subscribe exactly so no
        /// handlers are left dangling after this object is toggled or unloaded.
        /// </summary>
        protected override void Unsubscribe()
        {
            DialogueInteractable.OnLineShown    -= HandleLine;
            DialogueInteractable.OnDialogueEnded -= HandleEnd;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleLine(DialogueLine line, int index, int total, string formatted)
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

        private void HandleEnd()
        {
            if (DialoguePanel) DialoguePanel.SetActive(false);
        }
    }
}

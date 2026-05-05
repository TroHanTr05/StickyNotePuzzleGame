// ─────────────────────────────────────────────────────────────────────────────
//  DialogueInteractable.cs
//
//  CHANGES FROM ORIGINAL:
//    • ConversationSet.IsUnlocked() no longer calls InventoryModel.Instance
//      directly. It now resolves IInventoryModel from ServiceResolver so the
//      concrete type is never hard-wired into the condition logic.
//    • Added Game.Runtime namespace consistent with the rest of the project.
//    • Debug.Log replaced with IGameLog resolved from ServiceResolver.
//
//  MVVM role: VIEW-MODEL
//    • Reads IInventoryModel (Model), fires events consumed by DialogueUI (View).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    // ── Data types ────────────────────────────────────────────────────────────

    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Who is speaking — leave blank to use the NPC GameObject name.")]
        public string SpeakerName = "";

        [Tooltip("What they say.")]
        [TextArea(2, 6)]
        public string Text = "";

        /// <summary>Returns the formatted string shown in UI: "Speaker: Text"</summary>
        public string Formatted(string fallbackSpeaker)
        {
            string speaker = string.IsNullOrWhiteSpace(SpeakerName) ? fallbackSpeaker : SpeakerName;
            return $"{speaker}: {Text}";
        }
    }

    [Serializable]
    public class ConversationSet
    {
        [Tooltip("Friendly label shown in the Inspector — not used at runtime.")]
        public string Label = "Default";

        [Tooltip("ALL of these item tags must be in the inventory for this set to activate. " +
                 "Leave empty to make this the unconditional fallback.")]
        public string[] RequiredItems = Array.Empty<string>();

        [Tooltip("ANY of these item tags must NOT be in the inventory. " +
                 "Leave empty to ignore.")]
        public string[] BlockingItems = Array.Empty<string>();

        public DialogueLine[] Lines = Array.Empty<DialogueLine>();

        [Tooltip("Loop after the last line?")]
        public bool Loop = false;

        /// <summary>
        /// Returns true when the inventory satisfies this set's conditions.
        /// Resolves IInventoryModel from the DI container — no static singleton access.
        /// </summary>
        public bool IsUnlocked()
        {
            // Resolve through the container so this stays decoupled from
            // the concrete InventoryModel type and is overridable in tests.
            var inv = ServiceResolver.Resolve<IInventoryModel>();
            foreach (var req in RequiredItems)
                if (!inv.Has(req)) return false;
            foreach (var block in BlockingItems)
                if (inv.Has(block)) return false;
            return true;
        }
    }

    // ── Component ─────────────────────────────────────────────────────────────

    public class DialogueInteractable : MonoBehaviour
    {
        private static IGameLog Log => ServiceResolver.Resolve<IGameLog>();

        // ── Static events (DialogueUI subscribes via ObserverMonoBehaviour) ───
        public static event Action<DialogueLine, int, int, string> OnLineShown;
        public static event Action OnDialogueEnded;

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Interaction")]
        [Tooltip("Auto-resolved from SnakeController at runtime. Override by dragging here.")]
        public GameObject PlayerHead;
        [Range(0.5f, 20f)] public float InteractionRange = 3f;

        [Header("Conversation Sets (first matching set plays)")]
        [Tooltip("Put the most specific (most items required) set FIRST. " +
                 "The last set should have no requirements as a fallback.")]
        public ConversationSet[] ConversationSets = Array.Empty<ConversationSet>();

        [Header("Dialogue Output (TMP) — optional, DialogueUI can handle this instead")]
        public TextMeshProUGUI DialogueText;
        public TextMeshProUGUI SpeakerText;
        public GameObject      DialoguePanel;

        // ── Private state ─────────────────────────────────────────────────────
        bool _headResolved;
        bool _inConversation;
        bool _playerInRange;
        int  _currentIndex;
        ConversationSet _activeSet;

        // ── Unity ─────────────────────────────────────────────────────────────

        void Start() => SetBoxVisible(false);

        void Update()
        {
            if (!_headResolved) { TryResolveHead(); if (!_headResolved) return; }
            CheckRange();
            if (_playerInRange) CheckInput();
        }

        // ── Head resolution ───────────────────────────────────────────────────

        void TryResolveHead()
        {
            if (PlayerHead != null) { _headResolved = true; return; }
            var snake = FindFirstObjectByType<SnakeController>();
            if (snake == null || snake.Head == null) return;
            PlayerHead = snake.Head;
            _headResolved = true;
        }

        // ── Range ─────────────────────────────────────────────────────────────

        void CheckRange()
        {
            bool inRange = Vector3.Distance(transform.position, PlayerHead.transform.position)
                           <= InteractionRange;

            if (inRange && !_playerInRange)
            {
                _playerInRange = true;
                Log.Info($"[{gameObject.name}] Press E to interact.");
            }
            else if (!inRange && _playerInRange)
            {
                _playerInRange = false;
                if (_inConversation) EndConversation();
            }
        }

        // ── Input ─────────────────────────────────────────────────────────────

        void CheckInput()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.eKey.wasPressedThisFrame) return;

            if (!_inConversation)
                BeginConversation();
            else
                AdvanceLine();
        }

        // ── Conversation flow ─────────────────────────────────────────────────

        void BeginConversation()
        {
            _activeSet = null;
            foreach (var set in ConversationSets)
            {
                if (set.IsUnlocked()) { _activeSet = set; break; }
            }

            if (_activeSet == null || _activeSet.Lines.Length == 0)
            {
                Log.Info($"[{gameObject.name}] No available dialogue.");
                return;
            }

            _currentIndex   = 0;
            _inConversation = true;
            SetBoxVisible(true);
            ShowCurrentLine();
        }

        void ShowCurrentLine()
        {
            if (_activeSet == null || _currentIndex >= _activeSet.Lines.Length)
            { EndConversation(); return; }

            var line   = _activeSet.Lines[_currentIndex];
            int total  = _activeSet.Lines.Length;
            string fmt = line.Formatted(gameObject.name);

            if (DialogueText != null)
            {
                if (SpeakerText != null)
                {
                    string sp = string.IsNullOrWhiteSpace(line.SpeakerName)
                                ? gameObject.name : line.SpeakerName;
                    SpeakerText.text  = sp;
                    DialogueText.text = line.Text;
                }
                else
                {
                    DialogueText.text = fmt;
                }
            }

            Log.Info(fmt);
            OnLineShown?.Invoke(line, _currentIndex, total, fmt);
        }

        void AdvanceLine()
        {
            _currentIndex++;
            if (_currentIndex >= _activeSet.Lines.Length)
            {
                if (_activeSet.Loop) { _currentIndex = 0; ShowCurrentLine(); }
                else EndConversation();
                return;
            }
            ShowCurrentLine();
        }

        void EndConversation()
        {
            _inConversation = false;
            _currentIndex   = 0;
            _activeSet      = null;
            SetBoxVisible(false);
            OnDialogueEnded?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        void SetBoxVisible(bool v)
        {
            if (DialoguePanel != null) { DialoguePanel.SetActive(v); return; }
            if (DialogueText  != null)   DialogueText.gameObject.SetActive(v);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.25f);
            Gizmos.DrawSphere(transform.position, InteractionRange);
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, InteractionRange);
        }
#endif
    }
}

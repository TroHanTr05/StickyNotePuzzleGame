// ─────────────────────────────────────────────────────────────────────────────
//  DialogueInteractable.cs  (updated)
//
//  Each NPC/sign can have multiple ConversationSets. The first set whose
//  inventory condition is satisfied is the one that plays.
//
//  Line format displayed: "Speaker: Text"
//
//  MVVM role: VIEW-MODEL (reads InventoryModel, fires events for DialogueUI)
// ─────────────────────────────────────────────────────────────────────────────
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// ── Data types ────────────────────────────────────────────────────────────────

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

    /// <summary>Returns true when the inventory satisfies this set's conditions.</summary>
    public bool IsUnlocked()
    {
        var inv = InventoryModel.Instance;
        foreach (var req in RequiredItems)
            if (!inv.Has(req)) return false;
        foreach (var block in BlockingItems)
            if (inv.Has(block)) return false;
        return true;
    }
}

// ── Component ─────────────────────────────────────────────────────────────────

public class DialogueInteractable : MonoBehaviour
{
    // ── Static events (DialogueUI subscribes) ─────────────────────────────────
    public static event Action<DialogueLine, int, int, string> OnLineShown;   // line, index, total, formattedText
    public static event Action OnDialogueEnded;

    // ── Inspector ─────────────────────────────────────────────────────────────
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
    public GameObject     DialoguePanel;

    // ── Private state ─────────────────────────────────────────────────────────
    bool _headResolved;
    bool _inConversation;
    bool _playerInRange;
    int  _currentIndex;
    ConversationSet _activeSet;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start() => SetBoxVisible(false);

    void Update()
    {
        if (!_headResolved) { TryResolveHead(); if (!_headResolved) return; }
        CheckRange();
        if (_playerInRange) CheckInput();
    }

    // ── Head resolution ───────────────────────────────────────────────────────

    void TryResolveHead()
    {
        if (PlayerHead != null) { _headResolved = true; return; }
        var snake = FindFirstObjectByType<SnakeController>();
        if (snake == null || snake.Head == null) return;
        PlayerHead = snake.Head;
        _headResolved = true;
    }

    // ── Range ─────────────────────────────────────────────────────────────────

    void CheckRange()
    {
        bool inRange = Vector3.Distance(transform.position, PlayerHead.transform.position)
                       <= InteractionRange;

        if (inRange && !_playerInRange)
        {
            _playerInRange = true;
            Debug.Log($"[{gameObject.name}] Press E to interact.");
        }
        else if (!inRange && _playerInRange)
        {
            _playerInRange = false;
            if (_inConversation) EndConversation();
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void CheckInput()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (!_inConversation)
            BeginConversation();
        else
            AdvanceLine();
    }

    // ── Conversation flow ─────────────────────────────────────────────────────

    void BeginConversation()
    {
        // Pick the first set whose conditions are met (re-evaluated every open)
        _activeSet = null;
        foreach (var set in ConversationSets)
        {
            if (set.IsUnlocked()) { _activeSet = set; break; }
        }

        if (_activeSet == null || _activeSet.Lines.Length == 0)
        {
            Debug.Log($"[{gameObject.name}] No available dialogue.");
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

        var line      = _activeSet.Lines[_currentIndex];
        int total     = _activeSet.Lines.Length;
        string fmt    = line.Formatted(gameObject.name);

        // Write to TMP if directly wired
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

        Debug.Log(fmt);
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

    // ── Helpers ───────────────────────────────────────────────────────────────

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

using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class DialogueLine
{
    [Tooltip("Who is speaking — leave blank for signs / narration.")]
    public string SpeakerName = "";

    [Tooltip("What they say. Use \\n for line breaks.")]
    [TextArea(2, 6)]
    public string Text = "";
}

// ─────────────────────────────────────────────────────────────────────────────
//  DialogueInteractable
//
//  Attach to any sign or NPC GameObject.
//
//  PlayerHead is resolved automatically at runtime from SnakeController.Head
//  (which is only valid after SnakeController.Start() builds the snake).
//  The lookup is deferred to the first Update() so there is no Start() ordering
//  race condition — no manual assignment needed.
//
//  You can still drag a GameObject into PlayerHead in the Inspector to override.
//
//  Static events for UI:
//    DialogueInteractable.OnLineShown   (DialogueLine, index, total)
//    DialogueInteractable.OnDialogueEnded ()
// ─────────────────────────────────────────────────────────────────────────────
public class DialogueInteractable : MonoBehaviour
{
    public static event System.Action<DialogueLine, int, int> OnLineShown;
    public static event System.Action OnDialogueEnded;

    [Header("Interaction")]
    [Tooltip("Leave empty — auto-resolved from SnakeController at runtime. " +
             "Drag a GameObject here only if you want to override.")]
    public GameObject PlayerHead;

    [Range(0.5f, 20f)]
    public float InteractionRange = 3f;

    [Header("Dialogue")]
    public DialogueLine[] Lines = new DialogueLine[0];

    [Tooltip("Loop back to the first line after the last one.")]
    public bool Loop = false;

    // ── Private ───────────────────────────────────────────────────────────────
    private bool _headResolved = false;   // true once PlayerHead is confirmed valid
    private int _currentIndex = 0;
    private bool _inConversation = false;
    private bool _playerInRange = false;

    // ─────────────────────────────────────────────────────────────────────────
    //  UPDATE — resolve head first, then normal tick
    // ─────────────────────────────────────────────────────────────────────────

    void Update()
    {
        // Deferred head resolution: keeps retrying every frame until
        // SnakeController.Start() has finished building the snake.
        if (!_headResolved)
        {
            TryResolveHead();
            if (!_headResolved) return;   // still not ready, wait another frame
        }

        CheckRange();
        if (_playerInRange) CheckInput();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HEAD RESOLUTION
    // ─────────────────────────────────────────────────────────────────────────

    void TryResolveHead()
    {
        // If something was manually dragged in just use it directly
        if (PlayerHead != null) { _headResolved = true; return; }

        // Find the SnakeController and grab the head it built at runtime
        SnakeController snake = FindFirstObjectByType<SnakeController>();
        if (snake == null) return;          // controller not in scene yet

        GameObject head = snake.Head;
        if (head == null) return;           // controller found but snake not built yet

        PlayerHead = head;
        _headResolved = true;
        Debug.Log($"[DialogueInteractable] Auto-resolved player head: '{head.name}'.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RANGE CHECK
    // ─────────────────────────────────────────────────────────────────────────

    void CheckRange()
    {
        float dist = Vector3.Distance(transform.position, PlayerHead.transform.position);
        bool inRange = dist <= InteractionRange;

        if (inRange && !_playerInRange)
        {
            _playerInRange = true;
            if (Lines != null && Lines.Length > 0)
                Debug.Log($"[{gameObject.name}] Press E to interact.");
        }
        else if (!inRange && _playerInRange)
        {
            _playerInRange = false;
            if (_inConversation) EndConversation();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  INPUT — new Input System
    // ─────────────────────────────────────────────────────────────────────────

    void CheckInput()
    {
        if (Lines == null || Lines.Length == 0) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (!_inConversation)
        {
            _currentIndex = 0;
            _inConversation = true;
            ShowCurrentLine();
        }
        else
        {
            AdvanceLine();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DIALOGUE FLOW
    // ─────────────────────────────────────────────────────────────────────────

    void ShowCurrentLine()
    {
        if (_currentIndex >= Lines.Length) { EndConversation(); return; }

        DialogueLine line = Lines[_currentIndex];
        string speaker = string.IsNullOrEmpty(line.SpeakerName)
                             ? gameObject.name : line.SpeakerName;

        Debug.Log($"[{speaker}] {line.Text}");
        OnLineShown?.Invoke(line, _currentIndex, Lines.Length);
    }

    void AdvanceLine()
    {
        _currentIndex++;
        if (_currentIndex >= Lines.Length)
        {
            if (Loop) { _currentIndex = 0; ShowCurrentLine(); }
            else EndConversation();
            return;
        }
        ShowCurrentLine();
    }

    void EndConversation()
    {
        _inConversation = false;
        _currentIndex = 0;
        Debug.Log($"[{gameObject.name}] (Conversation ended.)");
        OnDialogueEnded?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────────────────────────────────

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
using UnityEngine;
using UnityEngine.UI;

// The View in our MVVM setup.
// 
// Owns a PowerUpViewModel, subscribes to its OnStateChanged event,
// and pushes the current state into three UI Toggle widgets.
//
// SETUP IN THE EDITOR
// 1.  Create a UI Panel (e.g. "PowerUpPanel") with three Toggles as children.
// 2.  Drag each Toggle into the corresponding slot below.
// 3.  Optionally assign label Text components so the script can recolor them.
// 4.  Attach this script to any GameObject (an empty "UIManager" works well).
//
// The toggles are set to non-interactable so the player can't manually
// flip them — they're display-only indicators driven entirely by gameplay.
public class PowerUpUIManager : MonoBehaviour
{
    [Header("Toggle References")]
    [Tooltip("Toggle that represents the Ball Throw ability.")]
    public Toggle ballsToggle;

    [Tooltip("Toggle that represents the Grapple ability.")]
    public Toggle grappleToggle;

    [Tooltip("Toggle that represents the Glide ability.")]
    public Toggle glideToggle;

    [Header("Label References (optional)")]
    public Text ballsLabel;
    public Text grappleLabel;
    public Text glideLabel;

    [Header("Colors")]
    public Color activeColor   = new Color(0.2f, 0.85f, 0.3f, 1f);   // green
    public Color inactiveColor = new Color(0.55f, 0.55f, 0.55f, 1f);  // grey

    private PowerUpViewModel _vm;

    private void Awake()
    {
        _vm = new PowerUpViewModel();
        _vm.OnStateChanged += RefreshUI;
    }

    private void Start()
    {
        // Make sure the toggles can't be clicked by the player
        if (ballsToggle   != null) ballsToggle.interactable   = false;
        if (grappleToggle  != null) grappleToggle.interactable  = false;
        if (glideToggle    != null) glideToggle.interactable    = false;

        // Sync visuals to initial (all-off) state
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (_vm != null)
        {
            _vm.OnStateChanged -= RefreshUI;
            _vm.Dispose();
        }
    }

    // One-way data binding: reads every flag from the ViewModel
    // and pushes it into the UI. Called whenever the VM raises OnStateChanged.
    private void RefreshUI()
    {
        ApplyToggle(ballsToggle,   ballsLabel,   _vm.HasBalls);
        ApplyToggle(grappleToggle, grappleLabel,  _vm.HasGrapple);
        ApplyToggle(glideToggle,   glideLabel,    _vm.HasGlide);
    }

    private void ApplyToggle(Toggle toggle, Text label, bool isActive)
    {
        if (toggle == null) return;

        toggle.isOn = isActive;

        // Tint the checkmark / background so it's obvious at a glance
        ColorBlock cb = toggle.colors;
        cb.disabledColor = isActive ? activeColor : inactiveColor;
        toggle.colors = cb;

        if (label != null)
            label.color = isActive ? activeColor : inactiveColor;
    }

    // Resets all three toggles to off. Useful on level reload.
    public void ResetAll()
    {
        _vm.ResetAll();
    }
}

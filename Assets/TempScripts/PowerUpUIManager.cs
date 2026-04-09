using UnityEngine;
using UnityEngine.UI;

public class PowerUpUIManager : MonoBehaviour
{
    [Header("Toggle References")]
    public Toggle ballsToggle;
    public Toggle grappleToggle;
    public Toggle glideToggle;

    [Header("Label References (optional)")]
    public Text ballsLabel;
    public Text grappleLabel;
    public Text glideLabel;

    [Header("Colors")]
    public Color activeColor   = new Color(0.2f, 0.85f, 0.3f, 1f);
    public Color inactiveColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private PowerUpViewModel _vm;

    private void Awake()
    {
        _vm = new PowerUpViewModel();
        _vm.OnStateChanged += RefreshUI;
    }

    private void Start()
    {
        if (ballsToggle   != null) ballsToggle.interactable   = false;
        if (grappleToggle  != null) grappleToggle.interactable  = false;
        if (glideToggle    != null) glideToggle.interactable    = false;

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

        ColorBlock cb = toggle.colors;
        cb.disabledColor = isActive ? activeColor : inactiveColor;
        toggle.colors = cb;

        if (label != null)
            label.color = isActive ? activeColor : inactiveColor;
    }

    public void ResetAll()
    {
        _vm.ResetAll();
    }
}

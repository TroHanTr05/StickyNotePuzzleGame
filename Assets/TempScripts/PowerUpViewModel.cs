using System;

/// <summary>
/// The ViewModel in our MVVM setup.
/// Subscribes to PowerUpEvents (the Model layer), maintains canonical
/// state for each power-up, and notifies the View whenever something changes.
///
/// This is a plain C# class — no MonoBehaviour — so it's easy to test
/// and has zero Unity lifecycle coupling.
/// </summary>
public class PowerUpViewModel
{
    // ── Bindable state ──────────────────────────────────────────────
    public bool HasBalls  { get; private set; }
    public bool HasGrapple { get; private set; }
    public bool HasGlide  { get; private set; }

    /// <summary>
    /// The View subscribes to this single event.
    /// Fired every time any power-up flag changes.
    /// </summary>
    public event Action OnStateChanged;

    // ── Lifecycle ───────────────────────────────────────────────────
    public PowerUpViewModel()
    {
        PowerUpEvents.OnPowerUpCollected += HandleCollected;
        PowerUpEvents.OnPowerUpLost     += HandleLost;
    }

    /// <summary>Call this when the ViewModel is no longer needed to avoid leaks.</summary>
    public void Dispose()
    {
        PowerUpEvents.OnPowerUpCollected -= HandleCollected;
        PowerUpEvents.OnPowerUpLost     -= HandleLost;
    }

    // ── Event handlers ──────────────────────────────────────────────
    private void HandleCollected(PowerUpType type)
    {
        SetFlag(type, true);
    }

    private void HandleLost(PowerUpType type)
    {
        SetFlag(type, false);
    }

    private void SetFlag(PowerUpType type, bool value)
    {
        switch (type)
        {
            case PowerUpType.Balls:   HasBalls   = value; break;
            case PowerUpType.Grapple: HasGrapple = value; break;
            case PowerUpType.Glide:   HasGlide   = value; break;
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>Resets everything — handy for level restarts.</summary>
    public void ResetAll()
    {
        HasBalls   = false;
        HasGrapple = false;
        HasGlide   = false;
        OnStateChanged?.Invoke();
    }
}

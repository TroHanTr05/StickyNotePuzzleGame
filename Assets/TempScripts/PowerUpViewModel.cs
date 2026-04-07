using System;

public class PowerUpViewModel
{
    public bool HasBalls  { get; private set; }
    public bool HasGrapple { get; private set; }
    public bool HasGlide  { get; private set; }

    public event Action OnStateChanged;

    public PowerUpViewModel()
    {
        PowerUpEvents.OnPowerUpCollected += HandleCollected;
        PowerUpEvents.OnPowerUpLost     += HandleLost;
    }

    public void Dispose()
    {
        PowerUpEvents.OnPowerUpCollected -= HandleCollected;
        PowerUpEvents.OnPowerUpLost     -= HandleLost;
    }

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

    public void ResetAll()
    {
        HasBalls   = false;
        HasGrapple = false;
        HasGlide   = false;
        OnStateChanged?.Invoke();
    }
}

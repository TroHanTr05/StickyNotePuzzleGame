using System;

public static class PowerUpEvents
{
    public static event Action<PowerUpType> OnPowerUpCollected;
    public static event Action<PowerUpType> OnPowerUpLost;

    public static void RaisePowerUpCollected(PowerUpType type)
    {
        OnPowerUpCollected?.Invoke(type);
    }

    public static void RaisePowerUpLost(PowerUpType type)
    {
        OnPowerUpLost?.Invoke(type);
    }
}

public enum PowerUpType
{
    Balls,
    Grapple,
    Glide
}

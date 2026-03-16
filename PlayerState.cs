namespace DeterministicCombatSim;

public enum CombatState
{
    Idle,
    Windup,
    Active,
    Recovery,
    Dodge,
    Hitstun
}

public struct PlayerState : IEquatable<PlayerState>
{
    public float Position;
    public float Velocity;
    public float Stamina;
    public CombatState State;
    public int StateTicksRemaining;
    public bool HitConnected;

    public int DodgeTicksElapsed;

    public readonly bool IsInDodgeInvulnerability =>
        State == CombatState.Dodge
        && DodgeTicksElapsed >= SimulationConstants.DodgeStartupTicks
        && DodgeTicksElapsed < SimulationConstants.DodgeStartupTicks + SimulationConstants.DodgeInvulnerabilityTicks;

    public static PlayerState CreateDefault(float startPosition) => new()
    {
        Position = startPosition,
        Velocity = 0f,
        Stamina = SimulationConstants.MaxStamina,
        State = CombatState.Idle,
        StateTicksRemaining = 0,
        HitConnected = false,
        DodgeTicksElapsed = 0
    };

    public readonly bool Equals(PlayerState other) =>
        Position == other.Position
        && Velocity == other.Velocity
        && Stamina == other.Stamina
        && State == other.State
        && StateTicksRemaining == other.StateTicksRemaining
        && HitConnected == other.HitConnected
        && DodgeTicksElapsed == other.DodgeTicksElapsed;

    public override readonly bool Equals(object? obj) => obj is PlayerState other && Equals(other);

    public override readonly int GetHashCode() =>
        HashCode.Combine(Position, Velocity, Stamina, State, StateTicksRemaining, HitConnected, DodgeTicksElapsed);

    public static bool operator ==(PlayerState left, PlayerState right) => left.Equals(right);
    public static bool operator !=(PlayerState left, PlayerState right) => !left.Equals(right);

    public override readonly string ToString()
    {
        var stateStr = State switch
        {
            CombatState.Dodge when IsInDodgeInvulnerability => "Dodge(I)",
            CombatState.Dodge => "Dodge",
            _ => State.ToString()
        };
        return $"Pos={Position:F2} Sta={Stamina:F0} [{stateStr} t={StateTicksRemaining}]";
    }
}

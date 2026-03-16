namespace DeterministicCombatSim;

public enum InputAction
{
    None,
    LightAttack,
    Dodge,
    MoveLeft,
    MoveRight
}

public readonly record struct PlayerInput(int PlayerId, int Tick, InputAction Action)
{
    public override string ToString() => $"P{PlayerId}@T{Tick}:{Action}";
}

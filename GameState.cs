namespace DeterministicCombatSim;

public struct GameState : IEquatable<GameState>
{
    public PlayerState Player0;
    public PlayerState Player1;
    public int Tick;

    public PlayerState GetPlayer(int index) => index == 0 ? Player0 : Player1;

    public void SetPlayer(int index, PlayerState state)
    {
        if (index == 0) Player0 = state;
        else Player1 = state;
    }

    public static GameState CreateInitial() => new()
    {
        Player0 = PlayerState.CreateDefault(SimulationConstants.Player0StartPosition),
        Player1 = PlayerState.CreateDefault(SimulationConstants.Player1StartPosition),
        Tick = 0
    };

    public readonly GameState Clone() => new()
    {
        Player0 = Player0,
        Player1 = Player1,
        Tick = Tick
    };

    public readonly bool Equals(GameState other) =>
        Tick == other.Tick && Player0 == other.Player0 && Player1 == other.Player1;

    public override readonly bool Equals(object? obj) => obj is GameState other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Tick, Player0, Player1);
    public static bool operator ==(GameState left, GameState right) => left.Equals(right);
    public static bool operator !=(GameState left, GameState right) => !left.Equals(right);
}

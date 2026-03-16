namespace DeterministicCombatSim;

public sealed class Server
{
    private GameState _state;
    private readonly List<PlayerInput> _pendingInputs = [];
    private readonly Dictionary<int, GameState> _stateHistory = [];

    public int CurrentTick => _state.Tick;
    public GameState CurrentState => _state;

    public Server(GameState initialState)
    {
        _state = initialState;
        _stateHistory[_state.Tick] = _state;
    }

    public void ReceiveInput(PlayerInput input) => _pendingInputs.Add(input);

    public PlayerInput[] Tick()
    {
        int processingTick = _state.Tick;

        var tickInputs = new List<PlayerInput>();
        var remaining = new List<PlayerInput>();

        foreach (var input in _pendingInputs)
        {
            if (input.Tick <= processingTick)
            {
                var stamped = input.Tick < processingTick
                    ? new PlayerInput(input.PlayerId, processingTick, input.Action)
                    : input;
                tickInputs.Add(stamped);
            }
            else
            {
                remaining.Add(input);
            }
        }

        _pendingInputs.Clear();
        _pendingInputs.AddRange(remaining);

        var inputArray = tickInputs.ToArray();
        _state = Simulation.Step(_state, inputArray);
        _stateHistory[_state.Tick] = _state;

        return inputArray;
    }

    public GameState? GetStateAtTick(int tick) =>
        _stateHistory.TryGetValue(tick, out var s) ? s : null;
}

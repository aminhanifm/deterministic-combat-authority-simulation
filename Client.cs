namespace DeterministicCombatSim;

public sealed class Client
{
    private readonly int _playerId;
    private GameState _predictedState;
    private readonly Dictionary<int, GameState> _predictionHistory = [];
    private readonly List<PlayerInput> _inputHistory = [];

    public int PlayerId => _playerId;
    public GameState PredictedState => _predictedState;
    public IReadOnlyList<PlayerInput> InputHistory => _inputHistory;

    public Client(int playerId, GameState initialState)
    {
        _playerId = playerId;
        _predictedState = initialState;
        _predictionHistory[initialState.Tick] = initialState;
    }

    public void PredictInput(PlayerInput input)
    {
        _inputHistory.Add(input);
        _predictedState = Simulation.Step(_predictedState, [input]);
        _predictionHistory[_predictedState.Tick] = _predictedState;
    }

    public void PredictTick()
    {
        _predictedState = Simulation.Step(_predictedState, []);
        _predictionHistory[_predictedState.Tick] = _predictedState;
    }

    public string? Reconcile(GameState authoritative)
    {
        int authTick = authoritative.Tick;

        if (!_predictionHistory.TryGetValue(authTick, out var predicted))
        {
            _predictedState = authoritative;
            _predictionHistory[authTick] = authoritative;
            return $"[Tick {authTick}] P{_playerId}: No prediction history - SNAP to server state.";
        }

        var predPlayer = predicted.GetPlayer(_playerId);
        var authPlayer = authoritative.GetPlayer(_playerId);

        if (predPlayer == authPlayer)
            return null;

        if (predPlayer.State != authPlayer.State)
        {
            string msg = $"[Tick {authTick}] P{_playerId}: SNAP - predicted {predPlayer.State} but server says {authPlayer.State}. " +
                         $"Predicted: {predPlayer} | Server: {authPlayer}";
            SnapToServerState(authoritative, authTick);
            return msg;
        }

        if (predPlayer.Stamina != authPlayer.Stamina ||
            predPlayer.HitConnected != authPlayer.HitConnected ||
            predPlayer.StateTicksRemaining != authPlayer.StateTicksRemaining)
        {
            string msg = $"[Tick {authTick}] P{_playerId}: ROLLBACK - gameplay state diverged. " +
                         $"Predicted: {predPlayer} | Server: {authPlayer}";
            RollbackAndReplay(authoritative, authTick);
            return msg;
        }

        float posDiff = MathF.Abs(predPlayer.Position - authPlayer.Position);
        if (posDiff > 0f && posDiff <= SimulationConstants.BlendPositionThreshold)
        {
            string msg = $"[Tick {authTick}] P{_playerId}: BLEND - position off by {posDiff:F3}. " +
                         $"Predicted pos={predPlayer.Position:F2}, server pos={authPlayer.Position:F2}";
            BlendPosition(authPlayer.Position);
            return msg;
        }

        {
            string msg = $"[Tick {authTick}] P{_playerId}: SNAP - large divergence. " +
                         $"Predicted: {predPlayer} | Server: {authPlayer}";
            SnapToServerState(authoritative, authTick);
            return msg;
        }
    }

    private void SnapToServerState(GameState authoritative, int fromTick)
    {
        _predictedState = authoritative;
        _predictionHistory[fromTick] = authoritative;

        ReplayInputsFrom(fromTick);
    }

    private void RollbackAndReplay(GameState authoritative, int fromTick)
    {
        _predictedState = authoritative;
        _predictionHistory[fromTick] = authoritative;
        ReplayInputsFrom(fromTick);
    }

    private void ReplayInputsFrom(int fromTick)
    {
        var futureInputs = _inputHistory
            .Where(i => i.Tick > fromTick && i.PlayerId == _playerId)
            .OrderBy(i => i.Tick)
            .ToList();

        int inputIdx = 0;

        int targetTick = _predictionHistory.Keys.Max();

        while (_predictedState.Tick < targetTick)
        {
            var tickInputs = new List<PlayerInput>();
            while (inputIdx < futureInputs.Count && futureInputs[inputIdx].Tick == _predictedState.Tick)
            {
                tickInputs.Add(futureInputs[inputIdx]);
                inputIdx++;
            }

            _predictedState = Simulation.Step(_predictedState, tickInputs.ToArray());
            _predictionHistory[_predictedState.Tick] = _predictedState;
        }
    }

    private void BlendPosition(float authoritativePosition)
    {
        var ps = _predictedState.GetPlayer(_playerId);
        ps.Position += (authoritativePosition - ps.Position) * SimulationConstants.BlendRate;
        _predictedState.SetPlayer(_playerId, ps);
        _predictionHistory[_predictedState.Tick] = _predictedState;
    }
}

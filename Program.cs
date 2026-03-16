namespace DeterministicCombatSim;

public static class Program
{
    private const int PlayerBLatencyTicks = 8;
    private const int TotalSimulationTicks = 120;

    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        PrintHeader();
        var (serverFinalState, allServerInputs) = RunAuthoritativeScenario();
        RunReplay(serverFinalState, allServerInputs);
    }

    private static (GameState finalState, List<PlayerInput> allInputs) RunAuthoritativeScenario()
    {
        var initialState = GameState.CreateInitial();
        var server = new Server(initialState);
        var clientA = new Client(0, initialState);
        var clientB = new Client(1, initialState);

        var scriptedInputs = new Dictionary<int, List<PlayerInput>>
        {
            [10] = [new PlayerInput(0, 10, InputAction.LightAttack)],
            [15] = [new PlayerInput(1, 15, InputAction.Dodge)]
        };

        var delayedQueue = new List<(int deliveryTick, PlayerInput input)>();

        var allServerInputs = new List<PlayerInput>();

        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine("  COMBAT SIMULATION - SERVER AUTHORITATIVE");
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Tick rate: {SimulationConstants.TickRate} Hz | " +
                          $"Player B latency: {PlayerBLatencyTicks} ticks ({PlayerBLatencyTicks * SimulationConstants.TickDuration * 1000:F0}ms)");
        Console.WriteLine($"  Attack range: {SimulationConstants.AttackRange:F1} | " +
                          $"P0 start: {SimulationConstants.Player0StartPosition:F1} | " +
                          $"P1 start: {SimulationConstants.Player1StartPosition:F1} | " +
                          $"Distance: {MathF.Abs(SimulationConstants.Player1StartPosition - SimulationConstants.Player0StartPosition):F1}");
        Console.WriteLine($"  P0 = Player A | P1 = Player B");
        Console.WriteLine();
        PrintTableHeader();

        for (int t = 0; t < TotalSimulationTicks; t++)
        {
            int currentTick = server.CurrentTick;
            var prevState = server.CurrentState;
            var events = new List<string>();

            if (scriptedInputs.TryGetValue(currentTick, out var inputs))
            {
                foreach (var input in inputs)
                {
                    string playerLabel = input.PlayerId == 0 ? "P0" : "P1";
                    events.Add($"{playerLabel} {input.Action}");

                    if (input.PlayerId == 0) clientA.PredictInput(input);
                    else clientB.PredictInput(input);

                    int latency = input.PlayerId == 1 ? PlayerBLatencyTicks : 0;
                    if (latency == 0)
                    {
                        server.ReceiveInput(input);
                    }
                    else
                    {
                        delayedQueue.Add((currentTick + latency, input));
                    }
                }
            }

            var arrived = delayedQueue.Where(d => d.deliveryTick <= currentTick).ToList();
            foreach (var (_, input) in arrived)
            {
                string playerLabel = input.PlayerId == 0 ? "P0" : "P1";
                events.Add($"{playerLabel} input arrived (late)");
                server.ReceiveInput(input);
            }
            delayedQueue.RemoveAll(d => d.deliveryTick <= currentTick);

            if (!scriptedInputs.ContainsKey(currentTick) || !scriptedInputs[currentTick].Any(i => i.PlayerId == 0))
                clientA.PredictTick();
            if (!scriptedInputs.ContainsKey(currentTick) || !scriptedInputs[currentTick].Any(i => i.PlayerId == 1))
                clientB.PredictTick();

            var processedInputs = server.Tick();
            allServerInputs.AddRange(processedInputs);

            var serverState = server.CurrentState;

            for (int p = 0; p < 2; p++)
            {
                var prev = prevState.GetPlayer(p);
                var curr = serverState.GetPlayer(p);
                string pl = p == 0 ? "P0" : "P1";
                if (prev.State != curr.State)
                {
                    if (curr.State == CombatState.Hitstun)
                        events.Add($"{pl} HIT -> Hitstun");
                    else if (curr.State == CombatState.Active)
                        events.Add($"{pl} -> Active (can hit)");
                    else if (curr.State == CombatState.Idle && prev.State != CombatState.Idle)
                        events.Add($"{pl} -> Idle");
                    else if (curr.State == CombatState.Recovery)
                        events.Add($"{pl} -> Recovery");
                }
            }

            string? corrA = clientA.Reconcile(serverState);
            string? corrB = clientB.Reconcile(serverState);

            if (corrA != null) events.Add($"Client P0 correction");
            if (corrB != null) events.Add($"Client P1 correction");

            string eventStr = events.Count > 0 ? string.Join(", ", events) : "";

            bool hasCorrection = corrA != null || corrB != null;
            bool isKeyInputTick = currentTick == 10 || currentTick == 15;

            bool inCombatWindow = currentTick >= 8 && currentTick <= 55;
            if (inCombatWindow || hasCorrection || isKeyInputTick || currentTick == 0 || t == TotalSimulationTicks - 1)
            {
                PrintTickRow(currentTick + 1, serverState, clientA.PredictedState, clientB.PredictedState, eventStr);
            }

            if (corrA != null) Console.WriteLine($"  >> {corrA}");
            if (corrB != null) Console.WriteLine($"  >> {corrB}");
        }

        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        return (server.CurrentState, allServerInputs);
    }

    private static void RunReplay(GameState serverFinalState, List<PlayerInput> allServerInputs)
    {
        var initialState = GameState.CreateInitial();
        ReplayRunner.Replay(initialState, allServerInputs, serverFinalState, TotalSimulationTicks);
    }

    private static void PrintHeader()
    {
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine("  DETERMINISTIC COMBAT AUTHORITY SIMULATION                   ");
        Console.WriteLine("  Server-authoritative | Client prediction | Reconciliation   ");
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    private static void PrintTableHeader()
    {
        Console.WriteLine($"  {"Tick",4} | {"Server P0",-30} | {"Server P1",-30} | {"Client P0 (pred)",-30} | {"Client P1 (pred)",-30} | Event");
        Console.WriteLine($"  {"----",4}-+-{new string('-', 30)}-+-{new string('-', 30)}-+-{new string('-', 30)}-+-{new string('-', 30)}-+-{new string('-', 30)}");
    }

    private static void PrintTickRow(int tick, GameState server, GameState clientA, GameState clientB, string eventStr)
    {
        Console.WriteLine($"  {tick,4} | {server.Player0,-30} | {server.Player1,-30} | {clientA.Player0,-30} | {clientB.Player1,-30} | {eventStr}");
    }
}

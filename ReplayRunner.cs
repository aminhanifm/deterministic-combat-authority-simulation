namespace DeterministicCombatSim;

public static class ReplayRunner
{
    public static bool Replay(GameState initialState, IReadOnlyList<PlayerInput> inputs, GameState expectedFinalState, int totalTicks)
    {
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine("  DETERMINISTIC REPLAY VALIDATION");
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Replaying {inputs.Count} inputs over {totalTicks} ticks...");
        Console.WriteLine();

        var state = initialState;

        for (int t = 0; t < totalTicks; t++)
        {
            var tickInputs = inputs.Where(i => i.Tick == state.Tick).ToArray();
            state = Simulation.Step(state, tickInputs);
        }

        Console.WriteLine($"  Original  final state (Tick {expectedFinalState.Tick}):");
        Console.WriteLine($"    P0: {expectedFinalState.Player0}");
        Console.WriteLine($"    P1: {expectedFinalState.Player1}");
        Console.WriteLine();
        Console.WriteLine($"  Replayed  final state (Tick {state.Tick}):");
        Console.WriteLine($"    P0: {state.Player0}");
        Console.WriteLine($"    P1: {state.Player1}");
        Console.WriteLine();

        bool match = state == expectedFinalState;

        if (match)
        {
            Console.WriteLine("  REPLAY PASS - States are identical. Simulation is deterministic.");
        }
        else
        {
            Console.WriteLine("  REPLAY FAIL - States diverged!");
            if (state.Player0 != expectedFinalState.Player0)
            {
                Console.WriteLine($"    P0 diff: replay={state.Player0} vs original={expectedFinalState.Player0}");
            }
            if (state.Player1 != expectedFinalState.Player1)
            {
                Console.WriteLine($"    P1 diff: replay={state.Player1} vs original={expectedFinalState.Player1}");
            }
        }

        Console.WriteLine("══════════════════════════════════════════════════════════════");
        return match;
    }
}

namespace DeterministicCombatSim;

public static class Simulation
{
    public static GameState Step(GameState current, ReadOnlySpan<PlayerInput> inputs)
    {
        var next = current.Clone();
        next.Tick = current.Tick + 1;

        foreach (var input in inputs)
        {
            if (input.Tick != current.Tick) continue;
            var ps = next.GetPlayer(input.PlayerId);
            ps = ApplyInput(ps, input.Action);
            next.SetPlayer(input.PlayerId, ps);
        }

        next.Player0 = AdvanceStateMachine(next.Player0);
        next.Player1 = AdvanceStateMachine(next.Player1);

        ResolveHits(ref next);

        next.Player0 = ApplyMovement(next.Player0);
        next.Player1 = ApplyMovement(next.Player1);

        return next;
    }

    private static PlayerState ApplyInput(PlayerState ps, InputAction action)
    {
        switch (action)
        {
            case InputAction.LightAttack when ps.State == CombatState.Idle && ps.Stamina >= SimulationConstants.LightAttackStaminaCost:
                ps.State = CombatState.Windup;
                ps.StateTicksRemaining = SimulationConstants.LightAttackWindupTicks;
                ps.Stamina -= SimulationConstants.LightAttackStaminaCost;
                ps.HitConnected = false;
                break;

            case InputAction.Dodge when ps.State == CombatState.Idle && ps.Stamina >= SimulationConstants.DodgeStaminaCost:
                ps.State = CombatState.Dodge;
                ps.StateTicksRemaining = SimulationConstants.DodgeTotalTicks;
                ps.DodgeTicksElapsed = 0;
                ps.Stamina -= SimulationConstants.DodgeStaminaCost;
                break;

            case InputAction.MoveLeft when ps.State == CombatState.Idle:
                ps.Velocity = -SimulationConstants.MoveSpeed;
                break;

            case InputAction.MoveRight when ps.State == CombatState.Idle:
                ps.Velocity = SimulationConstants.MoveSpeed;
                break;

            case InputAction.None:
                if (ps.State == CombatState.Idle) ps.Velocity = 0f;
                break;
        }

        return ps;
    }

    private static PlayerState AdvanceStateMachine(PlayerState ps)
    {
        if (ps.State == CombatState.Idle) return ps;

        if (ps.State == CombatState.Dodge)
            ps.DodgeTicksElapsed++;

        ps.StateTicksRemaining--;

        if (ps.StateTicksRemaining <= 0)
        {
            ps = TransitionOnExpiry(ps);
        }

        return ps;
    }

    private static PlayerState TransitionOnExpiry(PlayerState ps)
    {
        switch (ps.State)
        {
            case CombatState.Windup:
                ps.State = CombatState.Active;
                ps.StateTicksRemaining = SimulationConstants.LightAttackActiveTicks;
                ps.HitConnected = false;
                break;

            case CombatState.Active:
                ps.State = CombatState.Recovery;
                ps.StateTicksRemaining = SimulationConstants.LightAttackRecoveryTicks;
                break;

            case CombatState.Recovery:
            case CombatState.Dodge:
            case CombatState.Hitstun:
                ps.State = CombatState.Idle;
                ps.StateTicksRemaining = 0;
                ps.DodgeTicksElapsed = 0;
                ps.Velocity = 0f;
                break;
        }
        return ps;
    }

    private static void ResolveHits(ref GameState gs)
    {
        TryHit(ref gs, attackerIndex: 0, defenderIndex: 1);
        TryHit(ref gs, attackerIndex: 1, defenderIndex: 0);
    }

    private static void TryHit(ref GameState gs, int attackerIndex, int defenderIndex)
    {
        var attacker = gs.GetPlayer(attackerIndex);
        var defender = gs.GetPlayer(defenderIndex);

        if (attacker.State != CombatState.Active) return;
        if (attacker.HitConnected) return;
        if (defender.IsInDodgeInvulnerability) return;

        float distance = MathF.Abs(attacker.Position - defender.Position);
        if (distance > SimulationConstants.AttackRange) return;

        attacker.HitConnected = true;
        gs.SetPlayer(attackerIndex, attacker);

        defender.State = CombatState.Hitstun;
        defender.StateTicksRemaining = SimulationConstants.HitstunTicks;
        defender.Velocity = 0f;
        defender.DodgeTicksElapsed = 0;
        gs.SetPlayer(defenderIndex, defender);
    }

    private static PlayerState ApplyMovement(PlayerState ps)
    {
        if (ps.State == CombatState.Idle)
        {
            ps.Position += ps.Velocity * (float)SimulationConstants.TickDuration;
        }
        else
        {
            ps.Velocity = 0f;
        }
        return ps;
    }
}

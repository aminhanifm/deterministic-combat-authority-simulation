namespace DeterministicCombatSim;

public static class SimulationConstants
{
    public const int TickRate = 60;
    public const double TickDuration = 1.0 / TickRate;

    public const int LightAttackWindupTicks = 12;
    public const int LightAttackActiveTicks = 6;
    public const int LightAttackRecoveryTicks = 18;

    public const int DodgeStartupTicks = 3;
    public const int DodgeInvulnerabilityTicks = 6;
    public const int DodgeRecoveryTicks = 12;
    public const int DodgeTotalTicks = DodgeStartupTicks + DodgeInvulnerabilityTicks + DodgeRecoveryTicks;

    public const int HitstunTicks = 18;

    public const float MaxStamina = 100f;
    public const float LightAttackStaminaCost = 20f;
    public const float DodgeStaminaCost = 15f;

    public const float AttackRange = 2.0f;
    public const float MoveSpeed = 5.0f;

    public const float Player0StartPosition = -1.0f;
    public const float Player1StartPosition = 1.0f;

    public const float BlendPositionThreshold = 0.5f;
    public const float BlendRate = 0.2f;
}

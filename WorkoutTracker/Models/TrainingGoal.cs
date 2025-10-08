namespace WorkoutTracker.Models
{
    public enum TrainingGoal
    {
        Strength = 0,
        Hypertrophy = 1,
        Endurance = 2
    }

    public sealed class GoalConfig
    {
        public int RepsLow { get; init; }
        public int RepsHigh { get; init; }
        public double TargetRir { get; init; }
        public double MaxStepUpPct { get; init; }
        public double MaxStepDnPct { get; init; }
        public double FatigueDropPct { get; init; }
        public double MinDeltaKgCompound { get; init; }
        public double MinDeltaKgIsolation { get; init; }
    }
}

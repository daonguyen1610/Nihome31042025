/**
 * Task weights inside a design phase must add up to 100% before the phase
 * (and the project) shows a progress figure. Kept free of imports so it can
 * be unit-tested directly.
 */
export const PHASE_TASK_WEIGHT_TOTAL = 100;

type PhaseRollup = { phaseId: number; taskSources: { weight: number }[] };

/** Sum of task weights already used in each phase. */
export function taskWeightTotals(rollups: PhaseRollup[]): Map<number, number> {
  return new Map(
    rollups.map((rollup) => [
      rollup.phaseId,
      rollup.taskSources.reduce((total, source) => total + source.weight, 0),
    ]),
  );
}

/** Weight for a new task that completes its phase, at least 1%. */
export function suggestedTaskWeight(usedWeight: number): number {
  return Math.min(PHASE_TASK_WEIGHT_TOTAL, Math.max(1, PHASE_TASK_WEIGHT_TOTAL - usedWeight));
}

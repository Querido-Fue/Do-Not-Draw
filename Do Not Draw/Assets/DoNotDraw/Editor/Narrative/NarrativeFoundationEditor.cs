using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DoNotDraw.Narrative.Editor
{
    public sealed class NarrativeValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public static class NarrativeAssetValidator
    {
        [MenuItem("Tools/Do Not Draw/Validate Narrative Assets")]
        public static void ValidateAllAndLog()
        {
            NarrativeValidationReport report = ValidateAll();
            LogReport(report, "all narrative assets");
        }

        public static NarrativeValidationReport ValidateAll()
        {
            NarrativeValidationReport report = new NarrativeValidationReport();
            ValidateStableIds<CardDefinition>("card", card => card.StableId, report);
            ValidateStableIds<CardSequenceDefinition>("sequence", sequence => sequence.StableId, report);
            ValidateStableIds<StoryFact>("fact", fact => fact.StableId, report);
            ValidateStableIds<StorySignal>("signal", signal => signal.StableId, report);

            foreach (CardSequenceDefinition sequence in LoadAll<CardSequenceDefinition>())
            {
                ValidateSequence(sequence, report);
            }

            return report;
        }

        public static NarrativeValidationReport ValidateSequence(CardSequenceDefinition sequence)
        {
            NarrativeValidationReport report = new NarrativeValidationReport();
            if (sequence == null)
            {
                report.Errors.Add("Sequence is null.");
                return report;
            }

            ValidateSequence(sequence, report);
            return report;
        }

        public static void LogReport(NarrativeValidationReport report, string scope)
        {
            foreach (string warning in report.Warnings)
            {
                Debug.LogWarning($"[Narrative Validation] {warning}");
            }

            foreach (string error in report.Errors)
            {
                Debug.LogError($"[Narrative Validation] {error}");
            }

            if (report.IsValid)
            {
                Debug.Log($"[Narrative Validation] PASS - {scope}. Warnings: {report.Warnings.Count}");
            }
            else
            {
                Debug.LogError(
                    $"[Narrative Validation] FAIL - {scope}. Errors: {report.Errors.Count}, warnings: {report.Warnings.Count}");
            }
        }

        private static void ValidateSequence(CardSequenceDefinition sequence, NarrativeValidationReport report)
        {
            string sequenceLabel = string.IsNullOrWhiteSpace(sequence.StableId) ? sequence.name : sequence.StableId;
            if (sequence.StepCount == 0)
            {
                report.Errors.Add($"Sequence '{sequenceLabel}' has no steps.");
                return;
            }

            HashSet<string> stepIds = new HashSet<string>(System.StringComparer.Ordinal);
            int cardStepCount = 0;

            for (int stepIndex = 0; stepIndex < sequence.StepCount; stepIndex++)
            {
                CardSequenceStep step = sequence.GetStep(stepIndex);
                if (step == null)
                {
                    report.Errors.Add($"Sequence '{sequenceLabel}' contains a null step at index {stepIndex}.");
                    continue;
                }

                string stepLabel = string.IsNullOrWhiteSpace(step.StepId) ? $"index {stepIndex}" : step.StepId;
                if (string.IsNullOrWhiteSpace(step.StepId))
                {
                    report.Errors.Add($"Sequence '{sequenceLabel}' has a step without an ID at index {stepIndex}.");
                }
                else if (!stepIds.Add(step.StepId))
                {
                    report.Errors.Add($"Sequence '{sequenceLabel}' contains duplicate step ID '{step.StepId}'.");
                }

                if (step.DrawsCard)
                {
                    cardStepCount++;
                    if (step.Card == null)
                    {
                        report.Errors.Add($"Sequence '{sequenceLabel}', step '{stepLabel}' draws a card but has no CardDefinition.");
                    }
                }
                else if (step.Card != null)
                {
                    report.Warnings.Add($"Sequence '{sequenceLabel}', event-only step '{stepLabel}' has an unused CardDefinition.");
                }

                ValidateConditionGroup(step.DrawAvailability, sequenceLabel, stepLabel, "availability", report);
                ValidateConditionGroup(step.CompletionConditions, sequenceLabel, stepLabel, "completion", report);

                bool defaultTransitionSeen = false;
                for (int transitionIndex = 0; transitionIndex < step.Transitions.Count; transitionIndex++)
                {
                    CardSequenceTransition transition = step.Transitions[transitionIndex];
                    if (transition == null)
                    {
                        report.Errors.Add($"Sequence '{sequenceLabel}', step '{stepLabel}' has a null transition.");
                        continue;
                    }

                    if (defaultTransitionSeen)
                    {
                        report.Warnings.Add(
                            $"Sequence '{sequenceLabel}', step '{stepLabel}' has an unreachable transition after a default transition.");
                    }

                    if (transition.IsDefault)
                    {
                        defaultTransitionSeen = true;
                    }

                    ValidateConditionGroup(transition.Conditions, sequenceLabel, stepLabel, "transition", report);

                    if (!transition.FinishSequence
                        && !sequence.TryGetStepIndex(transition.TargetStepId, out _))
                    {
                        report.Errors.Add(
                            $"Sequence '{sequenceLabel}', step '{stepLabel}' targets missing step '{transition.TargetStepId}'.");
                    }
                }
            }

            if (sequence.VisualDeckSize < cardStepCount)
            {
                report.Warnings.Add(
                    $"Sequence '{sequenceLabel}' visually contains {sequence.VisualDeckSize} cards but has {cardStepCount} card steps.");
            }
        }

        private static void ValidateConditionGroup(
            StoryConditionGroup group,
            string sequenceId,
            string stepId,
            string context,
            NarrativeValidationReport report)
        {
            if (group == null || group.IsEmpty)
            {
                return;
            }

            for (int index = 0; index < group.Conditions.Count; index++)
            {
                StoryCondition condition = group.Conditions[index];
                if (condition == null)
                {
                    report.Errors.Add($"Sequence '{sequenceId}', step '{stepId}' has a null {context} condition.");
                }
                else if (condition.Fact == null)
                {
                    report.Errors.Add($"Sequence '{sequenceId}', step '{stepId}' has a {context} condition without a StoryFact.");
                }
                else if (!condition.IsComparisonSupported)
                {
                    report.Errors.Add(
                        $"Sequence '{sequenceId}', step '{stepId}' uses unsupported comparison '{condition.Comparison}' "
                        + $"for fact '{condition.Fact.StableId}' ({condition.Fact.FactType}).");
                }
            }
        }

        private static void ValidateStableIds<T>(
            string kind,
            System.Func<T, string> getStableId,
            NarrativeValidationReport report)
            where T : ScriptableObject
        {
            Dictionary<string, T> seen = new Dictionary<string, T>(System.StringComparer.Ordinal);
            foreach (T asset in LoadAll<T>())
            {
                string stableId = getStableId(asset);
                if (string.IsNullOrWhiteSpace(stableId))
                {
                    report.Errors.Add($"{kind} asset '{AssetDatabase.GetAssetPath(asset)}' has no stable ID.");
                    continue;
                }

                if (seen.TryGetValue(stableId, out T existing))
                {
                    report.Errors.Add(
                        $"Duplicate {kind} ID '{stableId}' in '{AssetDatabase.GetAssetPath(existing)}' "
                        + $"and '{AssetDatabase.GetAssetPath(asset)}'.");
                }
                else
                {
                    seen.Add(stableId, asset);
                }
            }
        }

        private static IEnumerable<T> LoadAll<T>() where T : ScriptableObject
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }
    }

    [CustomEditor(typeof(CardSequenceDefinition))]
    public sealed class CardSequenceDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate This Sequence"))
            {
                CardSequenceDefinition sequence = (CardSequenceDefinition)target;
                NarrativeAssetValidator.LogReport(
                    NarrativeAssetValidator.ValidateSequence(sequence),
                    $"sequence '{sequence.StableId}'");
            }
        }
    }

    [CustomEditor(typeof(CardSequenceRunner))]
    public sealed class CardSequenceRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CardSequenceRunner runner = (CardSequenceRunner)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State", runner.State.ToString());
            EditorGUILayout.LabelField("Current Step", runner.CurrentStep?.StepId ?? "None");
            EditorGUILayout.LabelField("Draw Count", runner.DrawCount.ToString());

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime controls are available in Play Mode only.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Restart Sequence"))
            {
                runner.RestartSequence();
            }

            using (new EditorGUI.DisabledScope(!runner.CanPlayerDraw))
            {
                if (GUILayout.Button("Simulate Player Draw"))
                {
                    runner.RequestPlayerDraw();
                }
            }

            bool canForceComplete = runner.State == CardSequenceState.WaitingForAvailability
                || runner.State == CardSequenceState.ReadyForActivation
                || runner.State == CardSequenceState.WaitingForCompletion;
            using (new EditorGUI.DisabledScope(!canForceComplete))
            {
                if (GUILayout.Button("Force Complete Current Step"))
                {
                    runner.DebugForceCompleteCurrentStep();
                }
            }
        }
    }
}

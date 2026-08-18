using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoNotDraw.Interaction;
using DoNotDraw.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoNotDraw.Narrative.Editor
{
    public static class FinalExperienceBuilder
    {
        private const string ScenePath = "Assets/Scenes/ClosedRoom.unity";
        private const string FinalRootName = "FINAL EXPERIENCE - FLOW AUTHORITY";
        private const string FinalDataRoot = "Assets/DoNotDraw/Narrative/Final";
        private const string CardRoot = FinalDataRoot + "/Cards";
        private const string FactRoot = FinalDataRoot + "/Facts";
        private const string SignalRoot = FinalDataRoot + "/Signals";
        private const string SequenceRoot = FinalDataRoot + "/Sequences";
        private const string FinalMaterialRoot = "Assets/DoNotDraw/Materials/Final";
        private const string VoiceSourcePath = "Assets/Sounds/voice.mp3";
        private const string VoiceOutputRoot = "Assets/Sounds/voice";

        private sealed class NarrativeAssets
        {
            public readonly Dictionary<string, CardDefinition> Cards = new Dictionary<string, CardDefinition>();
            public readonly Dictionary<string, StoryFact> Facts = new Dictionary<string, StoryFact>();
            public readonly Dictionary<string, StorySignal> Signals = new Dictionary<string, StorySignal>();
            public CardSequenceDefinition Sequence;
        }

        private sealed class CardSpec
        {
            public string Key;
            public string Text;
            public string DisplayName;
        }

        private sealed class ConditionSpec
        {
            public StoryFact Fact;
            public bool Expected = true;
        }

        private sealed class TransitionSpec
        {
            public string Target;
            public bool Finish;
            public readonly List<ConditionSpec> Conditions = new List<ConditionSpec>();
        }

        private sealed class VariantSpec
        {
            public CardDefinition Card;
            public readonly List<ConditionSpec> Conditions = new List<ConditionSpec>();
        }

        private sealed class StepSpec
        {
            public string Id;
            public string Label;
            public CardSequenceStepMode Mode = CardSequenceStepMode.PlayerDraw;
            public CardDefinition Card;
            public float ReadyDelay;
            public float CompletionDelay = 0.5f;
            public bool AllowExternalAdvance;
            public readonly List<ConditionSpec> CompletionConditions = new List<ConditionSpec>();
            public readonly List<StorySignal> EnterSignals = new List<StorySignal>();
            public readonly List<StorySignal> ActivationSignals = new List<StorySignal>();
            public readonly List<StorySignal> RevealSignals = new List<StorySignal>();
            public readonly List<StorySignal> CompleteSignals = new List<StorySignal>();
            public readonly List<TransitionSpec> Transitions = new List<TransitionSpec>();
            public readonly List<VariantSpec> Variants = new List<VariantSpec>();
        }

        private struct VoiceSegment
        {
            public int StartSample;
            public int EndSample;
        }

        [MenuItem("Tools/Do Not Draw/Build Final Flow Experience")]
        public static void BuildFinalFlowExperience()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Final Experience] Exit Play Mode before rebuilding the final flow.");
                return;
            }

            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Scene loading can unload unreferenced native asset objects. Resolve all build-time
            // asset references only after the target scene is open so they survive scene wiring.
            List<AudioClip> voiceClips = SplitVoiceRecording();
            NarrativeAssets assets = BuildNarrativeAssets(voiceClips);

            BuildScene(scene, assets);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            NarrativeValidationReport report = NarrativeAssetValidator.ValidateAll();
            NarrativeAssetValidator.LogReport(report, "final flow experience");
            Debug.Log(
                $"[Final Experience] BUILD COMPLETE. Voice segments: {voiceClips.Count}. "
                + $"Validation errors: {report.Errors.Count}, warnings: {report.Warnings.Count}.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(FinalDataRoot);
            EnsureFolder(CardRoot);
            EnsureFolder(FactRoot);
            EnsureFolder(SignalRoot);
            EnsureFolder(SequenceRoot);
            EnsureFolder(FinalMaterialRoot);
            EnsureFolder(VoiceOutputRoot);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static List<AudioClip> SplitVoiceRecording()
        {
            List<AudioClip> result = new List<AudioClip>();
            AudioImporter importer = AssetImporter.GetAtPath(VoiceSourcePath) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Voice Split] No recording found at '{VoiceSourcePath}'. Cards will remain silent.");
                return result;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool reimport = !importer.forceToMono
                || !settings.preloadAudioData
                || settings.loadType != AudioClipLoadType.DecompressOnLoad
                || settings.compressionFormat != AudioCompressionFormat.PCM;
            importer.forceToMono = true;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.quality = 1f;
            importer.defaultSampleSettings = settings;
            if (reimport)
            {
                importer.SaveAndReimport();
            }

            AudioClip source = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceSourcePath);
            if (source == null || !source.LoadAudioData())
            {
                Debug.LogWarning("[Voice Split] Unity could not decode voice.mp3.");
                return result;
            }

            float[] samples = new float[source.samples * source.channels];
            if (!source.GetData(samples, 0))
            {
                Debug.LogWarning("[Voice Split] AudioClip.GetData failed for voice.mp3.");
                return result;
            }

            float[] mono = ToMono(samples, source.channels);
            List<VoiceSegment> segments = DetectVoiceSegments(mono, source.frequency);
            if (segments.Count > 0)
            {
                float toneCheckDuration =
                    (segments[0].EndSample - segments[0].StartSample) / (float)source.frequency;
                segments.RemoveAt(0);
                Debug.Log(
                    $"[Voice Split] Skipped the first {toneCheckDuration:0.00}s segment (voice tone check). "
                    + "Final card mapping starts at detected segment 2.");
            }

            string absoluteRoot = Path.Combine(Application.dataPath, "Sounds/voice");
            Directory.CreateDirectory(absoluteRoot);
            DeleteStaleGeneratedVoiceAssets(segments.Count);

            for (int index = 0; index < segments.Count; index++)
            {
                VoiceSegment segment = segments[index];
                string fileName = $"Voice_{index + 1:00}.wav";
                string absolutePath = Path.Combine(absoluteRoot, fileName);
                WritePcm16Wav(
                    absolutePath,
                    mono,
                    segment.StartSample,
                    segment.EndSample,
                    source.frequency);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int index = 0; index < segments.Count; index++)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    $"{VoiceOutputRoot}/Voice_{index + 1:00}.wav");
                if (clip != null)
                {
                    result.Add(clip);
                }
            }

            string durations = string.Join(
                ", ",
                segments.Select(segment =>
                    ((segment.EndSample - segment.StartSample) / (float)source.frequency).ToString("0.00s")));
            Debug.Log($"[Voice Split] Detected {segments.Count} spoken segments: {durations}");
            return result;
        }

        private static void DeleteStaleGeneratedVoiceAssets(int expectedCount)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceOutputRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(path);
                if (!fileName.StartsWith("Voice_", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string indexText = Path.GetFileNameWithoutExtension(fileName).Substring("Voice_".Length);
                if (int.TryParse(indexText, out int clipIndex)
                    && clipIndex >= 1
                    && clipIndex <= expectedCount)
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(path);
            }
        }

        private static float[] ToMono(float[] interleaved, int channels)
        {
            if (channels <= 1)
            {
                return interleaved;
            }

            int sampleCount = interleaved.Length / channels;
            float[] mono = new float[sampleCount];
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                float sum = 0f;
                for (int channel = 0; channel < channels; channel++)
                {
                    sum += interleaved[sampleIndex * channels + channel];
                }

                mono[sampleIndex] = sum / channels;
            }

            return mono;
        }

        private static List<VoiceSegment> DetectVoiceSegments(float[] samples, int frequency)
        {
            int windowSamples = Mathf.Max(1, frequency / 50);
            int windowCount = Mathf.CeilToInt(samples.Length / (float)windowSamples);
            float[] rms = new float[windowCount];
            float maxRms = 0f;

            for (int window = 0; window < windowCount; window++)
            {
                int start = window * windowSamples;
                int end = Mathf.Min(samples.Length, start + windowSamples);
                double squareSum = 0d;
                for (int index = start; index < end; index++)
                {
                    squareSum += samples[index] * samples[index];
                }

                rms[window] = end > start ? Mathf.Sqrt((float)(squareSum / (end - start))) : 0f;
                maxRms = Mathf.Max(maxRms, rms[window]);
            }

            float threshold = Mathf.Max(0.004f, maxRms * 0.055f);
            const int minimumActiveWindows = 3;
            const int silenceWindowsToEnd = 32;
            const int preRollWindows = 5;
            const int postRollWindows = 9;
            List<VoiceSegment> detected = new List<VoiceSegment>();
            int activeRun = 0;
            int silenceRun = 0;
            int segmentStartWindow = -1;

            for (int window = 0; window < windowCount; window++)
            {
                if (rms[window] >= threshold)
                {
                    activeRun++;
                    silenceRun = 0;
                    if (segmentStartWindow < 0 && activeRun >= minimumActiveWindows)
                    {
                        segmentStartWindow = Mathf.Max(0, window - activeRun + 1 - preRollWindows);
                    }
                }
                else
                {
                    activeRun = 0;
                    if (segmentStartWindow >= 0)
                    {
                        silenceRun++;
                        if (silenceRun >= silenceWindowsToEnd)
                        {
                            int endWindow = Mathf.Min(
                                windowCount,
                                window - silenceRun + 1 + postRollWindows);
                            AddSegment(detected, segmentStartWindow, endWindow, windowSamples, samples.Length, frequency);
                            segmentStartWindow = -1;
                            silenceRun = 0;
                        }
                    }
                }
            }

            if (segmentStartWindow >= 0)
            {
                AddSegment(detected, segmentStartWindow, windowCount, windowSamples, samples.Length, frequency);
            }

            List<VoiceSegment> merged = new List<VoiceSegment>();
            int mergeGap = Mathf.RoundToInt(frequency * 0.42f);
            foreach (VoiceSegment segment in detected)
            {
                if (merged.Count > 0 && segment.StartSample - merged[merged.Count - 1].EndSample <= mergeGap)
                {
                    VoiceSegment previous = merged[merged.Count - 1];
                    previous.EndSample = segment.EndSample;
                    merged[merged.Count - 1] = previous;
                }
                else
                {
                    merged.Add(segment);
                }
            }

            return merged;
        }

        private static void AddSegment(
            List<VoiceSegment> output,
            int startWindow,
            int endWindow,
            int windowSamples,
            int totalSamples,
            int frequency)
        {
            VoiceSegment segment = new VoiceSegment
            {
                StartSample = Mathf.Clamp(startWindow * windowSamples, 0, totalSamples),
                EndSample = Mathf.Clamp(endWindow * windowSamples, 0, totalSamples)
            };
            if (segment.EndSample - segment.StartSample >= frequency * 0.25f)
            {
                output.Add(segment);
            }
        }

        private static void WritePcm16Wav(
            string path,
            float[] samples,
            int startSample,
            int endSample,
            int frequency)
        {
            int sampleCount = Mathf.Max(0, endSample - startSample);
            int dataBytes = sampleCount * sizeof(short);
            using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(frequency);
            writer.Write(frequency * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);

            for (int index = startSample; index < endSample; index++)
            {
                float clamped = Mathf.Clamp(samples[index], -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(clamped * short.MaxValue));
            }
        }

        private static NarrativeAssets BuildNarrativeAssets(IReadOnlyList<AudioClip> voiceClips)
        {
            NarrativeAssets assets = new NarrativeAssets();
            Material redAccent = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/DoNotDraw/Materials/Cards/CardAccentRed.mat");
            Material blackAccent = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/DoNotDraw/Materials/Cards/CardAccentBlack.mat");

            List<CardSpec> cardSpecs = CreateCardSpecs();
            for (int index = 0; index < cardSpecs.Count; index++)
            {
                CardSpec spec = cardSpecs[index];
                string path = $"{CardRoot}/{index + 1:00}_{spec.Key}.asset";
                CardDefinition card = GetOrCreateAsset<CardDefinition>(path);
                SerializedObject serialized = new SerializedObject(card);
                Set(serialized, "stableId", $"final.card.{spec.Key}");
                Set(serialized, "displayName", spec.DisplayName);
                Set(serialized, "faceText", spec.Text);
                Set(serialized, "faceAccentMaterial", index % 3 == 0 ? blackAccent : redAccent);
                Set(serialized, "faceTextColor", new Color(0.055f, 0.035f, 0.025f, 1f));
                Set(serialized, "voiceClip", index < voiceClips.Count ? voiceClips[index] : null);
                Set(serialized, "voiceVolume", 0.82f);
                Set(serialized, "voiceDelay", 0.16f);
                SetStringArray(serialized.FindProperty("tags"), new[] { "final", "flow-authority", spec.Key });
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);
                assets.Cards.Add(spec.Key, card);
            }

            if (voiceClips.Count != cardSpecs.Count)
            {
                Debug.LogWarning(
                    $"[Voice Split] Final flow has {cardSpecs.Count} spoken card variants, "
                    + $"but {voiceClips.Count} voice segments were detected. Sequential mapping was applied to the available clips.");
            }

            foreach (string factKey in new[]
                     {
                         "light_switch_used", "second_door_opened", "entered_second_room",
                         "enter_card_drawn", "exited_second_room", "window_silhouette_seen",
                         "turned_around", "door_silhouette_seen", "left_room"
                     })
            {
                StoryFact fact = GetOrCreateAsset<StoryFact>($"{FactRoot}/{factKey}.asset");
                SerializedObject serialized = new SerializedObject(fact);
                Set(serialized, "stableId", $"final.fact.{factKey}");
                Set(serialized, "description", $"Runtime state for final flow: {factKey}.");
                serialized.FindProperty("factType").enumValueIndex = (int)StoryFactType.Boolean;
                Set(serialized, "defaultBool", false);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fact);
                assets.Facts.Add(factKey, fact);
            }

            foreach (string signalKey in new[]
                     {
                         "rear_warning", "reveal_light_switch", "enable_light_switch",
                         "enable_second_door", "mark_enter_card_drawn", "slam_second_door",
                         "window_silhouette", "darken_for_hunt", "start_hunt", "settle_after_hunt",
                         "turn_around_test", "door_opens_itself", "door_crack_silhouette",
                         "open_exit", "show_ending"
                     })
            {
                StorySignal signal = GetOrCreateAsset<StorySignal>($"{SignalRoot}/{signalKey}.asset");
                SerializedObject serialized = new SerializedObject(signal);
                Set(serialized, "stableId", $"final.signal.{signalKey}");
                Set(serialized, "description", $"Scene cue for final flow: {signalKey}.");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(signal);
                assets.Signals.Add(signalKey, signal);
            }

            assets.Sequence = BuildFinalSequence(assets);
            AssetDatabase.SaveAssets();
            return assets;
        }

        private static List<CardSpec> CreateCardSpecs()
        {
            return new List<CardSpec>
            {
                Card("do_not_draw_opening", "DO NOT DRAW."),
                Card("do_not_look_behind_early", "DO NOT LOOK BEHIND YOU."),
                Card("do_not_turn_off_light", "DO NOT TURN OFF THE LIGHT."),
                Card("do_not_open_second_door", "DO NOT OPEN THE SECOND DOOR."),
                Card("do_not_enter", "DO NOT ENTER."),
                Card("do_not_draw_next_room_card", "DO NOT DRAW CARD OF NEXT ROOM."),
                Card("do_not_look_at_door", "DO NOT LOOK AT THE DOOR."),
                Card("do_not_look_through_window", "DO NOT LOOK THROUGH THE WINDOW."),
                Card("you_already_did", "YOU ALREADY DID."),
                Card("do_not_draw_next_card", "DO NOT DRAW THE NEXT CARD."),
                Card("do_not_draw_survival", "DO NOT DRAW."),
                Card("do_not_turn_around", "DO NOT TURN AROUND."),
                Card("good", "GOOD."),
                Card("i_saw_you_look", "I SAW YOU LOOK."),
                Card("do_not_touch_door", "DO NOT TOUCH THE DOOR."),
                Card("why_did_you_open_it", "WHY DID YOU OPEN IT?"),
                Card("do_not_blame_cards", "DO NOT BLAME THE CARDS."),
                Card("do_not_look_behind_door", "DO NOT LOOK BEHIND YOU."),
                Card("you_saw_it", "YOU SAW IT."),
                Card("do_not_leave", "DO NOT LEAVE."),
                Card("do_not_draw_again", "DO NOT DRAW AGAIN.")
            };
        }

        private static CardSpec Card(string key, string text)
        {
            return new CardSpec { Key = key, Text = text, DisplayName = text };
        }

        private static CardSequenceDefinition BuildFinalSequence(NarrativeAssets assets)
        {
            Dictionary<string, StoryFact> f = assets.Facts;
            Dictionary<string, StorySignal> s = assets.Signals;
            Dictionary<string, CardDefinition> c = assets.Cards;
            List<StepSpec> steps = new List<StepSpec>();

            steps.Add(Step("s01_opening", c["do_not_draw_opening"], 0.55f));

            StepSpec rear = Step("s02_rear_warning", c["do_not_look_behind_early"], 1.2f);
            rear.RevealSignals.Add(s["rear_warning"]);
            steps.Add(rear);

            StepSpec light = Step("s03_light_rule", c["do_not_turn_off_light"], 0.55f, true);
            light.EnterSignals.Add(s["reveal_light_switch"]);
            light.RevealSignals.Add(s["enable_light_switch"]);
            light.Transitions.Add(Transition("s04_second_door", Condition(f["light_switch_used"])));
            light.Transitions.Add(Transition("s03_light_rule"));
            steps.Add(light);

            StepSpec door = Step("s04_second_door", c["do_not_open_second_door"], 0.55f, true);
            door.RevealSignals.Add(s["enable_second_door"]);
            door.Transitions.Add(Transition("s05_do_not_enter", Condition(f["second_door_opened"])));
            door.Transitions.Add(Transition("s04_second_door"));
            steps.Add(door);

            StepSpec enter = Step("s05_do_not_enter", c["do_not_enter"], 0.65f, true);
            enter.Transitions.Add(Transition("s06_look_at_door", Condition(f["entered_second_room"])));
            enter.Transitions.Add(Transition("s05_do_not_enter"));
            steps.Add(enter);

            steps.Add(Step("s05a_next_room_card", c["do_not_draw_next_room_card"], 0.7f));

            StepSpec waitReenter = EventStep("s05a_wait_reenter");
            waitReenter.CompletionConditions.Add(Condition(f["entered_second_room"]));
            steps.Add(waitReenter);

            StepSpec lookDoor = Step("s06_look_at_door", c["do_not_look_at_door"], 0.9f, true);
            lookDoor.ActivationSignals.Add(s["mark_enter_card_drawn"]);
            lookDoor.RevealSignals.Add(s["slam_second_door"]);
            lookDoor.Transitions.Add(Transition(
                "s05a_next_room_card",
                Condition(f["exited_second_room"]),
                Condition(f["enter_card_drawn"], false)));
            steps.Add(lookDoor);

            StepSpec window = Step("s07_window", c["do_not_look_through_window"], 2.2f);
            window.RevealSignals.Add(s["window_silhouette"]);
            window.Transitions.Add(Transition("s08_already_did", Condition(f["window_silhouette_seen"])));
            window.Transitions.Add(Transition("s07_window"));
            steps.Add(window);

            StepSpec already = Step("s08_already_did", c["you_already_did"], 0.9f);
            already.CompleteSignals.Add(s["darken_for_hunt"]);
            steps.Add(already);

            StepSpec huntOne = Step("s09_survival_one", c["do_not_draw_next_card"], 0.8f);
            huntOne.ReadyDelay = 2.2f;
            huntOne.EnterSignals.Add(s["start_hunt"]);
            steps.Add(huntOne);

            StepSpec huntTwo = Step("s10_survival_two", c["do_not_draw_survival"], 0.9f);
            huntTwo.ReadyDelay = 1.6f;
            huntTwo.EnterSignals.Add(s["start_hunt"]);
            huntTwo.CompleteSignals.Add(s["settle_after_hunt"]);
            steps.Add(huntTwo);

            StepSpec turn = Step("s11_turn_around", c["do_not_turn_around"], 0.45f);
            turn.ReadyDelay = 1.2f;
            turn.RevealSignals.Add(s["turn_around_test"]);
            steps.Add(turn);

            StepSpec result = Step("s11_result", c["good"], 0.75f);
            VariantSpec sawLook = new VariantSpec { Card = c["i_saw_you_look"] };
            sawLook.Conditions.Add(Condition(f["turned_around"]));
            result.Variants.Add(sawLook);
            steps.Add(result);

            StepSpec touchDoor = Step("s12_touch_door", c["do_not_touch_door"], 2f);
            touchDoor.RevealSignals.Add(s["door_opens_itself"]);
            steps.Add(touchDoor);
            steps.Add(Step("s13_accusation", c["why_did_you_open_it"], 0.9f));
            steps.Add(Step("s14_blame", c["do_not_blame_cards"], 1f));

            StepSpec crack = Step("s15_door_crack", c["do_not_look_behind_door"], 1.8f, true);
            crack.RevealSignals.Add(s["door_crack_silhouette"]);
            crack.Transitions.Add(Transition("s16_you_saw_it", Condition(f["door_silhouette_seen"])));
            crack.Transitions.Add(Transition("s15_door_crack"));
            steps.Add(crack);

            steps.Add(Step("s16_you_saw_it", c["you_saw_it"], 0.9f));

            StepSpec leave = Step("s17_do_not_leave", c["do_not_leave"], 0f, true);
            leave.RevealSignals.Add(s["open_exit"]);
            leave.CompletionConditions.Add(Condition(f["left_room"]));
            steps.Add(leave);

            StepSpec ending = EventStep("s18_wall_ending");
            ending.ActivationSignals.Add(s["show_ending"]);
            ending.CompletionDelay = 6f;
            TransitionSpec finish = new TransitionSpec { Finish = true };
            ending.Transitions.Add(finish);
            steps.Add(ending);

            CardSequenceDefinition sequence = GetOrCreateAsset<CardSequenceDefinition>(
                $"{SequenceRoot}/FinalFlowSequence.asset");
            SerializedObject serialized = new SerializedObject(sequence);
            Set(serialized, "stableId", "final.sequence.flow_authority");
            Set(serialized, "description", "Authoritative implementation of 흐름. (벽); other documents only fill unspecified presentation details.");
            Set(serialized, "initialDeckSize", 48);
            SerializedProperty stepArray = serialized.FindProperty("steps");
            stepArray.arraySize = steps.Count;
            for (int index = 0; index < steps.Count; index++)
            {
                WriteStep(stepArray.GetArrayElementAtIndex(index), steps[index]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        private static StepSpec Step(
            string id,
            CardDefinition card,
            float completionDelay,
            bool allowExternal = false)
        {
            return new StepSpec
            {
                Id = id,
                Label = id,
                Card = card,
                CompletionDelay = completionDelay,
                AllowExternalAdvance = allowExternal
            };
        }

        private static StepSpec EventStep(string id)
        {
            return new StepSpec
            {
                Id = id,
                Label = id,
                Mode = CardSequenceStepMode.EventOnly,
                CompletionDelay = 0f
            };
        }

        private static ConditionSpec Condition(StoryFact fact, bool expected = true)
        {
            return new ConditionSpec { Fact = fact, Expected = expected };
        }

        private static TransitionSpec Transition(string target, params ConditionSpec[] conditions)
        {
            TransitionSpec transition = new TransitionSpec { Target = target };
            transition.Conditions.AddRange(conditions);
            return transition;
        }

        private static void WriteStep(SerializedProperty property, StepSpec step)
        {
            property.FindPropertyRelative("stepId").stringValue = step.Id;
            property.FindPropertyRelative("editorLabel").stringValue = step.Label;
            property.FindPropertyRelative("mode").enumValueIndex = (int)step.Mode;
            property.FindPropertyRelative("card").objectReferenceValue = step.Card;
            property.FindPropertyRelative("readyDelay").floatValue = step.ReadyDelay;
            property.FindPropertyRelative("completionDelay").floatValue = step.CompletionDelay;
            property.FindPropertyRelative("allowExternalAdvance").boolValue = step.AllowExternalAdvance;
            WriteConditionGroup(property.FindPropertyRelative("drawAvailability"), Array.Empty<ConditionSpec>());
            WriteConditionGroup(property.FindPropertyRelative("completionConditions"), step.CompletionConditions);
            WriteObjectArray(property.FindPropertyRelative("enterSignals"), step.EnterSignals);
            WriteObjectArray(property.FindPropertyRelative("activationSignals"), step.ActivationSignals);
            WriteObjectArray(property.FindPropertyRelative("revealSignals"), step.RevealSignals);
            WriteObjectArray(property.FindPropertyRelative("completeSignals"), step.CompleteSignals);

            SerializedProperty variants = property.FindPropertyRelative("cardVariants");
            variants.arraySize = step.Variants.Count;
            for (int index = 0; index < step.Variants.Count; index++)
            {
                VariantSpec variant = step.Variants[index];
                SerializedProperty variantProperty = variants.GetArrayElementAtIndex(index);
                variantProperty.FindPropertyRelative("card").objectReferenceValue = variant.Card;
                WriteConditionGroup(variantProperty.FindPropertyRelative("conditions"), variant.Conditions);
            }

            SerializedProperty transitions = property.FindPropertyRelative("transitions");
            transitions.arraySize = step.Transitions.Count;
            for (int index = 0; index < step.Transitions.Count; index++)
            {
                TransitionSpec transition = step.Transitions[index];
                SerializedProperty transitionProperty = transitions.GetArrayElementAtIndex(index);
                transitionProperty.FindPropertyRelative("finishSequence").boolValue = transition.Finish;
                transitionProperty.FindPropertyRelative("targetStepId").stringValue = transition.Target ?? string.Empty;
                WriteConditionGroup(
                    transitionProperty.FindPropertyRelative("conditions"),
                    transition.Conditions);
            }
        }

        private static void WriteConditionGroup(
            SerializedProperty group,
            IReadOnlyList<ConditionSpec> conditions)
        {
            group.FindPropertyRelative("matchMode").enumValueIndex = (int)StoryConditionMatchMode.All;
            SerializedProperty array = group.FindPropertyRelative("conditions");
            array.arraySize = conditions.Count;
            for (int index = 0; index < conditions.Count; index++)
            {
                ConditionSpec condition = conditions[index];
                SerializedProperty item = array.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("fact").objectReferenceValue = condition.Fact;
                item.FindPropertyRelative("comparison").enumValueIndex = (int)StoryComparison.Equals;
                item.FindPropertyRelative("boolValue").boolValue = condition.Expected;
            }
        }

        private static void WriteObjectArray<T>(SerializedProperty property, IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void BuildScene(Scene scene, NarrativeAssets assets)
        {
            GameObject oldRoot = FindSceneObject(scene, FinalRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }

            GameObject originalNorthWall = FindSceneObject(scene, "North Wall");
            GameObject originalDesk = FindSceneObject(scene, "Desk - Center");
            GameObject primaryDeckObject = FindSceneObject(scene, "Card Deck System");
            GameObject player = FindSceneObject(scene, "Player - First Person Controller");
            GameObject warmLightObject = FindSceneObject(scene, "Warm Point Light");
            GameObject atmosphereObject = FindSceneObject(scene, "Atmosphere - Horror");
            GameObject promptPanel = FindSceneObject(scene, "Draw Card Prompt");
            Text promptText = FindSceneObject(scene, "Prompt Text")?.GetComponent<Text>();

            if (originalNorthWall == null || originalDesk == null || primaryDeckObject == null || player == null)
            {
                throw new InvalidOperationException("[Final Experience] Required ClosedRoom scene objects were not found.");
            }

            originalNorthWall.SetActive(false);
            GameObject root = new GameObject(FinalRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            Material wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/DoNotDraw/Materials/Wall.mat");
            Material floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/DoNotDraw/Materials/Floor.mat");
            Material ceiling = AssetDatabase.LoadAssetAtPath<Material>("Assets/DoNotDraw/Materials/Ceiling.mat");
            Material doorMaterial = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/DoorDark.mat",
                new Color(0.075f, 0.055f, 0.042f, 1f),
                0.08f,
                0.2f,
                false);
            Material metal = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/SwitchMetal.mat",
                new Color(0.07f, 0.065f, 0.06f, 1f),
                0.72f,
                0.35f,
                false);
            Material silhouetteMaterial = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/Silhouette.mat",
                new Color(0.0015f, 0.001f, 0.001f, 1f),
                0f,
                0f,
                true);
            Material windowMaterial = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/WindowBlack.mat",
                new Color(0.008f, 0.012f, 0.016f, 1f),
                0.15f,
                0.12f,
                true);
            Material interactionGlow = GetOrCreateInteractionGlowMaterial();

            BuildFirstRoomDoorWall(root.transform, wall, doorMaterial);
            HorrorDoorInteractable secondDoor = FindChildRecursive(root.transform, "Second Door Pivot")
                .GetComponent<HorrorDoorInteractable>();
            GameObject secondDoorCover = FindChildRecursive(root.transform, "Second Door Concealing Wall").gameObject;
            ConfigureInteractionGlow(secondDoor, interactionGlow);

            GameObject secondRoom = BuildSecondRoom(
                root.transform,
                originalDesk,
                wall,
                floor,
                ceiling,
                windowMaterial,
                out CardDeckPresenter secondPresenter,
                out CardDeckInteraction secondInteraction,
                out Light secondLight);

            CardSequenceRunner runner = primaryDeckObject.GetComponent<CardSequenceRunner>();
            StoryBlackboard blackboard = primaryDeckObject.GetComponent<StoryBlackboard>();
            CardDeckPresenter primaryPresenter = primaryDeckObject.GetComponent<CardDeckPresenter>();
            CardDeckInteraction primaryInteraction = primaryDeckObject.GetComponent<CardDeckInteraction>();
            ConfigureDeckInteraction(primaryDeckObject, primaryInteraction, runner, true);
            ConfigureDeckInteraction(secondInteraction.gameObject, secondInteraction, runner, false);
            ConfigureInteractionGlow(primaryInteraction, interactionGlow);
            ConfigureInteractionGlow(secondInteraction, interactionGlow);

            if (assets.Sequence == null || !EditorUtility.IsPersistent(assets.Sequence))
            {
                throw new InvalidOperationException(
                    "FinalFlowSequence must be a saved project asset before it is assigned to the scene runner.");
            }

            runner.Configure(assets.Sequence, blackboard, primaryPresenter, true, true);
            EditorUtility.SetDirty(runner);
            if (runner.Sequence != assets.Sequence)
            {
                throw new InvalidOperationException("Failed to wire FinalFlowSequence to CardSequenceRunner.");
            }

            Camera playerCamera = Camera.main;
            PlayerInteractionRouter interactionRouter = player.GetComponent<PlayerInteractionRouter>();
            if (interactionRouter == null)
            {
                interactionRouter = player.AddComponent<PlayerInteractionRouter>();
            }

            SerializedObject routerSerialized = new SerializedObject(interactionRouter);
            Set(routerSerialized, "viewTransform", playerCamera != null ? playerCamera.transform : player.transform);
            Set(routerSerialized, "maxDistance", 2.65f);
            Set(routerSerialized, "promptPanel", promptPanel);
            Set(routerSerialized, "promptText", promptText);
            routerSerialized.ApplyModifiedPropertiesWithoutUndo();

            AudioClip switchClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Horror/LightSwitch.wav");
            AudioClip doorCreak = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Horror/DoorCreak.wav");
            AudioClip doorSlam = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Horror/DoorSlam.wav");
            AudioClip rearWarning = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Horror/RearWarning.wav");
            AudioClip breathing = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Horror/ThreatBreathing.wav");
            AudioClip drone = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Horror/ThreatDrone.wav");
            AudioClip whoosh = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/Card/Card_Game_Movement_Deal_Single_Whoosh_02.wav");

            ConfigureDoor(secondDoor, doorCreak, doorSlam);
            HorrorLightSwitchInteractable lightSwitch = BuildLightSwitch(
                root.transform,
                originalDesk.transform,
                metal,
                switchClip);
            ConfigureInteractionGlow(lightSwitch, interactionGlow);

            Transform rearTarget = CreateMarker(root.transform, "Rear Warning Target", new Vector3(0f, 1.25f, -2.75f));
            GameObject windowSilhouette = CreateSilhouette(
                "Window Silhouette",
                secondRoom.transform,
                new Vector3(0f, 0f, 9.28f),
                silhouetteMaterial,
                0.82f);
            Transform windowTarget = CreateMarker(windowSilhouette.transform, "Window Gaze Target", new Vector3(0f, 1.45f, 0f), true);

            GameObject threat = CreateSilhouette(
                "Approaching Silhouette",
                secondRoom.transform,
                new Vector3(0f, 0f, 8.65f),
                silhouetteMaterial,
                1.05f);
            Transform threatStart = CreateMarker(root.transform, "Threat Start", new Vector3(0f, 0f, 8.65f));
            Transform threatEnd = CreateMarker(root.transform, "Threat End", new Vector3(0f, 0f, 6.15f));

            GameObject crackSilhouette = CreateSilhouette(
                "Door Crack Silhouette",
                root.transform,
                new Vector3(1.4f, 0f, 2.63f),
                silhouetteMaterial,
                0.9f);
            Transform crackTarget = CreateMarker(crackSilhouette.transform, "Door Crack Gaze Target", new Vector3(0f, 1.5f, 0f), true);

            NarrativeZoneTrigger secondRoomZone = CreateZone(
                "Second Room Entry Zone",
                root.transform,
                new Vector3(1.4f, 1f, 3.82f),
                new Vector3(1.45f, 2f, 0.72f),
                NarrativeZoneId.SecondRoom,
                player.transform,
                true);
            NarrativeZoneTrigger returnZone = CreateZone(
                "Return To First Room Zone",
                root.transform,
                new Vector3(1.4f, 1f, 2.62f),
                new Vector3(1.45f, 2f, 0.72f),
                NarrativeZoneId.ReturnedToFirstRoom,
                player.transform,
                false);

            BuildEndingCorridor(
                root.transform,
                wall,
                ceiling,
                player.transform,
                out GameObject endingCorridor,
                out GameObject endingMessage,
                out NarrativeZoneTrigger endingZone);
            CanvasGroup screenFade = BuildScreenFade(root.transform);

            GameObject directorObject = new GameObject("Closed Room Story Director");
            directorObject.transform.SetParent(root.transform, false);
            AudioSource rearSource = AddAudioSource(directorObject, true);
            AudioSource threatSource = AddAudioSource(directorObject, true);
            AudioSource oneShotSource = AddAudioSource(directorObject, false);
            ClosedRoomStoryDirector director = directorObject.AddComponent<ClosedRoomStoryDirector>();
            Behaviour movement = player.GetComponentsInChildren<Behaviour>(true)
                .FirstOrDefault(component => component.GetType().Name == "FirstPersonController");
            ConfigurePlayerMovement(movement);
            Light firstLight = warmLightObject != null ? warmLightObject.GetComponent<Light>() : null;
            AudioSource ambience = atmosphereObject != null ? atmosphereObject.GetComponent<AudioSource>() : null;

            ConfigureDirector(
                director,
                assets,
                runner,
                blackboard,
                player.transform,
                playerCamera != null ? playerCamera.transform : player.transform,
                movement,
                primaryPresenter,
                primaryInteraction,
                secondPresenter,
                secondInteraction,
                firstLight,
                secondLight,
                lightSwitch,
                secondDoor,
                secondDoorCover,
                secondRoomZone,
                returnZone,
                endingZone,
                rearTarget,
                windowTarget,
                crackTarget,
                windowSilhouette,
                threat.transform,
                threatStart,
                threatEnd,
                crackSilhouette,
                endingCorridor,
                endingMessage,
                screenFade,
                ambience,
                rearSource,
                threatSource,
                oneShotSource,
                rearWarning,
                breathing,
                whoosh,
                drone);
        }

        private static void BuildFirstRoomDoorWall(Transform parent, Material wall, Material doorMaterial)
        {
            GameObject wallRoot = new GameObject("North Wall - Door Layout");
            wallRoot.transform.SetParent(parent, false);
            CreateCube("Wall Left", wallRoot.transform, new Vector3(-3.125f, 1.5f, 3.1f), new Vector3(2.15f, 3.2f, 0.2f), wall);
            CreateCube("Wall Center", wallRoot.transform, new Vector3(0f, 1.5f, 3.1f), new Vector3(1.5f, 3.2f, 0.2f), wall);
            CreateCube("Wall Right", wallRoot.transform, new Vector3(3.125f, 1.5f, 3.1f), new Vector3(2.15f, 3.2f, 0.2f), wall);
            CreateCube("Door Lintel", wallRoot.transform, new Vector3(0f, 2.75f, 3.1f), new Vector3(4.1f, 0.7f, 0.22f), wall);
            CreateCube("First Door - Locked", wallRoot.transform, new Vector3(-1.4f, 1.17f, 2.98f), new Vector3(1.3f, 2.48f, 0.12f), doorMaterial);
            CreateCube(
                "Second Door Concealing Wall",
                wallRoot.transform,
                new Vector3(1.4f, 1.17f, 2.98f),
                new Vector3(1.3f, 2.5f, 0.14f),
                wall);

            GameObject pivot = new GameObject("Second Door Pivot");
            pivot.transform.SetParent(wallRoot.transform, false);
            pivot.transform.position = new Vector3(0.75f, -0.07f, 2.98f);
            GameObject panel = CreateCube(
                "Second Door Panel",
                pivot.transform,
                Vector3.zero,
                new Vector3(1.3f, 2.48f, 0.12f),
                doorMaterial);
            panel.transform.localPosition = new Vector3(0.65f, 1.24f, 0f);
            GameObject handle = CreatePrimitive(
                PrimitiveType.Sphere,
                "Second Door Handle",
                panel.transform,
                false,
                doorMaterial);
            handle.transform.localPosition = new Vector3(0.48f, 0f, -0.12f);
            handle.transform.localScale = Vector3.one * 0.1f;
            Transform interactionPoint = CreateMarker(panel.transform, "Door Interaction Point", new Vector3(0.48f, 0f, -0.18f), true);

            AudioSource audio = pivot.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 12f;
            HorrorDoorInteractable door = pivot.AddComponent<HorrorDoorInteractable>();
            SerializedObject serialized = new SerializedObject(door);
            Set(serialized, "pivot", pivot.transform);
            Set(serialized, "interactionPoint", interactionPoint);
            Set(serialized, "interactionEnabled", false);
            Set(serialized, "openAngle", -96f);
            Set(serialized, "partialOpenAngle", -14f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildSecondRoom(
            Transform parent,
            GameObject originalDesk,
            Material wall,
            Material floor,
            Material ceiling,
            Material windowMaterial,
            out CardDeckPresenter presenter,
            out CardDeckInteraction interaction,
            out Light roomLight)
        {
            GameObject room = new GameObject("Second Concrete Room");
            room.transform.SetParent(parent, false);
            CreateCube("Second Floor", room.transform, new Vector3(0f, -0.1f, 6.4f), new Vector3(8.4f, 0.2f, 6.4f), floor);
            CreateCube("Second Ceiling", room.transform, new Vector3(0f, 3.1f, 6.4f), new Vector3(8.4f, 0.2f, 6.4f), ceiling);
            CreateCube("Second West Wall", room.transform, new Vector3(-4.1f, 1.5f, 6.4f), new Vector3(0.2f, 3.2f, 6.4f), wall);
            CreateCube("Second East Wall", room.transform, new Vector3(4.1f, 1.5f, 6.4f), new Vector3(0.2f, 3.2f, 6.4f), wall);

            CreateCube("Second South Left", room.transform, new Vector3(-1.725f, 1.5f, 3.3f), new Vector3(4.95f, 3.2f, 0.2f), wall);
            CreateCube("Second South Right", room.transform, new Vector3(3.125f, 1.5f, 3.3f), new Vector3(2.15f, 3.2f, 0.2f), wall);
            CreateCube("Second South Lintel", room.transform, new Vector3(1.4f, 2.75f, 3.3f), new Vector3(1.3f, 0.7f, 0.2f), wall);

            CreateCube("Window Wall Left", room.transform, new Vector3(-2.7f, 1.5f, 9.5f), new Vector3(3f, 3.2f, 0.2f), wall);
            CreateCube("Window Wall Right", room.transform, new Vector3(2.7f, 1.5f, 9.5f), new Vector3(3f, 3.2f, 0.2f), wall);
            CreateCube("Window Wall Bottom", room.transform, new Vector3(0f, 0.4f, 9.5f), new Vector3(2.4f, 1f, 0.2f), wall);
            CreateCube("Window Wall Top", room.transform, new Vector3(0f, 2.75f, 9.5f), new Vector3(2.4f, 0.7f, 0.2f), wall);
            CreateCube("Black Window", room.transform, new Vector3(0f, 1.7f, 9.43f), new Vector3(2.35f, 1.4f, 0.05f), windowMaterial, false);

            GameObject desk = UnityEngine.Object.Instantiate(originalDesk, room.transform);
            desk.name = "Desk - Second Room";
            desk.transform.position = new Vector3(0f, 0f, 6.4f);
            GameObject deckObject = FindChildRecursive(desk.transform, "Card Deck System").gameObject;
            CardSequenceRunner clonedRunner = deckObject.GetComponent<CardSequenceRunner>();
            StoryBlackboard clonedBlackboard = deckObject.GetComponent<StoryBlackboard>();
            if (clonedRunner != null)
            {
                UnityEngine.Object.DestroyImmediate(clonedRunner);
            }
            if (clonedBlackboard != null)
            {
                UnityEngine.Object.DestroyImmediate(clonedBlackboard);
            }

            presenter = deckObject.GetComponent<CardDeckPresenter>();
            interaction = deckObject.GetComponent<CardDeckInteraction>();

            GameObject lightObject = new GameObject("Second Room Light");
            lightObject.transform.SetParent(room.transform, false);
            lightObject.transform.position = new Vector3(0f, 2.62f, 6.4f);
            roomLight = lightObject.AddComponent<Light>();
            roomLight.type = LightType.Point;
            roomLight.color = new Color(1f, 0.76f, 0.54f);
            roomLight.intensity = 760f;
            roomLight.range = 7.2f;
            roomLight.shadows = LightShadows.Soft;
            Material bulb = AssetDatabase.LoadAssetAtPath<Material>("Assets/DoNotDraw/Materials/BulbGlow.mat");
            GameObject bulbVisual = CreatePrimitive(PrimitiveType.Sphere, "Second Room Bulb", room.transform, false, bulb);
            bulbVisual.transform.position = new Vector3(0f, 2.72f, 6.4f);
            bulbVisual.transform.localScale = Vector3.one * 0.13f;
            return room;
        }

        private static void ConfigureDeckInteraction(
            GameObject deckObject,
            CardDeckInteraction interaction,
            CardSequenceRunner runner,
            bool enabled)
        {
            BoxCollider collider = deckObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = deckObject.AddComponent<BoxCollider>();
            }
            collider.center = new Vector3(0f, 0.06f, 0f);
            collider.size = new Vector3(0.85f, 0.25f, 1.12f);

            Transform point = FindChildRecursive(deckObject.transform, "Deck Top - Back Facing Up");
            SerializedObject serialized = new SerializedObject(interaction);
            Set(serialized, "runner", runner);
            Set(serialized, "interactionPoint", point != null ? point : deckObject.transform);
            Set(serialized, "interactionEnabled", enabled);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HorrorLightSwitchInteractable BuildLightSwitch(
            Transform parent,
            Transform desk,
            Material material,
            AudioClip clip)
        {
            Transform desktop = FindChildRecursive(desk, "Desktop");
            MeshFilter desktopMesh = desktop != null ? desktop.GetComponent<MeshFilter>() : null;
            if (desktop == null || desktopMesh == null || desktopMesh.sharedMesh == null)
            {
                throw new InvalidOperationException(
                    "The table light switch requires a Desktop child with a mesh.");
            }

            Bounds desktopLocalBounds = desktopMesh.sharedMesh.bounds;
            Vector3 desktopScale = desktop.lossyScale;
            float desktopHalfWidth = Mathf.Abs(desktopLocalBounds.extents.x * desktopScale.x);
            float desktopHalfHeight = Mathf.Abs(desktopLocalBounds.extents.y * desktopScale.y);
            float desktopHalfDepth = Mathf.Abs(desktopLocalBounds.extents.z * desktopScale.z);
            const float sideInset = 0.32f;
            const float depthOffset = 0.08f;
            const float surfaceClearance = 0.002f;
            float rightOffset = Mathf.Max(0f, desktopHalfWidth - sideInset);
            float clampedDepthOffset = Mathf.Clamp(
                depthOffset,
                -Mathf.Max(0f, desktopHalfDepth - 0.28f),
                Mathf.Max(0f, desktopHalfDepth - 0.28f));

            GameObject root = new GameObject("Table Light Switch");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(
                desktop.position
                    + desktop.right * rightOffset
                    + desktop.forward * clampedDepthOffset
                    + desktop.up * (desktopHalfHeight + surfaceClearance),
                desktop.rotation);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.14f, 0f);
            collider.size = new Vector3(0.52f, 0.28f, 0.56f);

            GameObject switchBase = CreateLocalCube(
                "Switch Base",
                root.transform,
                new Vector3(0f, 0.05f, 0f),
                new Vector3(0.36f, 0.1f, 0.44f),
                material,
                false);
            GameObject lever = CreateLocalCube(
                "Switch Lever",
                root.transform,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(0.09f, 0.07f, 0.27f),
                material,
                false);
            Transform point = CreateMarker(
                root.transform,
                "Switch Interaction Point",
                new Vector3(0f, 0.22f, 0f),
                true);
            ValidateLightSwitchAssembly(root.transform, switchBase.transform, lever.transform, collider);

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            HorrorLightSwitchInteractable interactable = root.AddComponent<HorrorLightSwitchInteractable>();
            SerializedObject serialized = new SerializedObject(interactable);
            Set(serialized, "lever", lever.transform);
            Set(serialized, "interactionPoint", point);
            Set(serialized, "interactionEnabled", false);
            Set(serialized, "switchSound", clip);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return interactable;
        }

        private static void ValidateLightSwitchAssembly(
            Transform root,
            Transform switchBase,
            Transform lever,
            BoxCollider collider)
        {
            if (switchBase.parent != root || lever.parent != root)
            {
                throw new InvalidOperationException(
                    "Every light switch visual must be parented to the switch root.");
            }

            float baseBottom = switchBase.localPosition.y - switchBase.localScale.y * 0.5f;
            float baseTop = switchBase.localPosition.y + switchBase.localScale.y * 0.5f;
            float leverBottom = lever.localPosition.y - lever.localScale.y * 0.5f;
            if (baseBottom < -0.001f || leverBottom < baseTop - 0.001f)
            {
                throw new InvalidOperationException(
                    "The light switch base or lever intersects the tabletop assembly plane.");
            }

            Bounds visualBounds = new Bounds(switchBase.localPosition, switchBase.localScale);
            visualBounds.Encapsulate(new Bounds(lever.localPosition, lever.localScale));
            Bounds interactionBounds = new Bounds(collider.center, collider.size);
            if (!interactionBounds.Contains(visualBounds.min)
                || !interactionBounds.Contains(visualBounds.max))
            {
                throw new InvalidOperationException(
                    "The light switch interaction collider must contain the complete switch assembly.");
            }
        }

        private static void ConfigureDoor(
            HorrorDoorInteractable door,
            AudioClip openClip,
            AudioClip slamClip)
        {
            SerializedObject serialized = new SerializedObject(door);
            Set(serialized, "openSound", openClip);
            Set(serialized, "slamSound", slamClip);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateSilhouette(
            string name,
            Transform parent,
            Vector3 position,
            Material material,
            float scale)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;

            GameObject body = CreatePrimitive(PrimitiveType.Capsule, "Body", root.transform, false, material);
            body.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            body.transform.localScale = new Vector3(0.52f, 0.72f, 0.32f);
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, "Head", root.transform, false, material);
            head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            head.transform.localScale = new Vector3(0.46f, 0.52f, 0.42f);
            GameObject leftArm = CreateCube("Left Arm", root.transform, Vector3.zero, new Vector3(0.18f, 1.25f, 0.2f), material, false);
            leftArm.transform.localPosition = new Vector3(-0.42f, 1.12f, 0f);
            leftArm.transform.localRotation = Quaternion.Euler(0f, 0f, -5f);
            GameObject rightArm = CreateCube("Right Arm", root.transform, Vector3.zero, new Vector3(0.18f, 1.25f, 0.2f), material, false);
            rightArm.transform.localPosition = new Vector3(0.42f, 1.12f, 0f);
            rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);
            return root;
        }

        private static NarrativeZoneTrigger CreateZone(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            NarrativeZoneId id,
            Transform player,
            bool enabled)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = position;
            BoxCollider collider = zone.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
            NarrativeZoneTrigger trigger = zone.AddComponent<NarrativeZoneTrigger>();
            SerializedObject serialized = new SerializedObject(trigger);
            serialized.FindProperty("zoneId").enumValueIndex = (int)id;
            Set(serialized, "playerRoot", player);
            Set(serialized, "triggerEnabled", enabled);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        private static void BuildEndingCorridor(
            Transform parent,
            Material wall,
            Material ceiling,
            Transform player,
            out GameObject corridor,
            out GameObject message,
            out NarrativeZoneTrigger endingZone)
        {
            corridor = new GameObject("Ending Corridor - Initially Hidden");
            corridor.transform.SetParent(parent, false);
            CreateCube("Corridor Left", corridor.transform, new Vector3(0.68f, 1.5f, 1.7f), new Vector3(0.16f, 3.2f, 2.7f), wall);
            CreateCube("Corridor Right", corridor.transform, new Vector3(2.12f, 1.5f, 1.7f), new Vector3(0.16f, 3.2f, 2.7f), wall);
            CreateCube("Corridor Ceiling", corridor.transform, new Vector3(1.4f, 3.1f, 1.7f), new Vector3(1.6f, 0.2f, 2.7f), ceiling);
            GameObject endWall = CreateCube("Ending Wall", corridor.transform, new Vector3(1.4f, 1.5f, 0.36f), new Vector3(1.6f, 3.2f, 0.2f), wall);

            GameObject canvasObject = new GameObject("DO NOT DRAW AGAIN - Wall Message");
            canvasObject.transform.SetParent(endWall.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0.12f, 0.52f);
            canvasObject.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.006f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(220f, 90f);
            GameObject textObject = new GameObject("Ending Text");
            textObject.transform.SetParent(canvasObject.transform, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "DO NOT DRAW AGAIN.";
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.78f, 0.08f, 0.055f, 1f);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            message = canvasObject;

            endingZone = CreateZone(
                "Ending Exit Zone",
                corridor.transform,
                new Vector3(1.4f, 1f, 2.35f),
                new Vector3(1.3f, 2f, 0.65f),
                NarrativeZoneId.EndingCorridor,
                player,
                false);
            corridor.SetActive(false);
        }

        private static CanvasGroup BuildScreenFade(Transform parent)
        {
            GameObject canvasObject = new GameObject("Ending Screen Fade");
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasObject.AddComponent<CanvasScaler>();
            CanvasGroup group = canvasObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            GameObject imageObject = new GameObject("Black");
            imageObject.transform.SetParent(canvasObject.transform, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            RectTransform rect = image.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return group;
        }

        private static void ConfigureDirector(
            ClosedRoomStoryDirector director,
            NarrativeAssets assets,
            CardSequenceRunner runner,
            StoryBlackboard blackboard,
            Transform player,
            Transform view,
            Behaviour movement,
            CardDeckPresenter primaryPresenter,
            CardDeckInteraction primaryInteraction,
            CardDeckPresenter secondPresenter,
            CardDeckInteraction secondInteraction,
            Light firstLight,
            Light secondLight,
            HorrorLightSwitchInteractable lightSwitch,
            HorrorDoorInteractable secondDoor,
            GameObject secondDoorCover,
            NarrativeZoneTrigger secondZone,
            NarrativeZoneTrigger returnZone,
            NarrativeZoneTrigger endingZone,
            Transform rearTarget,
            Transform windowTarget,
            Transform crackTarget,
            GameObject windowSilhouette,
            Transform threat,
            Transform threatStart,
            Transform threatEnd,
            GameObject crackSilhouette,
            GameObject endingCorridor,
            GameObject endingMessage,
            CanvasGroup screenFade,
            AudioSource ambience,
            AudioSource rearSource,
            AudioSource threatSource,
            AudioSource oneShotSource,
            AudioClip rearWarning,
            AudioClip breathing,
            AudioClip whoosh,
            AudioClip drone)
        {
            SerializedObject serialized = new SerializedObject(director);
            Set(serialized, "runner", runner);
            Set(serialized, "blackboard", blackboard);
            Set(serialized, "playerRoot", player);
            Set(serialized, "playerView", view);
            Set(serialized, "movementController", movement);
            Set(serialized, "primaryPresenter", primaryPresenter);
            Set(serialized, "primaryInteraction", primaryInteraction);
            Set(serialized, "secondRoomPresenter", secondPresenter);
            Set(serialized, "secondRoomInteraction", secondInteraction);
            Set(serialized, "ceilingLight", firstLight);
            Set(serialized, "secondRoomLight", secondLight);
            Set(serialized, "lightSwitch", lightSwitch);
            Set(serialized, "lightSwitchRoot", lightSwitch.gameObject);
            Set(serialized, "secondDoor", secondDoor);
            Set(serialized, "secondDoorRoot", secondDoor.gameObject);
            Set(serialized, "secondDoorCover", secondDoorCover);
            Set(serialized, "secondRoomZone", secondZone);
            Set(serialized, "returnZone", returnZone);
            Set(serialized, "endingZone", endingZone);
            Set(serialized, "rearWarningTarget", rearTarget);
            Set(serialized, "windowGazeTarget", windowTarget);
            Set(serialized, "doorCrackGazeTarget", crackTarget);
            Set(serialized, "windowSilhouette", windowSilhouette);
            Set(serialized, "threatSilhouette", threat);
            Set(serialized, "threatStart", threatStart);
            Set(serialized, "threatEnd", threatEnd);
            Set(serialized, "doorCrackSilhouette", crackSilhouette);
            Set(serialized, "endingCorridor", endingCorridor);
            Set(serialized, "endingWallMessage", endingMessage);
            Set(serialized, "screenFade", screenFade);
            Set(serialized, "ambientSource", ambience);
            Set(serialized, "rearSource", rearSource);
            Set(serialized, "threatSource", threatSource);
            Set(serialized, "oneShotSource", oneShotSource);
            Set(serialized, "rearWarningClip", rearWarning);
            Set(serialized, "threatBreathingClip", breathing);
            Set(serialized, "silhouetteWhooshClip", whoosh);
            Set(serialized, "threatDroneClip", drone);
            Set(serialized, "endingVoiceClip", assets.Cards["do_not_draw_again"].VoiceClip);

            Dictionary<string, StoryFact> facts = assets.Facts;
            Set(serialized, "lightSwitchUsedFact", facts["light_switch_used"]);
            Set(serialized, "secondDoorOpenedFact", facts["second_door_opened"]);
            Set(serialized, "enteredSecondRoomFact", facts["entered_second_room"]);
            Set(serialized, "enterCardDrawnFact", facts["enter_card_drawn"]);
            Set(serialized, "exitedSecondRoomFact", facts["exited_second_room"]);
            Set(serialized, "windowSilhouetteSeenFact", facts["window_silhouette_seen"]);
            Set(serialized, "turnedAroundFact", facts["turned_around"]);
            Set(serialized, "doorSilhouetteSeenFact", facts["door_silhouette_seen"]);
            Set(serialized, "leftRoomFact", facts["left_room"]);

            (string signal, ClosedRoomCue cue)[] bindings =
            {
                ("rear_warning", ClosedRoomCue.StartRearWarning),
                ("reveal_light_switch", ClosedRoomCue.RevealLightSwitch),
                ("enable_light_switch", ClosedRoomCue.EnableLightSwitchInteraction),
                ("enable_second_door", ClosedRoomCue.EnableSecondDoorInteraction),
                ("mark_enter_card_drawn", ClosedRoomCue.MarkEnterCardDrawn),
                ("slam_second_door", ClosedRoomCue.SlamSecondDoor),
                ("window_silhouette", ClosedRoomCue.ShowWindowSilhouette),
                ("darken_for_hunt", ClosedRoomCue.DarkenForHunt),
                ("start_hunt", ClosedRoomCue.StartHunt),
                ("settle_after_hunt", ClosedRoomCue.SettleAfterHunt),
                ("turn_around_test", ClosedRoomCue.StartTurnAroundTest),
                ("door_opens_itself", ClosedRoomCue.OpenDoorByItself),
                ("door_crack_silhouette", ClosedRoomCue.ShowDoorCrackSilhouette),
                ("open_exit", ClosedRoomCue.OpenExit),
                ("show_ending", ClosedRoomCue.ShowEnding)
            };
            SerializedProperty cueArray = serialized.FindProperty("cueBindings");
            cueArray.arraySize = bindings.Length;
            for (int index = 0; index < bindings.Length; index++)
            {
                SerializedProperty binding = cueArray.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("signal").objectReferenceValue = assets.Signals[bindings[index].signal];
                binding.FindPropertyRelative("cue").enumValueIndex = (int)bindings[index].cue;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static AudioSource AddAudioSource(GameObject owner, bool spatial)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 14f;
            return source;
        }

        private static Material GetOrCreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness,
            bool unlit)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateInteractionGlowMaterial()
        {
            const string shaderName = "DoNotDraw/InteractionOuterGlow";
            string path = $"{FinalMaterialRoot}/InteractionOuterGlow.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Interaction glow shader '{shaderName}' was not imported.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_GlowColor", new Color(0.1f, 0.82f, 2.4f, 0.8f));
            material.SetFloat("_OutlineWidth", 0.026f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureInteractionGlow(
            PlayerInteractableBehaviour interactable,
            Material glowMaterial)
        {
            if (interactable == null)
            {
                throw new InvalidOperationException("Cannot configure interaction glow on a missing interactable.");
            }

            Renderer[] renderers = interactable.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Interactable '{interactable.name}' has no renderer for its outer glow.");
            }

            InteractableOuterGlow glow = interactable.GetComponent<InteractableOuterGlow>();
            if (glow == null)
            {
                glow = interactable.gameObject.AddComponent<InteractableOuterGlow>();
            }

            glow.Configure(glowMaterial, renderers);
            EditorUtility.SetDirty(glow);
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool keepCollider = true)
        {
            GameObject cube = CreatePrimitive(PrimitiveType.Cube, name, parent, keepCollider, material);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            return cube;
        }

        private static GameObject CreateLocalCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider = true)
        {
            GameObject cube = CreatePrimitive(PrimitiveType.Cube, name, parent, keepCollider, material);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            return cube;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            bool keepCollider,
            Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            return gameObject;
        }

        private static Transform CreateMarker(
            Transform parent,
            string name,
            Vector3 position,
            bool local = false)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            if (local)
            {
                marker.transform.localPosition = position;
            }
            else
            {
                marker.transform.position = position;
            }
            return marker.transform;
        }

        private static void ConfigurePlayerMovement(Behaviour movement)
        {
            if (movement == null)
            {
                throw new InvalidOperationException("FirstPersonController was not found; movement could not be configured.");
            }

            SerializedObject serialized = new SerializedObject(movement);
            SerializedProperty jumpHeight = serialized.FindProperty("JumpHeight");
            SerializedProperty topClamp = serialized.FindProperty("TopClamp");
            SerializedProperty bottomClamp = serialized.FindProperty("BottomClamp");
            if (topClamp == null || bottomClamp == null)
            {
                throw new InvalidOperationException(
                    "FirstPersonController camera clamp fields changed; update the final scene builder.");
            }

            // The customized controller removes jump entirely, so JumpHeight is optional.
            if (jumpHeight != null)
            {
                jumpHeight.floatValue = 0f;
            }

            topClamp.floatValue = 45f;
            bottomClamp.floatValue = -45f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(movement);
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindChildRecursive(root.transform, name);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform found = FindChildRecursive(parent.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, Color value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static void SetStringArray(SerializedProperty property, IReadOnlyList<string> values)
        {
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
        }
    }
}

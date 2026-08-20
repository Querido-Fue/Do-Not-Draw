using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoNotDraw.Audio;
using DoNotDraw.Interaction;
using DoNotDraw.UI;
using DoNotDraw.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoNotDraw.Narrative.Editor
{
    public static class FinalExperienceBuilder
    {
        private const string FinalRootName = "FINAL EXPERIENCE - FLOW AUTHORITY";
        private const string SettingsPopupRootName = "Settings Popup Runtime";
        private const string SettingsPopupPrefabPath = "Assets/Prefabs/SettingPopup.prefab";
        private const string VolumeManagerRootName = "Volume Manager Runtime";
        private const string VolumeManagerPrefabPath = "Assets/Prefabs/VolumeManager.prefab";
        private const string FinalDataRoot = "Assets/DoNotDraw/Narrative/Final";
        private const string CardRoot = FinalDataRoot + "/Cards";
        private const string FactRoot = FinalDataRoot + "/Facts";
        private const string SignalRoot = FinalDataRoot + "/Signals";
        private const string SequenceRoot = FinalDataRoot + "/Sequences";
        private const string FinalMaterialRoot = "Assets/DoNotDraw/Materials/Final";
        private const string BackroomsTextureRoot = "Assets/DoNotDraw/Textures/Backrooms";
        private const string BackroomsWallpaperPath = BackroomsTextureRoot + "/BackroomsWallpaper_Tileable.png";
        private const string BackroomsAssetMaterialRoot =
            "Assets/Asset/BackroomsLikeAsset/material";
        private const string BackroomsSourceCarpetMaterialPath =
            BackroomsAssetMaterialRoot + "/Floor_Carpet_Mat.mat";
        private const string BackroomsTrimMaterialPath =
            BackroomsAssetMaterialRoot + "/Wall_trim_mat.mat";
        private const string BackroomsTintedCarpetMaterialPath =
            FinalMaterialRoot + "/BackroomsAssetCarpetTinted.mat";
        private const string CardArtRoot = "Assets/Art/Card";
        private const string VoiceSourcePath = "Assets/Sounds/voice.mp3";
        private const string VoiceOutputRoot = "Assets/Sounds/voice";

        private sealed class NarrativeAssets
        {
            public readonly Dictionary<string, CardDefinition> Cards = new Dictionary<string, CardDefinition>();
            public readonly Dictionary<string, StoryFact> Facts = new Dictionary<string, StoryFact>();
            public readonly Dictionary<string, StorySignal> Signals = new Dictionary<string, StorySignal>();
            public CardSequenceDefinition Sequence;
        }

        private sealed class FinalAudioClips
        {
            public AudioClip FluorescentBuzz;
            public AudioClip ClockLoop;
            public AudioClip ClockDesynced;
            public AudioClip LowDrone;
            public AudioClip LowStinger;
            public AudioClip WhiteNoise;
            public AudioClip BreathTexture;
            public AudioClip DeckHover;
            public AudioClip ImpactThud;
            public AudioClip CardDraw;
            public AudioClip CardLanding;
            public AudioClip FirstRoomFootstep;
            public AudioClip SecondRoomFootstep;
            public AudioClip FootstepsBehind;
            public AudioClip SwitchOff;
            public AudioClip SwitchOn;
            public AudioClip DoorHandle;
            public AudioClip DoorCreak;
            public AudioClip StoryDoorCreak;
            public AudioClip DoorSlam;
            public AudioClip Wind;
            public AudioClip SilhouetteApproach;
            public AudioClip StoryHandle;
            public AudioClip FluorescentStarter;
        }

        private sealed class CardSpec
        {
            public string Key;
            public string Text;
            public string DisplayName;
            public string FaceTextureFileName;
            public CardTypographyStage TypographyStage;
            public float TextFadeDuration = 0.28f;
            public float DoubleExposureDuration;
            public bool LiftOnReveal;
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

        [MenuItem("Tools/Do Not Draw/Refresh Final Narrative Assets")]
        public static void RefreshFinalNarrativeAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Final Narrative] Exit Play Mode before refreshing narrative assets.");
                return;
            }

            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            List<AudioClip> voiceClips = SplitVoiceRecording();
            BuildNarrativeAssets(voiceClips);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            NarrativeValidationReport report = NarrativeAssetValidator.ValidateAll();
            NarrativeAssetValidator.LogReport(report, "final narrative assets");
            Debug.Log(
                $"[Final Narrative] REFRESH COMPLETE. Voice segments: {voiceClips.Count}. "
                + $"Validation errors: {report.Errors.Count}, warnings: {report.Warnings.Count}. "
                + "ClosedRoom.unity was not opened or modified.");
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
                Set(serialized, "faceTexture", LoadCardFaceTexture(spec.FaceTextureFileName));
                Set(serialized, "faceAccentMaterial", index % 3 == 0 ? blackAccent : redAccent);
                Set(serialized, "faceTextColor", new Color(0.055f, 0.035f, 0.025f, 1f));
                Set(serialized, "voiceClip", index < voiceClips.Count ? voiceClips[index] : null);
                Set(serialized, "voiceVolume", 0.82f);
                Set(serialized, "voiceDelay", 0.16f);
                Set(serialized, "textFadeDuration", spec.TextFadeDuration);
                serialized.FindProperty("typographyStage").enumValueIndex = (int)spec.TypographyStage;
                Set(serialized, "doubleExposureDuration", spec.DoubleExposureDuration);
                Set(serialized, "liftOnReveal", spec.LiftOnReveal);
                Set(serialized, "revealLiftHeight", 0.045f);
                Set(serialized, "revealLiftDuration", 0.4f);
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
                         "turned_around", "turn_test_resolved", "door_silhouette_seen", "left_room"
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
                         "begin_opening", "pulse_opening_card", "reveal_opening_graffiti",
                         "rear_look_rule", "arm_light_rule",
                         "arm_second_door_rule", "arm_enter_rule", "mark_enter_card_drawn",
                         "resolve_room_card_edge", "act_one_to_two",
                         "resume_atmosphere", "close_second_door_on_look",
                         "arm_window_vision", "pause_sensory_beat",
                         "act_two_to_three", "start_hunt_far", "start_hunt_close",
                         "act_three_to_four", "start_turn_test", "schedule_first_door",
                         "swing_shadow", "open_exit", "prepare_ending", "show_ending"
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
                Card("do_not_draw_opening", "Do not drow", faceTextureFileName: "Card0_DoNotDraw.png"),
                Card("do_not_look_behind_early", "Do not LookBack", faceTextureFileName: "Card1_DoNotLookBack.png"),
                Card("do_not_turn_off_light", "Do Not Turn Off The Light", faceTextureFileName: "Card2_DoNotTurnOffTheLight.png"),
                Card("do_not_open_second_door", "Do NoT OPen thE DooR", faceTextureFileName: "Card3_DoNotOpenTheDoor.png"),
                Card("do_not_enter", "DO NOT ENTER", faceTextureFileName: "Card4_DoNotEnter.png"),
                Card("do_not_draw_next_room_card", "You See", faceTextureFileName: "Card6_YouSee.png"),
                Card("do_not_look_at_door", "Do Not Look At The Door", CardTypographyStage.Uneven, faceTextureFileName: "Card5_DoNotLookAtTheDoor.png"),
                Card("do_not_look_through_window", "Do not Look At The Window", CardTypographyStage.Uneven, faceTextureFileName: "Card7_DoNotLookAtTheWindow.png"),
                Card("you_already_did", "You Already Did", CardTypographyStage.Uneven, 1.05f, faceTextureFileName: "Card8_YouAleadyDid.png"),
                Card("do_not_draw_next_card", "Do nOt dRaW The NeXT cARd", CardTypographyStage.Uneven, faceTextureFileName: "Card9_DoNotDrawTheNextCard.png"),
                Card("do_not_draw_survival", "DO NOT DRAW", CardTypographyStage.Clean, 0.18f, faceTextureFileName: "Card10_DoNotDraw.png"),
                Card("do_not_turn_around", "dO nOT TuRN aROuNd", CardTypographyStage.Uneven, faceTextureFileName: "Card11_DoNotTurnAround.png"),
                Card("good", "Good", CardTypographyStage.Uneven, 0.28f, 0f, true, "Card12_Good.png"),
                Card("i_saw_you_look", "I Saw You Look", CardTypographyStage.Uneven, 0.28f, 0f, true, "Card12_ISawYouLook.png"),
                Card("do_not_touch_door", "Do noT TOUCH tHE dOOr", CardTypographyStage.Uneven, faceTextureFileName: "Card13_DoNotTouchTheDoor.png"),
                Card("why_did_you_open_it", "Why Did you OPen It?", CardTypographyStage.Uneven, 1.05f, faceTextureFileName: "Card14_WhyDidYouOpenIt.png"),
                Card("do_not_blame_cards", "Do NoT bLamE The CarDS", CardTypographyStage.Damaged, 0.8f, faceTextureFileName: "Card15_DoNotBlameTheCards.png"),
                Card("do_not_look_behind_door", "DO not lOOk bEHinD YOU", CardTypographyStage.DoubleExposure, 0.32f, 0.18f, faceTextureFileName: "Card16_DoNotLookBehindYou.png"),
                Card("you_saw_it", "You Saw it", CardTypographyStage.DoubleExposure, 1.05f, 0.3f, faceTextureFileName: "Card17_YouSawIt.png"),
                Card("do_not_leave", "DO not LeAve", CardTypographyStage.DoubleExposure, 0.32f, 0.18f, faceTextureFileName: "Card18_DoNotLeave.png"),
                Card("do_not_draw_again", "Do Not Drow Again", CardTypographyStage.DoubleExposure, 0.4f, 0.25f, false, "Card19_DoNotDrowAgain.png")
            };
        }

        private static CardSpec Card(
            string key,
            string text,
            CardTypographyStage typographyStage = CardTypographyStage.Clean,
            float textFadeDuration = 0.28f,
            float doubleExposureDuration = 0f,
            bool liftOnReveal = false,
            string faceTextureFileName = null)
        {
            return new CardSpec
            {
                Key = key,
                Text = text,
                DisplayName = text,
                FaceTextureFileName = faceTextureFileName,
                TypographyStage = typographyStage,
                TextFadeDuration = textFadeDuration,
                DoubleExposureDuration = doubleExposureDuration,
                LiftOnReveal = liftOnReveal
            };
        }

        private static Texture2D LoadCardFaceTexture(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string path = $"{CardArtRoot}/{fileName}";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Card face texture was not found at '{path}'.");
            }

            bool requiresReimport = importer.textureType != TextureImporterType.Default
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.npotScale != TextureImporterNPOTScale.None
                || !importer.mipmapEnabled
                || !importer.sRGBTexture
                || importer.filterMode != FilterMode.Bilinear;
            if (requiresReimport)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new InvalidOperationException($"Unity could not import card face texture '{path}'.");
            }

            return texture;
        }

        private static CardSequenceDefinition BuildFinalSequence(NarrativeAssets assets)
        {
            Dictionary<string, StoryFact> f = assets.Facts;
            Dictionary<string, StorySignal> s = assets.Signals;
            Dictionary<string, CardDefinition> c = assets.Cards;
            List<StepSpec> steps = new List<StepSpec>();

            StepSpec opening = Step("s01_opening", c["do_not_draw_opening"], 0.7f);
            opening.EnterSignals.Add(s["begin_opening"]);
            steps.Add(opening);

            StepSpec rear = Step("s02_rear_rule", c["do_not_look_behind_early"], 0.75f);
            rear.RevealSignals.Add(s["reveal_opening_graffiti"]);
            rear.RevealSignals.Add(s["rear_look_rule"]);
            steps.Add(rear);

            StepSpec light = Step("s03_light_rule", c["do_not_turn_off_light"], 0.35f, true);
            light.RevealSignals.Add(s["arm_light_rule"]);
            light.Transitions.Add(Transition("s04_second_door", Condition(f["light_switch_used"])));
            light.Transitions.Add(Transition("s03_light_rule"));
            steps.Add(light);

            StepSpec door = Step("s04_second_door", c["do_not_open_second_door"], 0.35f, true);
            door.RevealSignals.Add(s["arm_second_door_rule"]);
            door.Transitions.Add(Transition("s05_do_not_enter", Condition(f["second_door_opened"])));
            door.Transitions.Add(Transition("s04_second_door"));
            steps.Add(door);

            StepSpec enter = Step("s05_do_not_enter", c["do_not_enter"], 0.35f, true);
            enter.RevealSignals.Add(s["arm_enter_rule"]);
            enter.Transitions.Add(Transition(
                "transition_act_one_to_two",
                Condition(f["entered_second_room"])));
            enter.Transitions.Add(Transition("s05_do_not_enter"));
            steps.Add(enter);

            StepSpec actOneTransition = EventStep("transition_act_one_to_two");
            actOneTransition.ActivationSignals.Add(s["act_one_to_two"]);
            actOneTransition.CompletionDelay = 5.5f;
            actOneTransition.Transitions.Add(Transition("s06_look_at_door"));
            steps.Add(actOneTransition);

            steps.Add(Step("s05a_next_room_card", c["do_not_draw_next_room_card"], 0.7f));

            StepSpec waitReenter = EventStep("s05a_wait_reenter");
            waitReenter.CompletionConditions.Add(Condition(f["entered_second_room"]));
            steps.Add(waitReenter);

            StepSpec lookDoor = Step("s06_look_at_door", c["do_not_look_at_door"], 0.8f, true);
            lookDoor.EnterSignals.Add(s["resolve_room_card_edge"]);
            lookDoor.ActivationSignals.Add(s["mark_enter_card_drawn"]);
            lookDoor.RevealSignals.Add(s["resume_atmosphere"]);
            lookDoor.RevealSignals.Add(s["close_second_door_on_look"]);
            lookDoor.Transitions.Add(Transition(
                "s05a_next_room_card",
                Condition(f["exited_second_room"]),
                Condition(f["enter_card_drawn"], false)));
            steps.Add(lookDoor);

            StepSpec window = Step("s07_window", c["do_not_look_through_window"], 0.35f, true);
            window.RevealSignals.Add(s["arm_window_vision"]);
            window.CompletionConditions.Add(Condition(f["window_silhouette_seen"]));
            steps.Add(window);

            StepSpec already = Step("s08_already_did", c["you_already_did"], 0.4f);
            already.CompleteSignals.Add(s["act_two_to_three"]);
            steps.Add(already);

            StepSpec actTwoTransition = EventStep("transition_act_two_to_three");
            actTwoTransition.CompletionDelay = 5f;
            steps.Add(actTwoTransition);

            StepSpec huntOne = Step("s09_next_card", c["do_not_draw_next_card"], 0.5f);
            huntOne.Mode = CardSequenceStepMode.AutomaticDraw;
            huntOne.RevealSignals.Add(s["start_hunt_far"]);
            steps.Add(huntOne);

            StepSpec huntTwo = Step("s10_do_not_draw", c["do_not_draw_survival"], 0.35f);
            huntTwo.RevealSignals.Add(s["start_hunt_close"]);
            huntTwo.CompleteSignals.Add(s["act_three_to_four"]);
            steps.Add(huntTwo);

            StepSpec actThreeTransition = EventStep("transition_act_three_to_four");
            actThreeTransition.CompletionDelay = 3f;
            steps.Add(actThreeTransition);

            StepSpec turn = Step("s11_turn_around", c["do_not_turn_around"], 0f, true);
            turn.Mode = CardSequenceStepMode.AutomaticDraw;
            turn.RevealSignals.Add(s["start_turn_test"]);
            turn.CompletionConditions.Add(Condition(f["turn_test_resolved"]));
            steps.Add(turn);

            StepSpec result = Step("s12_result", c["good"], 0.8f);
            result.Mode = CardSequenceStepMode.AutomaticDraw;
            VariantSpec sawLook = new VariantSpec { Card = c["i_saw_you_look"] };
            sawLook.Conditions.Add(Condition(f["turned_around"]));
            result.Variants.Add(sawLook);
            steps.Add(result);

            StepSpec touchDoor = Step("s13_touch_door", c["do_not_touch_door"], 4.65f);
            touchDoor.RevealSignals.Add(s["schedule_first_door"]);
            steps.Add(touchDoor);

            StepSpec accusation = Step("s14_accusation", c["why_did_you_open_it"], 0.75f);
            accusation.RevealSignals.Add(s["pause_sensory_beat"]);
            steps.Add(accusation);
            steps.Add(Step("s15_blame", c["do_not_blame_cards"], 0.8f));

            StepSpec shadowTransition = EventStep("transition_act_four_to_five");
            shadowTransition.ActivationSignals.Add(s["swing_shadow"]);
            shadowTransition.CompletionDelay = 1f;
            steps.Add(shadowTransition);

            StepSpec callback = Step("s16_rear_callback", c["do_not_look_behind_door"], 0.8f);
            callback.RevealSignals.Add(s["rear_look_rule"]);
            steps.Add(callback);

            StepSpec sawIt = Step("s17_you_saw_it", c["you_saw_it"], 0.8f);
            sawIt.RevealSignals.Add(s["pause_sensory_beat"]);
            steps.Add(sawIt);

            StepSpec leave = Step("s18_do_not_leave", c["do_not_leave"], 0f, true);
            leave.RevealSignals.Add(s["open_exit"]);
            leave.CompletionConditions.Add(Condition(f["left_room"]));
            steps.Add(leave);

            StepSpec ending = Step("s19_do_not_draw_again", c["do_not_draw_again"], 7f);
            ending.Mode = CardSequenceStepMode.AutomaticDraw;
            ending.ReadyDelay = 0.05f;
            ending.EnterSignals.Add(s["prepare_ending"]);
            ending.RevealSignals.Add(s["show_ending"]);
            TransitionSpec finish = new TransitionSpec { Finish = true };
            ending.Transitions.Add(finish);
            steps.Add(ending);

            CardSequenceDefinition sequence = GetOrCreateAsset<CardSequenceDefinition>(
                $"{SequenceRoot}/FinalFlowSequence.asset");
            SerializedObject serialized = new SerializedObject(sequence);
            Set(serialized, "stableId", "final.sequence.flow_authority");
            Set(serialized, "description", "Authoritative Prototype A implementation of DO_NOT_DRAW_연출상세기획_프로토타입A.txt, including repeat-until-compliance rules, the room-card edge case, and the GOOD/I SAW YOU LOOK branch.");
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

        private static FinalAudioClips LoadFinalAudioClips()
        {
            return new FinalAudioClips
            {
                FluorescentBuzz = LoadRequiredAudioClip("Assets/Sounds/01_fluorescent_buzz_loop.wav"),
                ClockLoop = LoadRequiredAudioClip("Assets/Sounds/02_clock_tick_loop.wav"),
                ClockDesynced = LoadRequiredAudioClip("Assets/Sounds/03_clock_tick_desynced.wav"),
                LowDrone = LoadRequiredAudioClip("Assets/Sounds/04_low_drone_40_60hz_loop.wav"),
                LowStinger = LoadRequiredAudioClip("Assets/Sounds/06_low_stinger.wav"),
                WhiteNoise = LoadRequiredAudioClip("Assets/Sounds/07_white_noise_window_swell.wav"),
                BreathTexture = LoadRequiredAudioClip("Assets/Sounds/08_low_breath_texture_loop.wav"),
                DeckHover = LoadRequiredAudioClip("Assets/Sounds/09_deck_hover_fixed_drone.wav"),
                ImpactThud = LoadRequiredAudioClip("Assets/Sounds/10_impact_thud.wav"),
                CardDraw = LoadRequiredAudioClip("Assets/Sounds/11_card_draw_paper.wav"),
                CardLanding = LoadRequiredAudioClip("Assets/Sounds/Card/Card_Game_Movement_Tap_03.wav"),
                FirstRoomFootstep = LoadRequiredAudioClip("Assets/Sounds/12_carpet_footstep_single.wav"),
                SecondRoomFootstep = LoadRequiredAudioClip("Assets/Sounds/12b_carpet_footstep_room2_pitched.wav"),
                FootstepsBehind = LoadRequiredAudioClip("Assets/Sounds/13_footsteps_behind_4steps_carpet.wav"),
                SwitchOff = LoadRequiredAudioClip("Assets/Sounds/14a_switch_click_off_low.wav"),
                SwitchOn = LoadRequiredAudioClip("Assets/Sounds/14b_switch_click_on_high.wav"),
                DoorHandle = LoadRequiredAudioClip("Assets/Sounds/15_door_handle_turn.wav"),
                DoorCreak = LoadRequiredAudioClip("Assets/Sounds/16a_hinge_creak_door1.wav"),
                StoryDoorCreak = LoadRequiredAudioClip("Assets/Sounds/16b_hinge_creak_door1_selfopen_lowpitch.wav"),
                DoorSlam = LoadRequiredAudioClip(
                    "Assets/Sounds/Horror/freesound_community-door-slam-angrily-86963.mp3"),
                Wind = LoadRequiredAudioClip("Assets/Sounds/17_faint_wind_loop.wav"),
                SilhouetteApproach = LoadRequiredAudioClip("Assets/Sounds/18_silhouette_approach_loop.wav"),
                StoryHandle = LoadRequiredAudioClip("Assets/Sounds/19_door_handle_selfturning_crescendo.wav"),
                FluorescentStarter = LoadRequiredAudioClip("Assets/Sounds/20_fluorescent_starter_tick.wav")
            };
        }

        private static AudioClip LoadRequiredAudioClip(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException($"[Final Experience] Required audio clip was not found: {path}");
            }
            return clip;
        }

        private static Material LoadRequiredMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"[Final Experience] Required material was not found: {path}");
            }
            return material;
        }

        private static Material GetOrCreateBackroomsCarpetMaterial()
        {
            Material source = LoadRequiredMaterial(BackroomsSourceCarpetMaterialPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                BackroomsTintedCarpetMaterialPath);
            if (material == null)
            {
                material = new Material(source);
                material.name = "Backrooms Asset Carpet - Warm Beige";
                AssetDatabase.CreateAsset(material, BackroomsTintedCarpetMaterialPath);
            }
            else
            {
                if (material.shader != source.shader)
                {
                    material.shader = source.shader;
                }

                material.CopyPropertiesFromMaterial(source);
            }

            material.SetColor("_ColorOverlay", new Color(0.78f, 0.7f, 0.5f, 1f));
            material.SetFloat("_ColorOverlayOpacity", 0.62f);
            material.SetFloat("_Normal_Strength", 0.22f);
            material.SetFloat("_Smoothness", 0.11f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Legacy one-time scene construction is intentionally disconnected from every menu and
        // refresh path. ClosedRoom.unity is the authoritative map and must be edited directly.
        [Obsolete("ClosedRoom.unity is scene-authored. Do not regenerate it from narrative data.", true)]
        private static void BuildLegacyScene(Scene scene, NarrativeAssets assets)
        {
            GameObject oldRoot = FindSceneObject(scene, FinalRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }
            GameObject oldSettingsPopup = FindSceneObject(scene, SettingsPopupRootName);
            if (oldSettingsPopup != null)
            {
                UnityEngine.Object.DestroyImmediate(oldSettingsPopup);
            }
            GameObject oldVolumeManager = FindSceneObject(scene, VolumeManagerRootName);
            if (oldVolumeManager != null)
            {
                UnityEngine.Object.DestroyImmediate(oldVolumeManager);
            }

            GameObject originalDesk = FindSceneObject(scene, "Desk - Center");
            GameObject primaryDeckObject = FindSceneObject(scene, "Card Deck System");
            GameObject player = FindSceneObject(scene, "Player - First Person Controller");
            GameObject atmosphereObject = FindSceneObject(scene, "Atmosphere - Horror");
            GameObject promptPanel = FindSceneObject(scene, "Draw Card Prompt");
            Text promptText = FindSceneObject(scene, "Prompt Text")?.GetComponent<Text>();
            GameObject openingGraffiti = FindSceneObject(scene, "DoNotDraw_Wall 1")
                ?? FindSceneObject(scene, "DoNotDraw_Wall");

            if (originalDesk == null || primaryDeckObject == null || player == null || openingGraffiti == null)
            {
                throw new InvalidOperationException(
                    "[Final Experience] Required desk, deck, player, or opening graffiti scene object was not found.");
            }
            FinalAudioClips audio = LoadFinalAudioClips();

            FindSceneObject(scene, "Room Shell")?.SetActive(false);
            FindSceneObject(scene, "North Wall")?.SetActive(false);
            FindSceneObject(scene, "Warm Point Light")?.SetActive(false);
            FindSceneObject(scene, "Warm Bulb")?.SetActive(false);
            FindSceneObject(scene, "Small Ceiling Bulb")?.SetActive(false);
            GameObject root = new GameObject(FinalRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            BuildVolumeManager(scene);
            BuildSettingsPopup(scene);

            ConfigureBackroomsRenderSettings();
            Material wall = GetOrCreateWorldSpaceSurfaceMaterial(
                $"{FinalMaterialRoot}/BackroomsWallpaper.mat",
                BackroomsWallpaperPath,
                new Color(0.9f, 0.92f, 0.86f, 1f),
                0.58f,
                0.025f);
            Material floor = GetOrCreateBackroomsCarpetMaterial();
            Material ceiling = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/BackroomsCeilingTile.mat",
                new Color(0.7f, 0.68f, 0.49f, 1f),
                0f,
                0.08f,
                false);
            Material ceilingGrid = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/BackroomsCeilingGrid.mat",
                new Color(0.39f, 0.4f, 0.34f, 1f),
                0.08f,
                0.12f,
                true);
            Material wallTrim = LoadRequiredMaterial(BackroomsTrimMaterialPath);
            Material wood = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/BackroomsAgedWood.mat",
                new Color(0.23f, 0.15f, 0.075f, 1f),
                0f,
                0.14f,
                false);
            Material doorMaterial = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/DoorDark.mat",
                new Color(0.18f, 0.12f, 0.055f, 1f),
                0.04f,
                0.16f,
                false);
            Material switchMetal = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/SwitchMetal.mat",
                new Color(0.13f, 0.125f, 0.085f, 1f),
                0.58f,
                0.28f,
                false);
            Material brass = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/AgedBrass.mat",
                new Color(0.26f, 0.18f, 0.065f, 1f),
                0.72f,
                0.25f,
                false);
            Material nickel = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/DullNickel.mat",
                new Color(0.25f, 0.25f, 0.18f, 1f),
                0.72f,
                0.3f,
                false);
            Material curtain = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/DustyCurtain.mat",
                new Color(0.22f, 0.18f, 0.11f, 1f),
                0f,
                0.1f,
                false);
            Material silhouetteMaterial = GetOrCreateTransparentMaterial(
                $"{FinalMaterialRoot}/Silhouette.mat",
                new Color(0.0015f, 0.001f, 0.001f, 1f),
                true);
            Material windowMaterial = GetOrCreateMaterial(
                $"{FinalMaterialRoot}/WindowBlack.mat",
                new Color(0.008f, 0.012f, 0.016f, 1f),
                0.15f,
                0.12f,
                true);
            Material windowVisionMaterial = GetOrCreateTransparentMaterial(
                $"{FinalMaterialRoot}/WindowRoomEcho.mat",
                new Color(0.46f, 0.39f, 0.3f, 0.25f),
                true);
            Material whiteLightMaterial = GetOrCreateTransparentMaterial(
                $"{FinalMaterialRoot}/ExitWhiteGlow.mat",
                new Color(1f, 1f, 1f, 0.92f),
                true);
            Material interactionGlow = GetOrCreateInteractionGlowMaterial();

            primaryDeckObject.SetActive(true);
            primaryDeckObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            EditorUtility.SetDirty(primaryDeckObject.transform);

            CardDeckPresenter primaryPresenter = primaryDeckObject.GetComponent<CardDeckPresenter>();
            if (primaryPresenter == null || primaryPresenter.DisplayAnchor == null)
            {
                throw new InvalidOperationException(
                    "The primary card deck requires a presenter with a display anchor.");
            }

            // The deck was turned around for the new player start, so mirror the landing anchor
            // to keep drawn cards moving toward the opposite side of the tabletop.
            Vector3 displayAnchorPosition = primaryPresenter.DisplayAnchor.localPosition;
            displayAnchorPosition.x = -0.75f;
            primaryPresenter.DisplayAnchor.localPosition = displayAnchorPosition;
            EditorUtility.SetDirty(primaryPresenter.DisplayAnchor);

            DetailedRoomSetRefs room = DetailedRoomSetFactory.Build(
                root.transform,
                originalDesk,
                player.transform,
                wall,
                floor,
                ceiling,
                ceilingGrid,
                wallTrim,
                doorMaterial,
                wood,
                brass,
                nickel,
                windowMaterial,
                curtain,
                silhouetteMaterial,
                windowVisionMaterial,
                whiteLightMaterial,
                audio.DoorCreak,
                audio.DoorSlam);
            ConfigureInteractionGlow(room.SecondDoor, interactionGlow);

            CardSequenceRunner runner = primaryDeckObject.GetComponent<CardSequenceRunner>();
            StoryBlackboard blackboard = primaryDeckObject.GetComponent<StoryBlackboard>();
            CardDeckInteraction primaryInteraction = primaryDeckObject.GetComponent<CardDeckInteraction>();
            primaryPresenter.SetVoiceNarrationEnabled(false);
            room.SecondPresenter.SetVoiceNarrationEnabled(false);
            ConfigurePresenterAudio(primaryPresenter, audio.CardDraw, audio.CardLanding);
            ConfigurePresenterAudio(room.SecondPresenter, audio.CardDraw, audio.CardLanding);
            EditorUtility.SetDirty(primaryPresenter);
            EditorUtility.SetDirty(room.SecondPresenter);
            ConfigureDeckInteraction(primaryDeckObject, primaryInteraction, runner, true, false);
            ConfigureDeckInteraction(
                room.SecondInteraction.gameObject,
                room.SecondInteraction,
                runner,
                false,
                true);
            ConfigureInteractionGlow(primaryInteraction, interactionGlow);
            ConfigureInteractionGlow(room.SecondInteraction, interactionGlow);

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
            Set(routerSerialized, "maxDistance", 3.2f);
            Set(routerSerialized, "promptPanel", promptPanel);
            Set(routerSerialized, "promptText", promptText);
            routerSerialized.ApplyModifiedPropertiesWithoutUndo();
            if (promptText != null)
            {
                promptText.fontSize = 24;
                EditorUtility.SetDirty(promptText);
            }

            ConfigureDoor(room.SecondDoor, audio);
            ConfigureDoor(room.StoryDoor, audio);
            HorrorLightSwitchInteractable lightSwitch = BuildLightSwitch(
                root.transform,
                originalDesk.transform,
                switchMetal,
                audio.SwitchOff,
                audio.SwitchOn);
            ConfigureInteractionGlow(lightSwitch, interactionGlow);
            CanvasGroup screenFade = BuildScreenFade(root.transform);

            GameObject directorObject = new GameObject("Closed Room Story Director");
            directorObject.transform.SetParent(root.transform, false);
            AudioSource clockSource = AddAudioSource(directorObject, false);
            AudioSource rearSource = AddAudioSource(directorObject, true);
            AudioSource threatSource = AddAudioSource(directorObject, true);
            AudioSource transitionSource = AddAudioSource(directorObject, false);
            AudioSource windSource = AddAudioSource(directorObject, true);
            AudioSource oneShotSource = AddAudioSource(directorObject, false);
            ClosedRoomStoryDirector director = directorObject.AddComponent<ClosedRoomStoryDirector>();
            Behaviour movement = player.GetComponentsInChildren<Behaviour>(true)
                .FirstOrDefault(component => component.GetType().Name == "FirstPersonController");
            ConfigurePlayerMovement(movement);
            ConfigurePlayerFootsteps(player, audio.FirstRoomFootstep, audio.SecondRoomFootstep);
            AudioSource ambience = atmosphereObject != null ? atmosphereObject.GetComponent<AudioSource>() : null;
            ConfigureLoopSource(ambience, audio.FluorescentBuzz, 0.2f);
            clockSource.volume = 0.18f;

            ConfigureDetailedDirector(
                director,
                assets,
                runner,
                blackboard,
                player.transform,
                playerCamera != null ? playerCamera.transform : player.transform,
                movement,
                primaryPresenter,
                primaryInteraction,
                room,
                lightSwitch,
                screenFade,
                ambience,
                clockSource,
                rearSource,
                threatSource,
                transitionSource,
                windSource,
                oneShotSource,
                audio);
            ConfigureOpeningDiscoveryReveal(
                root,
                player.transform,
                playerCamera != null ? playerCamera.transform : player.transform,
                playerCamera,
                originalDesk.transform,
                lightSwitch.transform,
                primaryDeckObject,
                openingGraffiti,
                assets.Signals["reveal_opening_graffiti"]);
            ConfigureResolutionIndependentUi(scene);
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
            bool enabled,
            bool expandAimTarget)
        {
            BoxCollider collider = deckObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = deckObject.AddComponent<BoxCollider>();
            }
            collider.center = expandAimTarget
                ? new Vector3(0f, 0.12f, 0f)
                : new Vector3(0f, 0.06f, 0f);
            collider.size = expandAimTarget
                ? new Vector3(1.5f, 0.4f, 1.4f)
                : new Vector3(0.85f, 0.25f, 1.12f);

            Transform point = FindChildRecursive(deckObject.transform, "Deck Top - Back Facing Up");
            SerializedObject serialized = new SerializedObject(interaction);
            Set(serialized, "runner", runner);
            Set(serialized, "interactionPoint", point != null ? point : deckObject.transform);
            Set(serialized, "interactionEnabled", enabled);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureOpeningDiscoveryReveal(
            GameObject host,
            Transform player,
            Transform view,
            Camera playerCamera,
            Transform roomCenter,
            Transform switchTarget,
            GameObject deck,
            GameObject graffiti,
            StorySignal graffitiRevealSignal)
        {
            OpeningDiscoveryReveal reveal = host.AddComponent<OpeningDiscoveryReveal>();
            SerializedObject serialized = new SerializedObject(reveal);
            Set(serialized, "playerRoot", player);
            Set(serialized, "viewTransform", view);
            Set(serialized, "playerCamera", playerCamera);
            Set(serialized, "roomCenter", roomCenter);
            Set(serialized, "switchTarget", switchTarget);
            Set(serialized, "deckRoot", deck);
            Set(serialized, "graffitiRoot", graffiti);
            Set(serialized, "graffitiRevealSignal", graffitiRevealSignal);
            SerializedProperty direction = serialized.FindProperty("switchSideDirection");
            if (direction != null)
            {
                direction.vector3Value = Vector3.back;
            }
            SerializedProperty boundsCenter = serialized.FindProperty("deckSpawnBoundsCenter");
            if (boundsCenter != null)
            {
                boundsCenter.vector3Value = new Vector3(0f, 0.12f, 0f);
            }
            SerializedProperty boundsSize = serialized.FindProperty("deckSpawnBoundsSize");
            if (boundsSize != null)
            {
                boundsSize.vector3Value = new Vector3(1.05f, 0.6f, 1.3f);
            }
            Set(serialized, "viewArcDegrees", 120f);
            Set(serialized, "hideTargetsOnAwake", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reveal);

            deck.SetActive(false);
            graffiti.SetActive(false);
            EditorUtility.SetDirty(deck);
            EditorUtility.SetDirty(graffiti);
        }

        private static void ConfigurePresenterAudio(
            CardDeckPresenter presenter,
            AudioClip drawClip,
            AudioClip landingClip)
        {
            SerializedObject serialized = new SerializedObject(presenter);
            Set(serialized, "drawSound", drawClip);
            Set(serialized, "landingSound", landingClip);
            Set(serialized, "drawVolume", 0.48f);
            Set(serialized, "landingVolume", 0.24f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlayerFootsteps(
            GameObject player,
            AudioClip firstRoomClip,
            AudioClip secondRoomClip)
        {
            RandomFootstepPlayer footsteps = player.GetComponentInChildren<RandomFootstepPlayer>(true);
            if (footsteps == null)
            {
                footsteps = player.AddComponent<RandomFootstepPlayer>();
            }

            SerializedObject serialized = new SerializedObject(footsteps);
            SetObjectArray(serialized.FindProperty("footstepClips"), firstRoomClip);
            SetObjectArray(serialized.FindProperty("alternateFootstepClips"), secondRoomClip);
            Set(serialized, "stepDistance", 1.55f);
            Set(serialized, "volume", 0.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(footsteps);
        }

        private static void ConfigureLoopSource(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = Mathf.Clamp01(volume);
            EditorUtility.SetDirty(source);
        }

        private static HorrorLightSwitchInteractable BuildLightSwitch(
            Transform parent,
            Transform roomCenter,
            Material material,
            AudioClip switchOffClip,
            AudioClip switchOnClip)
        {
            if (roomCenter == null)
            {
                throw new InvalidOperationException(
                    "The opening wall switch requires a room-center transform.");
            }

            const float playerRightOffset = -2.25f;
            const float frontWallOffset = -2.29f;
            const float mountingHeight = 1.12f;
            GameObject root = new GameObject("Opening Wall Light Switch");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(
                roomCenter.position
                    + roomCenter.right * playerRightOffset
                    + roomCenter.forward * frontWallOffset
                    + roomCenter.up * mountingHeight,
                roomCenter.rotation * Quaternion.Euler(90f, 0f, 0f));

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
            audio.minDistance = 0.8f;
            audio.maxDistance = 8f;
            HorrorLightSwitchInteractable interactable = root.AddComponent<HorrorLightSwitchInteractable>();
            SerializedObject serialized = new SerializedObject(interactable);
            Set(serialized, "lever", lever.transform);
            Set(serialized, "interactionPoint", point);
            Set(serialized, "interactionEnabled", true);
            Set(serialized, "switchSound", (AudioClip)null);
            Set(serialized, "switchOffSound", switchOffClip);
            Set(serialized, "switchOnSound", switchOnClip);
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
                    "The light switch base or lever intersects the wall mounting plane.");
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
            FinalAudioClips audio)
        {
            SerializedObject serialized = new SerializedObject(door);
            Set(serialized, "openSound", audio.DoorCreak);
            Set(serialized, "storyOpenSound", audio.StoryDoorCreak);
            Set(serialized, "slamDuration", 0.16f);
            Set(serialized, "slamSound", audio.DoorSlam);
            Set(serialized, "handleTurnSound", audio.DoorHandle);
            Set(serialized, "storyHandleTurnSound", audio.StoryHandle);
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
            ResolutionIndependentCanvas.Configure(canvas);
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

        private static void BuildSettingsPopup(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPopupPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"[Final Experience] Setting popup prefab was not found: {SettingsPopupPrefabPath}");
            }

            GameObject popupRoot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (popupRoot == null)
            {
                throw new InvalidOperationException("[Final Experience] Failed to instantiate the setting popup.");
            }

            popupRoot.name = SettingsPopupRootName;
            popupRoot.SetActive(true);

            Canvas canvas = popupRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = popupRoot.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            ResolutionIndependentCanvas.Configure(canvas);

            if (popupRoot.GetComponent<GraphicRaycaster>() == null)
            {
                popupRoot.AddComponent<GraphicRaycaster>();
            }

            SettingPopupManager manager = popupRoot.GetComponent<SettingPopupManager>();
            if (manager == null)
            {
                throw new InvalidOperationException(
                    $"[Final Experience] {SettingsPopupPrefabPath} has no SettingPopupManager component.");
            }

            SerializedObject serialized = new SerializedObject(manager);
            GameObject popup = serialized.FindProperty("popup")?.objectReferenceValue as GameObject;
            if (popup == null)
            {
                throw new InvalidOperationException(
                    $"[Final Experience] {SettingsPopupPrefabPath} has no popup object assigned.");
            }

            manager.ConfigureResponsiveLayout();
            popup.SetActive(false);
            EditorUtility.SetDirty(popupRoot);
            EditorUtility.SetDirty(popup);
        }

        private static void BuildVolumeManager(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VolumeManagerPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"[Final Experience] Volume manager prefab was not found: {VolumeManagerPrefabPath}");
            }

            GameObject managerRoot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (managerRoot == null || managerRoot.GetComponent<VolumeManager>() == null)
            {
                throw new InvalidOperationException(
                    $"[Final Experience] {VolumeManagerPrefabPath} could not provide a VolumeManager component.");
            }

            managerRoot.name = VolumeManagerRootName;
            managerRoot.SetActive(true);
            EditorUtility.SetDirty(managerRoot);
        }

        private static void ConfigureResolutionIndependentUi(Scene scene)
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in sceneRoot.GetComponentsInChildren<Canvas>(true))
                {
                    CanvasScaler scaler = ResolutionIndependentCanvas.Configure(canvas);
                    if (scaler != null)
                    {
                        EditorUtility.SetDirty(scaler);
                    }
                }
            }
        }

        private static void ConfigureDetailedDirector(
            ClosedRoomStoryDirector director,
            NarrativeAssets assets,
            CardSequenceRunner runner,
            StoryBlackboard blackboard,
            Transform player,
            Transform view,
            Behaviour movement,
            CardDeckPresenter primaryPresenter,
            CardDeckInteraction primaryInteraction,
            DetailedRoomSetRefs room,
            HorrorLightSwitchInteractable lightSwitch,
            CanvasGroup screenFade,
            AudioSource ambience,
            AudioSource clockSource,
            AudioSource rearSource,
            AudioSource threatSource,
            AudioSource transitionSource,
            AudioSource windSource,
            AudioSource oneShotSource,
            FinalAudioClips audio)
        {
            SerializedObject serialized = new SerializedObject(director);
            Set(serialized, "runner", runner);
            Set(serialized, "blackboard", blackboard);
            Set(serialized, "playerRoot", player);
            Set(serialized, "playerView", view);
            Set(serialized, "playerCamera", view != null ? view.GetComponent<Camera>() : null);
            Set(serialized, "movementController", movement);
            Set(serialized, "playerStartMarker", room.PlayerStartMarker);
            Set(serialized, "primaryPresenter", primaryPresenter);
            Set(serialized, "primaryInteraction", primaryInteraction);
            Set(serialized, "secondRoomPresenter", room.SecondPresenter);
            Set(serialized, "secondRoomInteraction", room.SecondInteraction);
            Set(serialized, "firstRoomSet", room.FirstRoomSet);
            Set(serialized, "secondRoomSet", room.SecondRoomSet);
            Set(serialized, "lightSwitchRoot", lightSwitch.gameObject);
            Set(serialized, "secondDoorRoot", room.SecondDoor.gameObject);
            Set(serialized, "secondDoorCover", room.SecondDoorCover);
            Set(serialized, "windowVision", room.WindowVision);
            Set(serialized, "endingPortraitSilhouette", room.EndingPortraitSilhouette);
            Set(serialized, "lampLight", room.FirstLamp);
            Set(serialized, "secondRoomLampLight", room.SecondLamp);
            SetObjectArray(
                serialized.FindProperty("ceilingSurfaceRenderers"),
                room.FirstRoomSurfaceRenderer,
                room.SecondRoomSurfaceRenderer);
            Set(serialized, "moonLight", room.MoonLight);
            Set(serialized, "rearDoorRimLight", room.RearRimLight);
            Set(serialized, "firstRoomRimAnchor", room.FirstRearRimAnchor);
            Set(serialized, "secondRoomRimAnchor", room.SecondRearRimAnchor);
            Set(serialized, "silhouetteBacklight", room.SilhouetteBacklight);
            Set(serialized, "exitLight", room.ExitLight);
            Set(serialized, "lightSwitch", lightSwitch);
            Set(serialized, "secondDoor", room.SecondDoor);
            Set(serialized, "storyDoor", room.StoryDoor);
            Set(serialized, "secondRoomZone", room.SecondRoomZone);
            Set(serialized, "returnZone", room.ReturnZone);
            Set(serialized, "endingZone", room.EndingZone);
            Set(serialized, "windowGazeTarget", room.WindowGazeTarget);
            Set(serialized, "threatSilhouette", room.Threat);
            Set(serialized, "threatStart", room.ThreatStart);
            Set(serialized, "threatEnd", room.ThreatEnd);
            Set(serialized, "firstClockHand", room.FirstClockHand);
            Set(serialized, "secondClockHand", room.SecondClockHand);
            Set(serialized, "shadowCaster", room.ShadowCaster);
            Set(serialized, "screenFade", screenFade);
            Set(serialized, "enableClimaxThreat", false);
            Set(serialized, "flickerAmplitude", 0f);
            Set(serialized, "switchResidualDarkeningDuration", 1f);
            Set(serialized, "switchResidualLightMultiplier", 0.48f);
            Set(serialized, "ambientSource", ambience);
            Set(serialized, "clockSource", clockSource);
            Set(serialized, "rearSource", rearSource);
            Set(serialized, "threatSource", threatSource);
            Set(serialized, "transitionSource", transitionSource);
            Set(serialized, "windSource", windSource);
            Set(serialized, "oneShotSource", oneShotSource);
            Set(serialized, "fluorescentPowerClip", audio.FluorescentStarter);
            Set(serialized, "clockLoopClip", audio.ClockLoop);
            Set(serialized, "clockTickClip", audio.ClockDesynced);
            Set(serialized, "floorCreakClip", audio.FirstRoomFootstep);
            Set(serialized, "footstepsBehindClip", audio.FootstepsBehind);
            Set(serialized, "rearImpactClip", audio.ImpactThud);
            Set(serialized, "lowStingerClip", audio.LowStinger);
            Set(serialized, "threatBreathingClip", audio.BreathTexture);
            Set(serialized, "threatDroneClip", audio.LowDrone);
            Set(serialized, "deckHoverClip", audio.DeckHover);
            Set(serialized, "threatApproachClip", audio.SilhouetteApproach);
            Set(serialized, "whiteNoiseClip", audio.WhiteNoise);
            Set(serialized, "windClip", audio.Wind);
            Set(serialized, "lampTickClip", audio.FluorescentStarter);

            Dictionary<string, StoryFact> facts = assets.Facts;
            Set(serialized, "lightSwitchUsedFact", facts["light_switch_used"]);
            Set(serialized, "secondDoorOpenedFact", facts["second_door_opened"]);
            Set(serialized, "enteredSecondRoomFact", facts["entered_second_room"]);
            Set(serialized, "enterCardDrawnFact", facts["enter_card_drawn"]);
            Set(serialized, "exitedSecondRoomFact", facts["exited_second_room"]);
            Set(serialized, "windowVisionSeenFact", facts["window_silhouette_seen"]);
            Set(serialized, "turnedAroundFact", facts["turned_around"]);
            Set(serialized, "turnTestResolvedFact", facts["turn_test_resolved"]);
            Set(serialized, "leftRoomFact", facts["left_room"]);

            (string signal, ClosedRoomCue cue)[] bindings =
            {
                ("begin_opening", ClosedRoomCue.BeginOpening),
                ("pulse_opening_card", ClosedRoomCue.PulseOpeningCard),
                ("rear_look_rule", ClosedRoomCue.StartRearLookRule),
                ("arm_light_rule", ClosedRoomCue.ArmLightRule),
                ("arm_second_door_rule", ClosedRoomCue.ArmSecondDoorRule),
                ("arm_enter_rule", ClosedRoomCue.ArmEnterRule),
                ("mark_enter_card_drawn", ClosedRoomCue.MarkEnterCardDrawn),
                ("resolve_room_card_edge", ClosedRoomCue.ResolveRoomCardEdge),
                ("act_one_to_two", ClosedRoomCue.BeginActOneToTwo),
                ("resume_atmosphere", ClosedRoomCue.ResumeAtmosphere),
                ("close_second_door_on_look", ClosedRoomCue.CloseSecondDoorOnLook),
                ("arm_window_vision", ClosedRoomCue.ArmWindowVision),
                ("pause_sensory_beat", ClosedRoomCue.PauseSensoryBeat),
                ("act_two_to_three", ClosedRoomCue.BeginActTwoToThree),
                ("start_hunt_far", ClosedRoomCue.StartHuntFar),
                ("start_hunt_close", ClosedRoomCue.StartHuntClose),
                ("act_three_to_four", ClosedRoomCue.BeginActThreeToFour),
                ("start_turn_test", ClosedRoomCue.StartTurnAroundTest),
                ("schedule_first_door", ClosedRoomCue.ScheduleFirstDoorOpen),
                ("swing_shadow", ClosedRoomCue.SwingUnnaturalShadow),
                ("open_exit", ClosedRoomCue.OpenExit),
                ("prepare_ending", ClosedRoomCue.PrepareEnding),
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
                ("begin_opening", ClosedRoomCue.BeginOpening),
                ("pulse_opening_card", ClosedRoomCue.PulseOpeningCard),
                ("rear_look_rule", ClosedRoomCue.StartRearLookRule),
                ("arm_light_rule", ClosedRoomCue.ArmLightRule),
                ("arm_second_door_rule", ClosedRoomCue.ArmSecondDoorRule),
                ("arm_enter_rule", ClosedRoomCue.ArmEnterRule),
                ("act_one_to_two", ClosedRoomCue.BeginActOneToTwo),
                ("resume_atmosphere", ClosedRoomCue.ResumeAtmosphere),
                ("close_second_door_on_look", ClosedRoomCue.CloseSecondDoorOnLook),
                ("arm_window_vision", ClosedRoomCue.ArmWindowVision),
                ("pause_sensory_beat", ClosedRoomCue.PauseSensoryBeat),
                ("act_two_to_three", ClosedRoomCue.BeginActTwoToThree),
                ("start_hunt_far", ClosedRoomCue.StartHuntFar),
                ("start_hunt_close", ClosedRoomCue.StartHuntClose),
                ("act_three_to_four", ClosedRoomCue.BeginActThreeToFour),
                ("start_turn_test", ClosedRoomCue.StartTurnAroundTest),
                ("schedule_first_door", ClosedRoomCue.ScheduleFirstDoorOpen),
                ("swing_shadow", ClosedRoomCue.SwingUnnaturalShadow),
                ("open_exit", ClosedRoomCue.OpenExit),
                ("prepare_ending", ClosedRoomCue.PrepareEnding),
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

        private static void ConfigureBackroomsRenderSettings()
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.285f, 0.225f, 1f);
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.31f, 0.22f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.235f, 0.17f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.115f, 0.085f, 1f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.22f;
        }

        private static Material GetOrCreateWorldSpaceSurfaceMaterial(
            string materialPath,
            string texturePath,
            Color tint,
            float worldTiling,
            float smoothness)
        {
            const string shaderName = "DoNotDraw/BackroomsWorldSurface";
            ConfigureTileableTexture(texturePath);

            Shader shader = Shader.Find(shaderName);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (shader == null || albedo == null)
            {
                throw new InvalidOperationException(
                    $"Backrooms surface dependencies are missing: shader '{shaderName}', texture '{texturePath}'.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_WorldTiling", worldTiling);
            material.SetFloat("_BlendSharpness", 8f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_OcclusionStrength", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTileableTexture(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Tileable texture was not found at '{texturePath}'.");
            }

            bool requiresReimport = importer.textureType != TextureImporterType.Default
                || importer.wrapMode != TextureWrapMode.Repeat
                || importer.npotScale != TextureImporterNPOTScale.None
                || !importer.mipmapEnabled
                || !importer.sRGBTexture
                || importer.filterMode != FilterMode.Trilinear
                || importer.anisoLevel != 4;
            if (!requiresReimport)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        private static Material GetOrCreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness,
            bool unlit)
        {
            Shader desiredShader = Shader.Find(
                    unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(desiredShader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != desiredShader)
            {
                material.shader = desiredShader;
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

        private static Material GetOrCreateTexturedMaterial(
            string path,
            Texture2D texture,
            Color tint,
            Vector2 tiling,
            float metallic,
            float smoothness)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null && (importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled))
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
            }
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
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

        private static Material GetOrCreateTransparentMaterial(string path, Color color, bool unlit)
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
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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

            material.SetColor("_GlowColor", new Color(0.72f, 0.78f, 0.82f, 0.62f));
            material.SetFloat("_OutlineWidth", 0.018f);
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

        private static void SetObjectArray(
            SerializedProperty property,
            params UnityEngine.Object[] values)
        {
            if (property == null)
            {
                return;
            }

            property.arraySize = values?.Length ?? 0;
            for (int index = 0; index < property.arraySize; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
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

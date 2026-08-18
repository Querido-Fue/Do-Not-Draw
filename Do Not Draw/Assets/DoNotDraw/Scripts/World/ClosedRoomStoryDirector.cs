using System;
using System.Collections;
using System.Collections.Generic;
using DoNotDraw.Interaction;
using DoNotDraw.Narrative;
using UnityEngine;

namespace DoNotDraw.World
{
    public enum ClosedRoomCue
    {
        StartRearWarning,
        RevealLightSwitch,
        EnableLightSwitchInteraction,
        EnableSecondDoorInteraction,
        MarkEnterCardDrawn,
        SlamSecondDoor,
        ShowWindowSilhouette,
        DarkenForHunt,
        StartHunt,
        SettleAfterHunt,
        StartTurnAroundTest,
        OpenDoorByItself,
        ShowDoorCrackSilhouette,
        OpenExit,
        ShowEnding
    }

    [Serializable]
    public sealed class ClosedRoomCueBinding
    {
        [SerializeField] private StorySignal signal;
        [SerializeField] private ClosedRoomCue cue;

        public StorySignal Signal => signal;
        public ClosedRoomCue Cue => cue;
    }

    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class ClosedRoomStoryDirector : MonoBehaviour
    {
        [Header("Narrative")]
        [SerializeField] private CardSequenceRunner runner;
        [SerializeField] private StoryBlackboard blackboard;
        [SerializeField] private List<ClosedRoomCueBinding> cueBindings = new List<ClosedRoomCueBinding>();

        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform playerView;
        [SerializeField] private Behaviour movementController;

        [Header("Card Stations")]
        [SerializeField] private CardDeckPresenter primaryPresenter;
        [SerializeField] private CardDeckInteraction primaryInteraction;
        [SerializeField] private CardDeckPresenter secondRoomPresenter;
        [SerializeField] private CardDeckInteraction secondRoomInteraction;

        [Header("Room Devices")]
        [SerializeField] private Light ceilingLight;
        [SerializeField] private Light secondRoomLight;
        [SerializeField] private HorrorLightSwitchInteractable lightSwitch;
        [SerializeField] private GameObject lightSwitchRoot;
        [SerializeField] private HorrorDoorInteractable secondDoor;
        [SerializeField] private GameObject secondDoorRoot;
        [SerializeField] private GameObject secondDoorCover;
        [SerializeField] private NarrativeZoneTrigger secondRoomZone;
        [SerializeField] private NarrativeZoneTrigger returnZone;
        [SerializeField] private NarrativeZoneTrigger endingZone;

        [Header("Gaze Targets")]
        [SerializeField] private Transform rearWarningTarget;
        [SerializeField] private Transform windowGazeTarget;
        [SerializeField] private Transform doorCrackGazeTarget;
        [SerializeField, Range(0.5f, 0.999f)] private float rearLookDot = 0.78f;
        [SerializeField, Range(0.5f, 0.999f)] private float focusedLookDot = 0.93f;
        [SerializeField, Range(20f, 170f)] private float turnAroundAngle = 72f;

        [Header("Silhouettes")]
        [SerializeField] private GameObject windowSilhouette;
        [SerializeField] private Transform threatSilhouette;
        [SerializeField] private Transform threatStart;
        [SerializeField] private Transform threatEnd;
        [SerializeField] private GameObject doorCrackSilhouette;
        [SerializeField, Min(1f)] private float threatApproachDuration = 10f;

        [Header("Screen Transitions")]
        [SerializeField] private CanvasGroup screenFade;
        [SerializeField, Min(0f)] private float blackoutChangeDelay = 0.16f;
        [SerializeField, Min(0f)] private float blackoutPostChangeHold = 0.32f;

        [Header("Ending")]
        [SerializeField] private GameObject endingCorridor;
        [SerializeField] private GameObject endingWallMessage;
        [SerializeField, Min(0f)] private float endingHold = 3.5f;
        [SerializeField, Min(0.1f)] private float endingFadeDuration = 2.2f;

        [Header("Audio")]
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource rearSource;
        [SerializeField] private AudioSource threatSource;
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioClip rearWarningClip;
        [SerializeField] private AudioClip threatBreathingClip;
        [SerializeField] private AudioClip silhouetteWhooshClip;
        [SerializeField] private AudioClip threatDroneClip;
        [SerializeField] private AudioClip endingVoiceClip;

        [Header("Facts")]
        [SerializeField] private StoryFact lightSwitchUsedFact;
        [SerializeField] private StoryFact secondDoorOpenedFact;
        [SerializeField] private StoryFact enteredSecondRoomFact;
        [SerializeField] private StoryFact enterCardDrawnFact;
        [SerializeField] private StoryFact exitedSecondRoomFact;
        [SerializeField] private StoryFact windowSilhouetteSeenFact;
        [SerializeField] private StoryFact turnedAroundFact;
        [SerializeField] private StoryFact doorSilhouetteSeenFact;
        [SerializeField] private StoryFact leftRoomFact;

        private readonly List<(StorySignal signal, Action<StorySignalContext> handler)> subscriptions =
            new List<(StorySignal signal, Action<StorySignalContext> handler)>();

        private float initialLightIntensity;
        private float initialSecondLightIntensity;
        private float initialAmbientVolume;
        private bool rearWarningActive;
        private bool windowGazeArmed;
        private bool doorCrackGazeArmed;
        private bool turnTestActive;
        private bool huntActive;
        private bool inSecondRoom;
        private bool endingExitArmed;
        private Vector3 turnReferenceForward;
        private Vector3 windowSilhouetteScale = Vector3.one;
        private Vector3 doorSilhouetteScale = Vector3.one;
        private Coroutine lightRoutine;
        private Coroutine blackoutRoutine;
        private Coroutine threatRoutine;
        private Coroutine silhouetteRoutine;
        private readonly Queue<BlackoutTransitionRequest> blackoutTransitions =
            new Queue<BlackoutTransitionRequest>();
        private bool blackoutActive;
        private bool ceilingLightWasEnabled;
        private bool secondRoomLightWasEnabled;

        private sealed class BlackoutTransitionRequest
        {
            public BlackoutTransitionRequest(Action duringBlackout, Action afterLightsReturn)
            {
                DuringBlackout = duringBlackout;
                AfterLightsReturn = afterLightsReturn;
            }

            public Action DuringBlackout { get; }
            public Action AfterLightsReturn { get; }
        }

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindAnyObjectByType<CardSequenceRunner>();
            }

            blackboard ??= runner != null ? runner.Blackboard : null;
            if (playerView == null && Camera.main != null)
            {
                playerView = Camera.main.transform;
            }

            initialLightIntensity = ceilingLight != null ? ceilingLight.intensity : 1f;
            initialSecondLightIntensity = secondRoomLight != null ? secondRoomLight.intensity : initialLightIntensity;
            initialAmbientVolume = ambientSource != null ? ambientSource.volume : 0f;

            if (windowSilhouette != null)
            {
                windowSilhouetteScale = windowSilhouette.transform.localScale;
                windowSilhouette.SetActive(false);
            }

            if (doorCrackSilhouette != null)
            {
                doorSilhouetteScale = doorCrackSilhouette.transform.localScale;
                doorCrackSilhouette.SetActive(false);
            }

            if (threatSilhouette != null)
            {
                threatSilhouette.gameObject.SetActive(false);
            }

            lightSwitchRoot?.SetActive(false);
            secondDoorRoot?.SetActive(false);
            secondDoorCover?.SetActive(true);
            endingCorridor?.SetActive(false);
            endingWallMessage?.SetActive(false);

            primaryInteraction?.SetInteractionEnabled(true);
            secondRoomInteraction?.SetInteractionEnabled(false);
            returnZone?.SetTriggerEnabled(false);
            endingZone?.SetTriggerEnabled(false);

            if (screenFade != null)
            {
                screenFade.alpha = 0f;
                screenFade.blocksRaycasts = false;
                screenFade.interactable = false;
            }
        }

        private void OnEnable()
        {
            SubscribeToSignals();

            if (runner != null)
            {
                runner.CardDrawStarted += HandleCardDrawStarted;
                runner.CardRevealed += HandleCardRevealed;
            }

            if (lightSwitch != null)
            {
                lightSwitch.Activated += HandleLightSwitchActivated;
            }

            if (secondDoor != null)
            {
                secondDoor.PlayerOpened += HandleSecondDoorOpened;
            }

            SubscribeZone(secondRoomZone);
            SubscribeZone(returnZone);
            SubscribeZone(endingZone);
        }

        private void OnDisable()
        {
            if (blackoutRoutine != null)
            {
                StopCoroutine(blackoutRoutine);
                blackoutRoutine = null;
            }

            blackoutTransitions.Clear();
            RestoreFromBlackout();

            foreach ((StorySignal signal, Action<StorySignalContext> handler) in subscriptions)
            {
                if (signal != null)
                {
                    signal.Raised -= handler;
                }
            }

            subscriptions.Clear();

            if (runner != null)
            {
                runner.CardDrawStarted -= HandleCardDrawStarted;
                runner.CardRevealed -= HandleCardRevealed;
            }

            if (lightSwitch != null)
            {
                lightSwitch.Activated -= HandleLightSwitchActivated;
            }

            if (secondDoor != null)
            {
                secondDoor.PlayerOpened -= HandleSecondDoorOpened;
            }

            UnsubscribeZone(secondRoomZone);
            UnsubscribeZone(returnZone);
            UnsubscribeZone(endingZone);
        }

        private void Update()
        {
            if (rearWarningActive && IsLookingAt(rearWarningTarget, rearLookDot))
            {
                StopRearWarning();
            }

            if (windowGazeArmed && IsLookingAt(windowGazeTarget, focusedLookDot))
            {
                windowGazeArmed = false;
                SetFact(windowSilhouetteSeenFact, true);
                PlayOneShot(silhouetteWhooshClip, 0.68f);
                HideSilhouette(windowSilhouette);
            }

            if (doorCrackGazeArmed && IsLookingAt(doorCrackGazeTarget, focusedLookDot))
            {
                doorCrackGazeArmed = false;
                SetFact(doorSilhouetteSeenFact, true);
                PlayOneShot(silhouetteWhooshClip, 0.72f);
                QueueBlackoutTransition(
                    () => HideSilhouette(doorCrackSilhouette),
                    () => runner?.RequestExternalAdvance());
            }

            if (turnTestActive && HasTurnedAround())
            {
                SetFact(turnedAroundFact, true);
            }
        }

        private void SubscribeToSignals()
        {
            subscriptions.Clear();
            foreach (ClosedRoomCueBinding binding in cueBindings)
            {
                if (binding?.Signal == null)
                {
                    continue;
                }

                ClosedRoomCue cue = binding.Cue;
                Action<StorySignalContext> handler = _ => HandleCue(cue);
                binding.Signal.Raised += handler;
                subscriptions.Add((binding.Signal, handler));
            }
        }

        private void HandleCue(ClosedRoomCue cue)
        {
            switch (cue)
            {
                case ClosedRoomCue.StartRearWarning:
                    StartRearWarning();
                    break;
                case ClosedRoomCue.RevealLightSwitch:
                    RevealLightSwitch();
                    break;
                case ClosedRoomCue.EnableLightSwitchInteraction:
                    lightSwitch?.SetInteractionEnabled(true);
                    break;
                case ClosedRoomCue.EnableSecondDoorInteraction:
                    secondDoor?.SetInteractionEnabled(true);
                    break;
                case ClosedRoomCue.MarkEnterCardDrawn:
                    SetFact(enterCardDrawnFact, true);
                    break;
                case ClosedRoomCue.SlamSecondDoor:
                    secondDoor?.SetInteractionEnabled(false);
                    secondDoor?.CloseWithSlam();
                    break;
                case ClosedRoomCue.ShowWindowSilhouette:
                    ShowWindowSilhouette();
                    break;
                case ClosedRoomCue.DarkenForHunt:
                    DarkenForHunt();
                    break;
                case ClosedRoomCue.StartHunt:
                    StartHunt();
                    break;
                case ClosedRoomCue.SettleAfterHunt:
                    SettleAfterHunt();
                    break;
                case ClosedRoomCue.StartTurnAroundTest:
                    StartTurnAroundTest();
                    break;
                case ClosedRoomCue.OpenDoorByItself:
                    secondDoor?.OpenPartially();
                    break;
                case ClosedRoomCue.ShowDoorCrackSilhouette:
                    ShowDoorCrackSilhouette();
                    break;
                case ClosedRoomCue.OpenExit:
                    OpenExit();
                    break;
                case ClosedRoomCue.ShowEnding:
                    StartCoroutine(EndingRoutine());
                    break;
            }
        }

        private void StartRearWarning()
        {
            rearWarningActive = true;
            if (rearSource == null || rearWarningClip == null)
            {
                return;
            }

            if (rearWarningTarget != null)
            {
                rearSource.transform.position = rearWarningTarget.position;
            }

            rearSource.clip = rearWarningClip;
            rearSource.loop = true;
            rearSource.spatialBlend = 1f;
            rearSource.volume = 0.48f;
            rearSource.Play();
        }

        private void StopRearWarning()
        {
            rearWarningActive = false;
            if (rearSource != null)
            {
                rearSource.Stop();
            }
        }

        private void RevealLightSwitch()
        {
            StopRearWarning();
            QueueBlackoutTransition(() =>
            {
                lightSwitchRoot?.SetActive(true);
                lightSwitch?.SetInteractionEnabled(false);
            });
        }

        private void HandleLightSwitchActivated(HorrorLightSwitchInteractable source)
        {
            StartCoroutine(LightSwitchCycleRoutine());
        }

        private IEnumerator LightSwitchCycleRoutine()
        {
            if (ceilingLight != null)
            {
                ceilingLight.enabled = false;
            }
            if (secondRoomLight != null)
            {
                secondRoomLight.enabled = false;
            }

            yield return new WaitForSeconds(1.15f);

            if (ceilingLight != null)
            {
                ceilingLight.enabled = true;
                ceilingLight.intensity = initialLightIntensity;
            }
            if (secondRoomLight != null)
            {
                secondRoomLight.enabled = true;
                secondRoomLight.intensity = initialSecondLightIntensity;
            }

            secondDoorCover?.SetActive(false);
            secondDoorRoot?.SetActive(true);
            secondDoor?.SnapClosed();
            secondDoor?.SetInteractionEnabled(false);
            SetFact(lightSwitchUsedFact, true);
            runner?.RequestExternalAdvance();
        }

        private void HandleSecondDoorOpened(HorrorDoorInteractable source)
        {
            SetFact(secondDoorOpenedFact, true);
            runner?.RequestExternalAdvance();
        }

        private void HandleZoneEntered(NarrativeZoneId zoneId)
        {
            switch (zoneId)
            {
                case NarrativeZoneId.SecondRoom:
                    EnterSecondRoom();
                    break;
                case NarrativeZoneId.ReturnedToFirstRoom:
                    ReturnToFirstRoom();
                    break;
                case NarrativeZoneId.EndingCorridor:
                    EnterEndingCorridor();
                    break;
            }
        }

        private void EnterSecondRoom()
        {
            if (inSecondRoom)
            {
                return;
            }

            inSecondRoom = true;
            primaryInteraction?.SetInteractionEnabled(false);
            secondRoomInteraction?.SetInteractionEnabled(true);
            if (runner != null && secondRoomPresenter != null)
            {
                runner.SetPresenter(secondRoomPresenter, true);
            }

            returnZone?.SetTriggerEnabled(true);
            SetFact(exitedSecondRoomFact, false);
            SetFact(enteredSecondRoomFact, true);
            runner?.RequestExternalAdvance();
        }

        private void ReturnToFirstRoom()
        {
            if (!inSecondRoom || endingExitArmed)
            {
                return;
            }

            inSecondRoom = false;
            secondRoomInteraction?.SetInteractionEnabled(false);
            primaryInteraction?.SetInteractionEnabled(true);
            if (runner != null && primaryPresenter != null)
            {
                runner.SetPresenter(primaryPresenter, true);
            }

            SetFact(enteredSecondRoomFact, false);
            SetFact(exitedSecondRoomFact, true);
            if (!GetBool(enterCardDrawnFact))
            {
                runner?.RequestExternalAdvance();
            }
        }

        private void EnterEndingCorridor()
        {
            if (!endingExitArmed)
            {
                return;
            }

            endingZone?.SetTriggerEnabled(false);
            SetFact(leftRoomFact, true);
            runner?.RequestExternalAdvance();
        }

        private void ShowWindowSilhouette()
        {
            if (GetBool(windowSilhouetteSeenFact))
            {
                HideSilhouette(windowSilhouette);
                return;
            }

            windowGazeArmed = true;
            ShowSilhouette(windowSilhouette, windowSilhouetteScale);
        }

        private void DarkenForHunt()
        {
            StartLightFade(initialLightIntensity * 0.42f, 2.8f);
            if (ambientSource != null)
            {
                ambientSource.volume = initialAmbientVolume * 0.3f;
            }

            if (oneShotSource != null && threatDroneClip != null)
            {
                oneShotSource.PlayOneShot(threatDroneClip, 0.22f);
            }
        }

        private void StartHunt()
        {
            if (threatSilhouette == null || threatStart == null || threatEnd == null)
            {
                return;
            }

            StopThreatRoutine();
            huntActive = true;
            threatSilhouette.position = threatStart.position;
            threatSilhouette.rotation = threatStart.rotation;
            threatSilhouette.gameObject.SetActive(true);
            threatRoutine = StartCoroutine(ThreatApproachRoutine());

            if (threatSource != null && threatBreathingClip != null)
            {
                threatSource.transform.position = threatSilhouette.position;
                threatSource.clip = threatBreathingClip;
                threatSource.loop = true;
                threatSource.spatialBlend = 1f;
                threatSource.volume = 0.16f;
                threatSource.Play();
            }
        }

        private IEnumerator ThreatApproachRoutine()
        {
            float elapsed = 0f;
            Vector3 start = threatStart.position;
            Vector3 target = threatEnd.position;
            while (elapsed < threatApproachDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / threatApproachDuration);
                t = t * t;
                threatSilhouette.position = Vector3.Lerp(start, target, t);
                if (threatSource != null)
                {
                    threatSource.transform.position = threatSilhouette.position;
                    threatSource.volume = Mathf.Lerp(0.14f, 0.62f, t);
                }

                yield return null;
            }

            threatRoutine = null;
        }

        private void HandleCardDrawStarted(CardDefinition card, int drawIndex)
        {
            if (rearWarningActive)
            {
                StopRearWarning();
            }

            if (huntActive)
            {
                StopThreatRoutine();
            }

            if (turnTestActive)
            {
                turnTestActive = false;
                if (rearSource != null)
                {
                    rearSource.Stop();
                }
            }
        }

        private void HandleCardRevealed(CardDefinition card, GameObject cardObject, int drawIndex)
        {
            if (huntActive)
            {
                DismissThreat();
            }
        }

        private void DismissThreat()
        {
            huntActive = false;
            StopThreatRoutine();
            if (threatSilhouette != null)
            {
                threatSilhouette.gameObject.SetActive(false);
            }

            if (threatSource != null)
            {
                threatSource.Stop();
            }
        }

        private void SettleAfterHunt()
        {
            DismissThreat();
            StartLightFade(initialLightIntensity * 0.68f, 1.6f);
            if (rearSource != null && threatBreathingClip != null)
            {
                rearSource.clip = threatBreathingClip;
                rearSource.loop = false;
                rearSource.spatialBlend = 1f;
                rearSource.volume = 0.2f;
                rearSource.Play();
            }
        }

        private void StartTurnAroundTest()
        {
            turnTestActive = true;
            SetFact(turnedAroundFact, false);
            turnReferenceForward = HorizontalDirection(playerView != null ? playerView.forward : transform.forward);
            StartRearWarning();
            rearWarningActive = false;
        }

        private bool HasTurnedAround()
        {
            if (playerView == null)
            {
                return false;
            }

            Vector3 currentForward = HorizontalDirection(playerView.forward);
            return Vector3.Angle(turnReferenceForward, currentForward) >= turnAroundAngle;
        }

        private void ShowDoorCrackSilhouette()
        {
            if (GetBool(doorSilhouetteSeenFact))
            {
                HideSilhouette(doorCrackSilhouette);
                return;
            }

            secondDoor?.OpenPartially();
            doorCrackGazeArmed = true;
            ShowSilhouette(doorCrackSilhouette, doorSilhouetteScale);
        }

        private void OpenExit()
        {
            endingExitArmed = true;
            returnZone?.SetTriggerEnabled(false);
            endingCorridor?.SetActive(true);
            endingZone?.SetTriggerEnabled(true);
            secondDoor?.OpenByStory();
        }

        private IEnumerator EndingRoutine()
        {
            endingWallMessage?.SetActive(true);
            PlayOneShot(endingVoiceClip, 0.82f);
            yield return new WaitForSeconds(endingHold);

            if (movementController != null)
            {
                movementController.enabled = false;
            }

            if (screenFade == null)
            {
                yield break;
            }

            screenFade.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < endingFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                screenFade.alpha = Mathf.Clamp01(elapsed / endingFadeDuration);
                yield return null;
            }

            screenFade.alpha = 1f;
        }

        private bool IsLookingAt(Transform target, float dotThreshold)
        {
            if (playerView == null || target == null)
            {
                return false;
            }

            Vector3 direction = target.position - playerView.position;
            if (direction.sqrMagnitude < 0.001f)
            {
                return true;
            }

            return Vector3.Dot(playerView.forward, direction.normalized) >= dotThreshold;
        }

        private void ShowSilhouette(GameObject silhouette, Vector3 targetScale)
        {
            if (silhouette == null)
            {
                return;
            }

            if (silhouetteRoutine != null)
            {
                StopCoroutine(silhouetteRoutine);
            }

            silhouetteRoutine = StartCoroutine(ScaleSilhouetteRoutine(silhouette, Vector3.zero, targetScale, true));
        }

        private void HideSilhouette(GameObject silhouette)
        {
            if (silhouette == null)
            {
                return;
            }

            silhouette.SetActive(false);
        }

        private IEnumerator ScaleSilhouetteRoutine(
            GameObject silhouette,
            Vector3 start,
            Vector3 target,
            bool activate)
        {
            silhouette.SetActive(activate);
            silhouette.transform.localScale = start;
            float elapsed = 0f;
            const float duration = 1.2f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                silhouette.transform.localScale = Vector3.Lerp(start, target, t * t * (3f - 2f * t));
                yield return null;
            }

            silhouette.transform.localScale = target;
            silhouetteRoutine = null;
        }

        private void QueueBlackoutTransition(Action duringBlackout, Action afterLightsReturn = null)
        {
            blackoutTransitions.Enqueue(new BlackoutTransitionRequest(duringBlackout, afterLightsReturn));
            if (blackoutRoutine == null)
            {
                blackoutRoutine = StartCoroutine(BlackoutTransitionRoutine());
            }
        }

        private IEnumerator BlackoutTransitionRoutine()
        {
            while (blackoutTransitions.Count > 0)
            {
                BlackoutTransitionRequest request = blackoutTransitions.Dequeue();
                EnterBlackout();

                // Keep at least one fully rendered black frame before mutating the scene.
                yield return null;
                if (blackoutChangeDelay > 0f)
                {
                    yield return new WaitForSecondsRealtime(blackoutChangeDelay);
                }

                try
                {
                    request.DuringBlackout?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                if (blackoutPostChangeHold > 0f)
                {
                    yield return new WaitForSecondsRealtime(blackoutPostChangeHold);
                }

                RestoreFromBlackout();
                try
                {
                    request.AfterLightsReturn?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                // Ensure the restored room is rendered before another queued blackout begins.
                yield return null;
            }

            blackoutRoutine = null;
        }

        private void EnterBlackout()
        {
            if (lightRoutine != null)
            {
                StopCoroutine(lightRoutine);
                lightRoutine = null;
            }

            blackoutActive = true;
            ceilingLightWasEnabled = ceilingLight != null && ceilingLight.enabled;
            secondRoomLightWasEnabled = secondRoomLight != null && secondRoomLight.enabled;
            if (ceilingLight != null)
            {
                ceilingLight.enabled = false;
            }

            if (secondRoomLight != null)
            {
                secondRoomLight.enabled = false;
            }

            if (screenFade != null)
            {
                screenFade.alpha = 1f;
                screenFade.blocksRaycasts = true;
                screenFade.interactable = false;
            }
        }

        private void RestoreFromBlackout()
        {
            if (!blackoutActive)
            {
                return;
            }

            if (ceilingLight != null)
            {
                ceilingLight.enabled = ceilingLightWasEnabled;
            }

            if (secondRoomLight != null)
            {
                secondRoomLight.enabled = secondRoomLightWasEnabled;
            }

            if (screenFade != null)
            {
                screenFade.alpha = 0f;
                screenFade.blocksRaycasts = false;
                screenFade.interactable = false;
            }

            blackoutActive = false;
        }

        private void StartLightFade(float targetIntensity, float duration)
        {
            if (ceilingLight == null)
            {
                return;
            }

            if (lightRoutine != null)
            {
                StopCoroutine(lightRoutine);
            }

            lightRoutine = StartCoroutine(LightFadeRoutine(targetIntensity, duration));
        }

        private IEnumerator LightFadeRoutine(float targetIntensity, float duration)
        {
            ceilingLight.enabled = true;
            float start = ceilingLight.intensity;
            float secondStart = secondRoomLight != null ? secondRoomLight.intensity : start;
            float secondTarget = initialLightIntensity > 0.001f
                ? targetIntensity / initialLightIntensity * initialSecondLightIntensity
                : targetIntensity;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ceilingLight.intensity = Mathf.Lerp(start, targetIntensity, t);
                if (secondRoomLight != null)
                {
                    secondRoomLight.enabled = true;
                    secondRoomLight.intensity = Mathf.Lerp(secondStart, secondTarget, t);
                }
                yield return null;
            }

            ceilingLight.intensity = targetIntensity;
            if (secondRoomLight != null)
            {
                secondRoomLight.intensity = secondTarget;
            }
            lightRoutine = null;
        }

        private void StopThreatRoutine()
        {
            if (threatRoutine != null)
            {
                StopCoroutine(threatRoutine);
                threatRoutine = null;
            }
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (oneShotSource != null && clip != null)
            {
                oneShotSource.PlayOneShot(clip, volume);
            }
        }

        private void SetFact(StoryFact fact, bool value)
        {
            if (blackboard != null && fact != null)
            {
                blackboard.SetBool(fact, value);
            }
        }

        private bool GetBool(StoryFact fact)
        {
            return blackboard != null && fact != null && blackboard.GetValue(fact).BoolValue;
        }

        private void SubscribeZone(NarrativeZoneTrigger zone)
        {
            if (zone != null)
            {
                zone.PlayerEntered += HandleZoneEntered;
            }
        }

        private void UnsubscribeZone(NarrativeZoneTrigger zone)
        {
            if (zone != null)
            {
                zone.PlayerEntered -= HandleZoneEntered;
            }
        }

        private static Vector3 HorizontalDirection(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.001f ? value.normalized : Vector3.forward;
        }

        private void OnValidate()
        {
            cueBindings ??= new List<ClosedRoomCueBinding>();
            rearLookDot = Mathf.Clamp(rearLookDot, 0.5f, 0.999f);
            focusedLookDot = Mathf.Clamp(focusedLookDot, 0.5f, 0.999f);
            turnAroundAngle = Mathf.Clamp(turnAroundAngle, 20f, 170f);
            threatApproachDuration = Mathf.Max(1f, threatApproachDuration);
            blackoutChangeDelay = Mathf.Max(0f, blackoutChangeDelay);
            blackoutPostChangeHold = Mathf.Max(0f, blackoutPostChangeHold);
            endingHold = Mathf.Max(0f, endingHold);
            endingFadeDuration = Mathf.Max(0.1f, endingFadeDuration);
        }
    }
}

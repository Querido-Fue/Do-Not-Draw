using System;
using System.Collections;
using System.Collections.Generic;
using DoNotDraw.Audio;
using DoNotDraw.Interaction;
using DoNotDraw.Narrative;
using UnityEngine;

namespace DoNotDraw.World
{
    public enum ClosedRoomCue
    {
        BeginOpening,
        PulseOpeningCard,
        StartRearLookRule,
        ArmLightRule,
        ArmSecondDoorRule,
        ArmEnterRule,
        MarkEnterCardDrawn,
        ResolveRoomCardEdge,
        BeginActOneToTwo,
        ResumeAtmosphere,
        ArmWindowVision,
        PauseSensoryBeat,
        BeginActTwoToThree,
        StartHuntFar,
        StartHuntClose,
        BeginActThreeToFour,
        StartTurnAroundTest,
        ScheduleFirstDoorOpen,
        SwingUnnaturalShadow,
        OpenExit,
        PrepareEnding,
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
        private const float EndingDurationSeconds = 5f;
        private const float EndingFadeSeconds = 1f;

        [Header("Narrative")]
        [SerializeField] private CardSequenceRunner runner;
        [SerializeField] private StoryBlackboard blackboard;
        [SerializeField] private List<ClosedRoomCueBinding> cueBindings = new List<ClosedRoomCueBinding>();

        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform playerView;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Behaviour movementController;
        [SerializeField] private Transform playerStartMarker;

        [Header("Card Stations")]
        [SerializeField] private CardDeckPresenter primaryPresenter;
        [SerializeField] private CardDeckInteraction primaryInteraction;
        [SerializeField] private CardDeckPresenter secondRoomPresenter;
        [SerializeField] private CardDeckInteraction secondRoomInteraction;

        [Header("Room Sets")]
        [SerializeField] private GameObject firstRoomSet;
        [SerializeField] private GameObject secondRoomSet;
        [SerializeField] private GameObject lightSwitchRoot;
        [SerializeField] private GameObject secondDoorRoot;
        [SerializeField] private GameObject secondDoorCover;
        [SerializeField] private GameObject windowVision;
        [SerializeField] private GameObject endingPortraitSilhouette;

        [Header("Lights")]
        [SerializeField] private Light lampLight;
        [SerializeField] private Light secondRoomLampLight;
        [SerializeField] private Light moonLight;
        [SerializeField] private Light rearDoorRimLight;
        [SerializeField] private Transform firstRoomRimAnchor;
        [SerializeField] private Transform secondRoomRimAnchor;
        [SerializeField] private Light silhouetteBacklight;
        [SerializeField] private Light exitLight;
        [SerializeField, Range(0f, 0.05f)] private float flickerAmplitude;

        [Header("Doors And Switch")]
        [SerializeField] private HorrorLightSwitchInteractable lightSwitch;
        [SerializeField] private HorrorDoorInteractable secondDoor;
        [SerializeField] private HorrorDoorInteractable storyDoor;
        [SerializeField] private NarrativeZoneTrigger secondRoomZone;
        [SerializeField] private NarrativeZoneTrigger returnZone;
        [SerializeField] private NarrativeZoneTrigger endingZone;

        [Header("Gaze And Silhouette")]
        [SerializeField] private Transform windowGazeTarget;
        [SerializeField] private Transform threatSilhouette;
        [SerializeField] private Transform threatStart;
        [SerializeField] private Transform threatEnd;
        [SerializeField, Min(1f)] private float threatApproachDuration = 10f;
        [SerializeField, Range(0.5f, 0.999f)] private float focusedLookDot = 0.93f;
        [SerializeField, Min(0.1f)] private float windowGazeDuration = 1f;
        [SerializeField, Range(90f, 179f)] private float rearImpactAngle = 150f;

        [Header("Environment Animation")]
        [SerializeField] private Transform firstClockHand;
        [SerializeField] private Transform secondClockHand;
        [SerializeField] private Transform shadowCaster;

        [Header("Screen")]
        [SerializeField] private CanvasGroup screenFade;
        [SerializeField] private bool enableClimaxThreat;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource clockSource;
        [SerializeField] private AudioSource rearSource;
        [SerializeField] private AudioSource threatSource;
        [SerializeField] private AudioSource transitionSource;
        [SerializeField] private AudioSource windSource;
        [SerializeField] private AudioSource oneShotSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip fluorescentPowerClip;
        [SerializeField] private AudioClip clockLoopClip;
        [SerializeField] private AudioClip clockTickClip;
        [SerializeField] private AudioClip floorCreakClip;
        [SerializeField] private AudioClip footstepsBehindClip;
        [SerializeField] private AudioClip rearImpactClip;
        [SerializeField] private AudioClip lowStingerClip;
        [SerializeField] private AudioClip threatBreathingClip;
        [SerializeField] private AudioClip threatDroneClip;
        [SerializeField] private AudioClip deckHoverClip;
        [SerializeField] private AudioClip threatApproachClip;
        [SerializeField] private AudioClip whiteNoiseClip;
        [SerializeField] private AudioClip windClip;
        [SerializeField] private AudioClip lampTickClip;

        [Header("Facts")]
        [SerializeField] private StoryFact lightSwitchUsedFact;
        [SerializeField] private StoryFact secondDoorOpenedFact;
        [SerializeField] private StoryFact enteredSecondRoomFact;
        [SerializeField] private StoryFact enterCardDrawnFact;
        [SerializeField] private StoryFact exitedSecondRoomFact;
        [SerializeField] private StoryFact windowVisionSeenFact;
        [SerializeField] private StoryFact turnedAroundFact;
        [SerializeField] private StoryFact turnTestResolvedFact;
        [SerializeField] private StoryFact leftRoomFact;

        private readonly List<(StorySignal signal, Action<StorySignalContext> handler)> subscriptions =
            new List<(StorySignal signal, Action<StorySignalContext> handler)>();
        private readonly List<Renderer> threatRenderers = new List<Renderer>();

        private RandomFootstepPlayer playerFootsteps;
        private float initialLampIntensity;
        private float initialSecondLampIntensity;
        private float primaryLightBase;
        private float secondLightBase;
        private float initialAmbientVolume;
        private float initialCameraFov;
        private float initialLampColorTemperature;
        private float initialSecondLampColorTemperature;
        private Color initialLampColor;
        private Color initialSecondLampColor;
        private Vector3 baseViewLocalPosition;
        private Quaternion initialViewLocalRotation;
        private Vector3 lookReferenceForward;
        private Vector3 turnReferenceForward;
        private Vector3 threatBaseScale = Vector3.one;
        private Quaternion shadowInitialRotation;
        private float flickerDipMultiplier = 1f;
        private float nextClockTick;
        private float nextFloorCreak;
        private float windowGazeElapsed;
        private float threatProgress;
        private float scriptedShake;
        private bool inSecondRoom;
        private bool microFlickerPaused;
        private bool sensoryFrozen;
        private bool lookRuleActive;
        private bool rearImpactTriggered;
        private bool windowVisionArmed;
        private bool huntActive;
        private bool huntHovering;
        private bool turnTestActive;
        private bool turnViolationTriggered;
        private bool endingExitArmed;
        private bool endingActive;
        private bool lightRuleBlackoutActive;
        private int lightRuleRevealCount;
        private int secondDoorRuleRevealCount;
        private int enterRuleRevealCount;
        private float pendingCardDipMinimum = 0.84f;
        private Coroutine lightFadeRoutine;
        private Coroutine dipRoutine;
        private Coroutine threatFadeRoutine;
        private Coroutine sensoryRoutine;
        private Coroutine turnRoutine;
        private Coroutine endingRoutine;
        private Coroutine presenterSwitchRoutine;

        private CardDeckInteraction ActiveDeckInteraction => inSecondRoom
            ? secondRoomInteraction
            : primaryInteraction;

        private void Awake()
        {
            runner ??= FindAnyObjectByType<CardSequenceRunner>();
            blackboard ??= runner != null ? runner.Blackboard : null;
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
            if (playerView == null && playerCamera != null)
            {
                playerView = playerCamera.transform;
            }
            playerFootsteps = playerRoot != null
                ? playerRoot.GetComponentInChildren<RandomFootstepPlayer>(true)
                : FindAnyObjectByType<RandomFootstepPlayer>(FindObjectsInactive.Include);

            initialLampIntensity = lampLight != null ? lampLight.intensity : 1f;
            initialSecondLampIntensity = secondRoomLampLight != null
                ? secondRoomLampLight.intensity
                : initialLampIntensity * 0.9f;
            primaryLightBase = initialLampIntensity;
            secondLightBase = initialSecondLampIntensity;
            initialLampColor = lampLight != null ? lampLight.color : new Color(1f, 0.73f, 0.46f);
            initialSecondLampColor = secondRoomLampLight != null
                ? secondRoomLampLight.color
                : initialLampColor;
            initialLampColorTemperature = lampLight != null ? lampLight.colorTemperature : 4200f;
            initialSecondLampColorTemperature = secondRoomLampLight != null
                ? secondRoomLampLight.colorTemperature
                : initialLampColorTemperature;
            initialAmbientVolume = ambientSource != null ? ambientSource.volume : 0f;
            initialCameraFov = playerCamera != null ? playerCamera.fieldOfView : 60f;
            baseViewLocalPosition = playerView != null ? playerView.localPosition : Vector3.zero;
            initialViewLocalRotation = playerView != null ? playerView.localRotation : Quaternion.identity;
            threatBaseScale = threatSilhouette != null ? threatSilhouette.localScale : Vector3.one;
            shadowInitialRotation = shadowCaster != null ? shadowCaster.localRotation : Quaternion.identity;

            CacheThreatMaterials();
            playerFootsteps?.SetAlternateSurface(false);
            ResetSceneState();
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
                lightSwitch.StateChanged += HandleLightSwitchStateChanged;
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
                lightSwitch.StateChanged -= HandleLightSwitchStateChanged;
            }
            if (secondDoor != null)
            {
                secondDoor.PlayerOpened -= HandleSecondDoorOpened;
            }
            UnsubscribeZone(secondRoomZone);
            UnsubscribeZone(returnZone);
            UnsubscribeZone(endingZone);
            if (playerView != null)
            {
                playerView.localPosition = baseViewLocalPosition;
            }
            if (playerCamera != null)
            {
                playerCamera.fieldOfView = initialCameraFov;
            }
            presenterSwitchRoutine = null;
        }

        private void Update()
        {
            UpdateAmbientDetails();
            UpdateLampFlicker();
            UpdateRearLookRule();
            UpdateWindowGaze();
            UpdateThreat();
            UpdateTurnTest();
            UpdateExitCamera();
        }

        private void LateUpdate()
        {
            if (playerView == null)
            {
                return;
            }
            float hoverScale = huntHovering ? 0.5f : 1f;
            float huntShake = huntActive ? 0.0035f * hoverScale : 0f;
            float idle = endingActive || sensoryFrozen ? 0f : Mathf.Sin(Time.unscaledTime * 1.15f) * 0.0022f;
            Vector3 random = UnityEngine.Random.insideUnitSphere * (scriptedShake + huntShake);
            random.z *= 0.3f;
            playerView.localPosition = baseViewLocalPosition + new Vector3(0f, idle, 0f) + random;
        }

        private void ResetSceneState()
        {
            inSecondRoom = false;
            endingActive = false;
            endingExitArmed = false;
            lookRuleActive = false;
            windowVisionArmed = false;
            huntActive = false;
            turnTestActive = false;
            sensoryFrozen = false;
            microFlickerPaused = false;
            lightRuleBlackoutActive = false;
            lightRuleRevealCount = 0;
            secondDoorRuleRevealCount = 0;
            enterRuleRevealCount = 0;
            pendingCardDipMinimum = 0.84f;

            firstRoomSet?.SetActive(true);
            secondRoomSet?.SetActive(true);
            lightSwitchRoot?.SetActive(false);
            lightSwitch?.ResetSwitch(true);
            lightSwitch?.SetInteractionEnabled(false);
            secondDoorRoot?.SetActive(false);
            secondDoorCover?.SetActive(true);
            secondDoor?.SnapClosed();
            secondDoor?.SetInteractionEnabled(false);
            storyDoor?.SnapClosed();
            storyDoor?.SetInteractionEnabled(false);
            secondRoomZone?.SetTriggerEnabled(true);
            returnZone?.SetTriggerEnabled(false);
            endingZone?.SetTriggerEnabled(false);

            windowVision?.SetActive(false);
            endingPortraitSilhouette?.SetActive(false);
            if (threatSilhouette != null)
            {
                threatSilhouette.gameObject.SetActive(false);
                threatSilhouette.localScale = threatBaseScale;
            }

            SetLightEnabled(lampLight, true);
            SetLightEnabled(secondRoomLampLight, true);
            SetLightEnabled(moonLight, false);
            SetLightEnabled(rearDoorRimLight, false);
            SetLightEnabled(silhouetteBacklight, false);
            SetLightEnabled(exitLight, false);
            if (lampLight != null)
            {
                lampLight.color = initialLampColor;
                lampLight.colorTemperature = initialLampColorTemperature;
            }
            if (secondRoomLampLight != null)
            {
                secondRoomLampLight.color = initialSecondLampColor;
                secondRoomLampLight.colorTemperature = initialSecondLampColorTemperature;
            }

            primaryInteraction?.SetInteractionEnabled(false);
            primaryInteraction?.SetBlockedDrawInteractionEnabled(false);
            secondRoomInteraction?.SetInteractionEnabled(false);
            secondRoomInteraction?.SetBlockedDrawInteractionEnabled(false);
            if (screenFade != null)
            {
                screenFade.alpha = 1f;
                screenFade.blocksRaycasts = true;
                screenFade.interactable = false;
            }
            nextClockTick = Time.unscaledTime + 1f;
            nextFloorCreak = Time.unscaledTime + UnityEngine.Random.Range(15f, 20f);
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
                case ClosedRoomCue.BeginOpening:
                    StartCoroutine(OpeningRoutine());
                    break;
                case ClosedRoomCue.PulseOpeningCard:
                    StartCardDip(0.8f, 0.2f, true);
                    break;
                case ClosedRoomCue.StartRearLookRule:
                    StartRearLookRule();
                    break;
                case ClosedRoomCue.ArmLightRule:
                    ArmLightRule();
                    break;
                case ClosedRoomCue.ArmSecondDoorRule:
                    ArmSecondDoorRule();
                    break;
                case ClosedRoomCue.ArmEnterRule:
                    ArmEnterRule();
                    break;
                case ClosedRoomCue.MarkEnterCardDrawn:
                    SetFact(enterCardDrawnFact, true);
                    break;
                case ClosedRoomCue.ResolveRoomCardEdge:
                    ResolveRoomCardEdge();
                    break;
                case ClosedRoomCue.BeginActOneToTwo:
                    StartCoroutine(ActOneToTwoRoutine());
                    break;
                case ClosedRoomCue.ResumeAtmosphere:
                    ResumeAtmosphere();
                    break;
                case ClosedRoomCue.ArmWindowVision:
                    ArmWindowVision();
                    break;
                case ClosedRoomCue.PauseSensoryBeat:
                    PauseSensoryBeat();
                    break;
                case ClosedRoomCue.BeginActTwoToThree:
                    StartCoroutine(ActTwoToThreeRoutine());
                    break;
                case ClosedRoomCue.StartHuntFar:
                    StartHunt(false);
                    break;
                case ClosedRoomCue.StartHuntClose:
                    StartHunt(true);
                    break;
                case ClosedRoomCue.BeginActThreeToFour:
                    StartCoroutine(ActThreeToFourRoutine());
                    break;
                case ClosedRoomCue.StartTurnAroundTest:
                    StartTurnAroundTest();
                    break;
                case ClosedRoomCue.ScheduleFirstDoorOpen:
                    StartCoroutine(OpenStoryDoorAfterDelay());
                    break;
                case ClosedRoomCue.SwingUnnaturalShadow:
                    StartCoroutine(SwingShadowRoutine());
                    break;
                case ClosedRoomCue.OpenExit:
                    OpenExit();
                    break;
                case ClosedRoomCue.PrepareEnding:
                    PrepareEndingReset();
                    break;
                case ClosedRoomCue.ShowEnding:
                    if (endingRoutine != null)
                    {
                        StopCoroutine(endingRoutine);
                    }
                    endingRoutine = StartCoroutine(EndingZoomRoutine());
                    break;
            }
        }

        private IEnumerator OpeningRoutine()
        {
            primaryLightBase = initialLampIntensity;
            secondLightBase = initialSecondLampIntensity;
            SetLightEnabled(lampLight, true);
            SetLightEnabled(secondRoomLampLight, true);
            if (screenFade != null)
            {
                screenFade.alpha = 0f;
                screenFade.blocksRaycasts = false;
            }
            if (ambientSource != null)
            {
                ambientSource.volume = initialAmbientVolume;
                if (!ambientSource.isPlaying)
                {
                    ambientSource.Play();
                }
            }
            StartClockLoop();
            PlayOneShot(fluorescentPowerClip, 0.55f);
            primaryInteraction?.SetInteractionEnabled(true);
            yield return null;
        }

        private void StartRearLookRule()
        {
            StopRearLookRule();
            Transform rimAnchor = inSecondRoom ? secondRoomRimAnchor : firstRoomRimAnchor;
            if (rearDoorRimLight != null && rimAnchor != null)
            {
                rearDoorRimLight.transform.SetPositionAndRotation(rimAnchor.position, rimAnchor.rotation);
            }
            lookReferenceForward = HorizontalDirection(playerView != null ? playerView.forward : transform.forward);
            lookRuleActive = true;
            rearImpactTriggered = false;
            if (rearSource != null && threatDroneClip != null)
            {
                rearSource.clip = threatDroneClip;
                rearSource.loop = true;
                rearSource.spatialBlend = 0f;
                rearSource.volume = 0.032f;
                rearSource.Play();
            }
        }

        private void UpdateRearLookRule()
        {
            if (!lookRuleActive || playerView == null)
            {
                return;
            }
            float angle = Vector3.Angle(lookReferenceForward, HorizontalDirection(playerView.forward));
            bool rimVisible = angle >= 90f;
            SetLightEnabled(rearDoorRimLight, rimVisible);
            if (rearSource != null)
            {
                rearSource.volume = Mathf.Lerp(0.032f, 0.063f, Mathf.InverseLerp(90f, 150f, angle));
            }
            if (angle >= rearImpactAngle && !rearImpactTriggered)
            {
                rearImpactTriggered = true;
                StartCoroutine(CameraShakeRoutine(0.008f, 0.3f));
                PlayOneShot(rearImpactClip, 0.38f);
            }
            else if (angle < 20f)
            {
                rearImpactTriggered = false;
            }
        }

        private void StopRearLookRule()
        {
            lookRuleActive = false;
            rearImpactTriggered = false;
            SetLightEnabled(rearDoorRimLight, false);
            if (rearSource != null && rearSource.clip == threatDroneClip)
            {
                rearSource.Stop();
                rearSource.clip = null;
            }
        }

        private void ArmLightRule()
        {
            bool firstReveal = lightSwitchRoot != null && !lightSwitchRoot.activeSelf;
            lightSwitchRoot?.SetActive(true);
            if (firstReveal)
            {
                lightSwitch?.ResetSwitch(true);
            }
            if (lightSwitch != null && lightSwitch.IsOn)
            {
                lightSwitch.SetInteractionEnabled(true);
            }
            QueueRuleCardDip(ref lightRuleRevealCount, true);
        }

        private void HandleLightSwitchStateChanged(HorrorLightSwitchInteractable source, bool isOn)
        {
            if (GetBool(lightSwitchUsedFact))
            {
                return;
            }
            if (!isOn)
            {
                lightRuleBlackoutActive = true;
                SetLightEnabled(lampLight, false);
                SetLightEnabled(secondRoomLampLight, false);
                SetLightEnabled(moonLight, true);
                if (ambientSource != null)
                {
                    ambientSource.volume = 0f;
                }
                clockSource?.Stop();
                source.SetInteractionEnabled(false);
                StartCoroutine(ReenableSwitchAfterDarkHold(source));
                return;
            }
            lightRuleRevealCount = 0;
            SetLightEnabled(moonLight, false);
            RestoreAlteredFluorescentLight();
            RevealSecondDoor();
            SetFact(lightSwitchUsedFact, true);
            runner?.RequestExternalAdvance();
        }

        private IEnumerator ReenableSwitchAfterDarkHold(HorrorLightSwitchInteractable source)
        {
            yield return new WaitForSecondsRealtime(1.35f);
            if (source != null && !source.IsOn && !GetBool(lightSwitchUsedFact))
            {
                if (clockSource != null && clockTickClip != null)
                {
                    clockSource.PlayOneShot(clockTickClip, 0.3f);
                }
                yield return new WaitForSecondsRealtime(0.3f);
                source.SetInteractionEnabled(true);
            }
        }

        private void RestoreAlteredFluorescentLight()
        {
            lightRuleBlackoutActive = false;
            if (lampLight != null)
            {
                lampLight.color = new Color(1f, 0.86f, 0.5f);
                lampLight.colorTemperature = 3400f;
            }
            if (secondRoomLampLight != null)
            {
                secondRoomLampLight.color = new Color(1f, 0.86f, 0.5f);
                secondRoomLampLight.colorTemperature = 3400f;
            }
            SetLightEnabled(lampLight, true);
            SetLightEnabled(secondRoomLampLight, true);
            if (ambientSource != null)
            {
                ambientSource.volume = initialAmbientVolume;
                if (!ambientSource.isPlaying)
                {
                    ambientSource.Play();
                }
            }
            StartClockLoop();
            PlayOneShot(lowStingerClip != null ? lowStingerClip : rearImpactClip, 0.18f);
        }

        private void RevealSecondDoor()
        {
            secondDoorCover?.SetActive(false);
            secondDoorRoot?.SetActive(true);
            secondDoor?.SnapClosed();
            secondDoor?.SetInteractionEnabled(false);
        }

        private void ArmSecondDoorRule()
        {
            QueueRuleCardDip(ref secondDoorRuleRevealCount, false);
            RevealSecondDoor();
            secondDoor?.SetInteractionEnabled(true);
        }

        private void HandleSecondDoorOpened(HorrorDoorInteractable source)
        {
            if (GetBool(secondDoorOpenedFact))
            {
                return;
            }
            secondDoorRuleRevealCount = 0;
            SetFact(secondDoorOpenedFact, true);
            runner?.RequestExternalAdvance();
        }

        private void ArmEnterRule()
        {
            QueueRuleCardDip(ref enterRuleRevealCount, false);
            secondRoomZone?.SetTriggerEnabled(true);
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
                    EnterEndingExit();
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
            playerFootsteps?.SetAlternateSurface(true);
            enterRuleRevealCount = 0;
            primaryInteraction?.SetInteractionEnabled(false);
            secondRoomInteraction?.SetInteractionEnabled(true);
            returnZone?.SetTriggerEnabled(true);
            SwitchPresenterForCurrentRoom(true);
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
            playerFootsteps?.SetAlternateSurface(false);
            secondRoomInteraction?.SetInteractionEnabled(false);
            primaryInteraction?.SetInteractionEnabled(true);
            SwitchPresenterForCurrentRoom(true);
            SetFact(enteredSecondRoomFact, false);
            SetFact(exitedSecondRoomFact, true);
            if (!GetBool(enterCardDrawnFact))
            {
                runner?.RequestExternalAdvance();
            }
        }

        private void ResolveRoomCardEdge()
        {
            if (GetBool(exitedSecondRoomFact) && !GetBool(enterCardDrawnFact))
            {
                runner?.RequestExternalAdvance();
            }
        }

        private void SwitchPresenterForCurrentRoom(bool resetPresentation)
        {
            CardDeckPresenter target = inSecondRoom ? secondRoomPresenter : primaryPresenter;
            if (runner == null || target == null || runner.SetPresenter(target, resetPresentation))
            {
                return;
            }
            if (presenterSwitchRoutine != null)
            {
                StopCoroutine(presenterSwitchRoutine);
            }
            presenterSwitchRoutine = StartCoroutine(SwitchPresenterWhenAvailable(resetPresentation));
        }

        private IEnumerator SwitchPresenterWhenAvailable(bool resetPresentation)
        {
            while (runner != null)
            {
                CardDeckPresenter target = inSecondRoom ? secondRoomPresenter : primaryPresenter;
                if (target != null && runner.SetPresenter(target, resetPresentation))
                {
                    break;
                }
                yield return null;
            }
            presenterSwitchRoutine = null;
        }

        private IEnumerator ActOneToTwoRoutine()
        {
            FreezeAtmosphere();
            yield return new WaitForSecondsRealtime(3f);
            if (ambientSource != null)
            {
                ambientSource.volume = initialAmbientVolume * 0.35f;
                if (!ambientSource.isPlaying)
                {
                    ambientSource.Play();
                }
            }
            storyDoor?.TwistHandleByStory(2f, 45f);
        }

        private void FreezeAtmosphere()
        {
            sensoryFrozen = true;
            microFlickerPaused = true;
            if (ambientSource != null)
            {
                ambientSource.volume = 0f;
            }
            clockSource?.Stop();
        }

        private void ResumeAtmosphere()
        {
            sensoryFrozen = false;
            microFlickerPaused = false;
            if (ambientSource != null)
            {
                ambientSource.volume = initialAmbientVolume;
                if (!ambientSource.isPlaying)
                {
                    ambientSource.Play();
                }
            }
            StartClockLoop();
        }

        private void ArmWindowVision()
        {
            windowGazeElapsed = 0f;
            windowVisionArmed = true;
            windowVision?.SetActive(false);
        }

        private void UpdateWindowGaze()
        {
            if (!windowVisionArmed || playerView == null || windowGazeTarget == null)
            {
                return;
            }
            Vector3 direction = windowGazeTarget.position - playerView.position;
            bool focused = direction.sqrMagnitude < 0.001f
                || Vector3.Dot(playerView.forward, direction.normalized) >= focusedLookDot;
            windowGazeElapsed = focused
                ? windowGazeElapsed + Time.deltaTime
                : Mathf.Max(0f, windowGazeElapsed - Time.deltaTime * 2f);
            if (windowGazeElapsed >= windowGazeDuration)
            {
                windowVisionArmed = false;
                StartCoroutine(WindowVisionRoutine());
            }
        }

        private IEnumerator WindowVisionRoutine()
        {
            windowVision?.SetActive(true);
            if (transitionSource != null && whiteNoiseClip != null)
            {
                transitionSource.clip = whiteNoiseClip;
                transitionSource.loop = false;
                transitionSource.volume = 0.18f;
                transitionSource.Play();
            }
            yield return new WaitForSecondsRealtime(1.5f);
            windowVision?.SetActive(false);
            if (transitionSource != null && transitionSource.clip == whiteNoiseClip)
            {
                transitionSource.Stop();
            }
            SetFact(windowVisionSeenFact, true);
            runner?.RequestExternalAdvance();
        }

        private void PauseSensoryBeat()
        {
            if (sensoryRoutine != null)
            {
                StopCoroutine(sensoryRoutine);
            }
            sensoryRoutine = StartCoroutine(SensoryBeatRoutine());
        }

        private IEnumerator SensoryBeatRoutine()
        {
            FreezeAtmosphere();
            yield return new WaitForSecondsRealtime(1.25f);
            ResumeAtmosphere();
            sensoryRoutine = null;
        }

        private IEnumerator ActTwoToThreeRoutine()
        {
            sensoryFrozen = false;
            microFlickerPaused = false;
            StartLightFade(initialLampIntensity * 0.4f, initialSecondLampIntensity * 0.4f, 6f);
            if (transitionSource != null && threatBreathingClip != null)
            {
                transitionSource.clip = threatBreathingClip;
                transitionSource.loop = true;
                transitionSource.volume = 0f;
                transitionSource.Play();
            }
            float elapsed = 0f;
            const float duration = 3f;
            float ambientStart = ambientSource != null ? ambientSource.volume : 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (ambientSource != null)
                {
                    ambientSource.volume = Mathf.Lerp(ambientStart, 0f, t);
                }
                if (transitionSource != null)
                {
                    transitionSource.volume = Mathf.Lerp(0f, 0.2f, t);
                }
                yield return null;
            }
        }

        private void StartHunt(bool close)
        {
            if (threatSilhouette == null || threatStart == null || threatEnd == null)
            {
                return;
            }
            if (threatFadeRoutine != null)
            {
                StopCoroutine(threatFadeRoutine);
                threatFadeRoutine = null;
            }
            if (transitionSource != null && transitionSource.clip == threatBreathingClip)
            {
                transitionSource.Stop();
            }
            huntActive = true;
            huntHovering = false;
            threatProgress = close ? 0.42f : 0f;
            threatSilhouette.gameObject.SetActive(true);
            threatSilhouette.localScale = threatBaseScale;
            threatSilhouette.SetPositionAndRotation(
                Vector3.Lerp(threatStart.position, threatEnd.position, threatProgress),
                threatStart.rotation);
            SetThreatAlpha(1f);
            SetLightEnabled(silhouetteBacklight, true);
            AudioClip approachClip = threatApproachClip != null
                ? threatApproachClip
                : threatBreathingClip;
            if (threatSource != null && approachClip != null)
            {
                threatSource.transform.position = threatSilhouette.position;
                threatSource.clip = approachClip;
                threatSource.loop = true;
                threatSource.spatialBlend = 1f;
                threatSource.volume = close ? 0.31f : 0.14f;
                threatSource.Play();
            }
        }

        private void UpdateThreat()
        {
            if (!huntActive || threatSilhouette == null)
            {
                huntHovering = false;
                return;
            }
            bool hovered = ActiveDeckInteraction != null && ActiveDeckInteraction.IsInteractionHighlighted;
            if (hovered != huntHovering)
            {
                huntHovering = hovered;
                if (hovered)
                {
                    PlayOneShot(deckHoverClip != null ? deckHoverClip : threatDroneClip, 0.12f);
                }
            }
            if (!huntHovering)
            {
                threatProgress = Mathf.Clamp01(
                    threatProgress + Time.deltaTime / Mathf.Max(1f, threatApproachDuration));
            }
            float shaped = threatProgress * threatProgress;
            threatSilhouette.position = Vector3.Lerp(threatStart.position, threatEnd.position, shaped);
            if (threatSource != null)
            {
                threatSource.transform.position = threatSilhouette.position;
                threatSource.volume = Mathf.Lerp(0.14f, 0.65f, shaped);
            }
        }

        private void DismissThreat(float duration)
        {
            if (threatFadeRoutine != null)
            {
                StopCoroutine(threatFadeRoutine);
            }
            threatFadeRoutine = StartCoroutine(DismissThreatRoutine(Mathf.Max(0.05f, duration)));
        }

        private IEnumerator DismissThreatRoutine(float duration)
        {
            huntActive = false;
            huntHovering = false;
            float startVolume = threatSource != null ? threatSource.volume : 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetThreatAlpha(1f - t);
                if (threatSilhouette != null)
                {
                    threatSilhouette.localScale = Vector3.Lerp(threatBaseScale, threatBaseScale * 0.92f, t);
                }
                if (threatSource != null)
                {
                    threatSource.volume = Mathf.Lerp(startVolume, 0f, t);
                }
                yield return null;
            }
            if (threatSilhouette != null)
            {
                threatSilhouette.localScale = threatBaseScale;
                threatSilhouette.gameObject.SetActive(false);
            }
            threatSource?.Stop();
            SetLightEnabled(silhouetteBacklight, false);
            SetThreatAlpha(1f);
            threatFadeRoutine = null;
        }

        private IEnumerator ActThreeToFourRoutine()
        {
            if (huntActive || (threatSilhouette != null && threatSilhouette.gameObject.activeSelf))
            {
                DismissThreat(0.8f);
            }
            StartLightFade(initialLampIntensity * 0.55f, initialSecondLampIntensity * 0.55f, 1.4f);
            yield return new WaitForSecondsRealtime(1.5f);
            if (rearSource != null && threatBreathingClip != null)
            {
                rearSource.transform.position = threatEnd != null ? threatEnd.position : transform.position;
                rearSource.clip = threatBreathingClip;
                rearSource.loop = false;
                rearSource.spatialBlend = 1f;
                rearSource.volume = 0.16f;
                rearSource.Play();
            }
            yield return new WaitForSecondsRealtime(1f);
            if (rearSource != null && rearSource.clip == threatBreathingClip)
            {
                rearSource.Stop();
            }
        }

        private void StartTurnAroundTest()
        {
            if (turnRoutine != null)
            {
                StopCoroutine(turnRoutine);
            }
            turnTestActive = true;
            turnViolationTriggered = false;
            turnReferenceForward = HorizontalDirection(playerView != null ? playerView.forward : transform.forward);
            SetFact(turnedAroundFact, false);
            SetFact(turnTestResolvedFact, false);
            turnRoutine = StartCoroutine(TurnTestRoutine());
        }

        private void UpdateTurnTest()
        {
            if (!turnTestActive || turnViolationTriggered || playerView == null)
            {
                return;
            }
            float angle = Vector3.Angle(turnReferenceForward, HorizontalDirection(playerView.forward));
            if (angle >= rearImpactAngle)
            {
                turnViolationTriggered = true;
                SetFact(turnedAroundFact, true);
                StartCoroutine(FrameBlackoutRoutine());
            }
        }

        private IEnumerator TurnTestRoutine()
        {
            if (rearSource != null && footstepsBehindClip != null)
            {
                rearSource.transform.position = playerRoot != null
                    ? playerRoot.position - playerRoot.forward * 1.8f
                    : transform.position;
                rearSource.spatialBlend = 1f;
                rearSource.volume = 0.48f;
                rearSource.PlayOneShot(footstepsBehindClip, 0.55f);
                yield return new WaitForSecondsRealtime(Mathf.Max(2.48f, footstepsBehindClip.length));
            }
            else
            {
                for (int step = 0; step < 4; step++)
                {
                    if (rearSource != null && floorCreakClip != null)
                    {
                        rearSource.transform.position = playerRoot != null
                            ? playerRoot.position - playerRoot.forward * (2.2f - step * 0.35f)
                            : transform.position;
                        rearSource.spatialBlend = 1f;
                        rearSource.volume = 0.36f + step * 0.04f;
                        rearSource.PlayOneShot(floorCreakClip, 0.5f);
                    }
                    yield return new WaitForSecondsRealtime(0.62f);
                }
            }
            yield return new WaitForSecondsRealtime(3f);
            turnTestActive = false;
            if (!turnViolationTriggered)
            {
                StartLightFade(primaryLightBase * 1.03f, secondLightBase * 1.03f, 0.35f);
            }
            SetFact(turnTestResolvedFact, true);
            runner?.RequestExternalAdvance();
            turnRoutine = null;
        }

        private IEnumerator FrameBlackoutRoutine()
        {
            if (screenFade == null)
            {
                yield break;
            }
            screenFade.alpha = 1f;
            yield return null;
            yield return null;
            screenFade.alpha = 0f;
        }

        private IEnumerator OpenStoryDoorAfterDelay()
        {
            yield return new WaitForSecondsRealtime(4f);
            if (storyDoor != null)
            {
                AudioSource source = storyDoor.GetComponent<AudioSource>();
                if (source != null)
                {
                    source.pitch = 0.9f;
                }
                storyDoor.OpenByStory();
            }
        }

        private IEnumerator SwingShadowRoutine()
        {
            if (shadowCaster == null)
            {
                yield break;
            }
            Quaternion target = shadowInitialRotation * Quaternion.Euler(0f, 38f, 0f);
            float elapsed = 0f;
            const float duration = 1f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                shadowCaster.localRotation = Quaternion.Slerp(
                    shadowInitialRotation,
                    target,
                    Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            shadowCaster.localRotation = shadowInitialRotation;
        }

        private void OpenExit()
        {
            endingExitArmed = true;
            storyDoor?.OpenByStory(false);
            SetLightEnabled(exitLight, true);
            endingZone?.SetTriggerEnabled(true);
            if (windSource != null && windClip != null)
            {
                windSource.clip = windClip;
                windSource.loop = true;
                windSource.volume = 0.12f;
                windSource.Play();
            }
            if (enableClimaxThreat)
            {
                StartHunt(true);
            }
        }

        private void UpdateExitCamera()
        {
            if (playerCamera == null || endingActive)
            {
                return;
            }
            float target = initialCameraFov;
            if (endingExitArmed && storyDoor != null && playerView != null)
            {
                Vector3 toDoor = storyDoor.transform.position - playerView.position;
                if (toDoor.sqrMagnitude > 0.01f
                    && Vector3.Dot(playerView.forward, toDoor.normalized) > 0.45f)
                {
                    target = initialCameraFov + 4f;
                }
            }
            else if ((lightSwitch != null && lightSwitch.IsInteractionHighlighted)
                     || (secondDoor != null && secondDoor.IsInteractionHighlighted))
            {
                target = initialCameraFov - 2f;
            }
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, Time.deltaTime * 5f);
        }

        private void EnterEndingExit()
        {
            if (!endingExitArmed)
            {
                return;
            }
            endingZone?.SetTriggerEnabled(false);
            SetFact(leftRoomFact, true);
            runner?.RequestExternalAdvance();
        }

        private void PrepareEndingReset()
        {
            endingActive = true;
            endingExitArmed = false;
            StopRearLookRule();
            turnTestActive = false;
            if (turnRoutine != null)
            {
                StopCoroutine(turnRoutine);
                turnRoutine = null;
            }
            rearSource?.Stop();
            if (threatFadeRoutine != null)
            {
                StopCoroutine(threatFadeRoutine);
                threatFadeRoutine = null;
            }
            huntActive = false;
            if (threatSilhouette != null)
            {
                threatSilhouette.gameObject.SetActive(false);
            }
            threatSource?.Stop();
            if (movementController != null)
            {
                movementController.enabled = false;
            }
            if (playerFootsteps != null)
            {
                playerFootsteps.enabled = false;
            }
            if (playerRoot != null && playerStartMarker != null)
            {
                playerRoot.SetPositionAndRotation(playerStartMarker.position, playerStartMarker.rotation);
            }
            if (playerView != null)
            {
                playerView.localRotation = initialViewLocalRotation;
            }
            inSecondRoom = false;
            playerFootsteps?.SetAlternateSurface(false);
            firstRoomSet?.SetActive(true);
            secondRoomSet?.SetActive(false);
            lightSwitchRoot?.SetActive(false);
            secondDoorRoot?.SetActive(false);
            secondDoorCover?.SetActive(true);
            returnZone?.SetTriggerEnabled(false);
            endingPortraitSilhouette?.SetActive(false);
            SetLightEnabled(lampLight, true);
            SetLightEnabled(secondRoomLampLight, false);
            SetLightEnabled(moonLight, false);
            SetLightEnabled(rearDoorRimLight, false);
            SetLightEnabled(silhouetteBacklight, false);
            SetLightEnabled(exitLight, false);
            primaryLightBase = initialLampIntensity;
            if (lampLight != null)
            {
                lampLight.color = initialLampColor;
                lampLight.colorTemperature = initialLampColorTemperature;
            }
            windSource?.Stop();
            transitionSource?.Stop();
            if (ambientSource != null)
            {
                ambientSource.volume = initialAmbientVolume;
                if (!ambientSource.isPlaying)
                {
                    ambientSource.Play();
                }
            }
            StartClockLoop();
            secondRoomInteraction?.SetInteractionEnabled(false);
            primaryInteraction?.SetInteractionEnabled(false);
            if (runner != null && primaryPresenter != null)
            {
                int deckSize = runner.Sequence != null ? runner.Sequence.VisualDeckSize : 48;
                primaryPresenter.ResetPresentation(Mathf.Max(48, deckSize));
                primaryPresenter.SetDeckThicknessMultiplier(3.7f);
                runner.SetPresenter(primaryPresenter, false);
            }
            if (playerCamera != null)
            {
                playerCamera.fieldOfView = initialCameraFov;
            }
            if (screenFade != null)
            {
                screenFade.alpha = 0f;
                screenFade.blocksRaycasts = false;
            }
        }

        private IEnumerator EndingZoomRoutine()
        {
            float startFov = playerCamera != null ? playerCamera.fieldOfView : initialCameraFov;
            float elapsed = 0f;
            while (elapsed < EndingDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / EndingDurationSeconds);
                t = t * t * (3f - 2f * t);
                if (playerCamera != null)
                {
                    playerCamera.fieldOfView = Mathf.Lerp(startFov, 24f, t);
                }

                if (screenFade != null)
                {
                    float fade = Mathf.InverseLerp(
                        EndingDurationSeconds - EndingFadeSeconds,
                        EndingDurationSeconds,
                        elapsed);
                    screenFade.alpha = fade;
                    screenFade.blocksRaycasts = fade > 0f;
                }

                yield return null;
            }
            if (screenFade != null)
            {
                screenFade.alpha = 1f;
                screenFade.blocksRaycasts = true;
            }
            endingRoutine = null;
            QuitGame();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleCardDrawStarted(CardDefinition card, int drawIndex)
        {
            StopRearLookRule();
            if (windowVisionArmed)
            {
                windowVisionArmed = false;
                windowVision?.SetActive(false);
            }
            if (huntActive)
            {
                DismissThreat(2f);
            }
        }

        private void HandleCardRevealed(CardDefinition card, GameObject cardObject, int drawIndex)
        {
            float minimum = Mathf.Clamp(pendingCardDipMinimum, 0.8f, 0.84f);
            pendingCardDipMinimum = 0.84f;
            StartCardDip(minimum, 0.2f, true);
        }

        private void QueueRuleCardDip(ref int revealCount, bool escalate)
        {
            revealCount++;
            int repeatIndex = Mathf.Max(0, revealCount - 1);
            pendingCardDipMinimum = escalate
                ? Mathf.Clamp(0.84f - repeatIndex * 0.01f, 0.8f, 0.84f)
                : 0.84f;
        }

        private void StartCardDip(float minimumMultiplier, float duration, bool playTick)
        {
            if (dipRoutine != null)
            {
                StopCoroutine(dipRoutine);
            }
            dipRoutine = StartCoroutine(CardDipRoutine(minimumMultiplier, duration, playTick));
        }

        private IEnumerator CardDipRoutine(float minimumMultiplier, float duration, bool playTick)
        {
            if (playTick)
            {
                PlayOneShot(lampTickClip, 0.38f);
            }
            float half = Mathf.Max(0.01f, duration * 0.5f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed < half
                    ? Mathf.Clamp01(elapsed / half)
                    : Mathf.Clamp01((duration - elapsed) / half);
                flickerDipMultiplier = Mathf.Lerp(1f, minimumMultiplier, t);
                yield return null;
            }
            flickerDipMultiplier = 1f;
            dipRoutine = null;
        }

        private void UpdateAmbientDetails()
        {
            if (sensoryFrozen)
            {
                return;
            }
            float rotation = Time.deltaTime * 6f;
            firstClockHand?.Rotate(0f, 0f, -rotation, Space.Self);
            secondClockHand?.Rotate(0f, 0f, rotation, Space.Self);
            if (clockLoopClip == null && Time.unscaledTime >= nextClockTick)
            {
                nextClockTick = Time.unscaledTime + 1f;
                if (clockSource != null && clockTickClip != null)
                {
                    clockSource.PlayOneShot(clockTickClip, 0.24f);
                }
            }
            if (!endingActive && !lightRuleBlackoutActive && Time.unscaledTime >= nextFloorCreak)
            {
                nextFloorCreak = Time.unscaledTime + UnityEngine.Random.Range(15f, 20f);
                if (rearSource != null && floorCreakClip != null && !rearSource.isPlaying)
                {
                    rearSource.transform.position = playerRoot != null
                        ? playerRoot.position + UnityEngine.Random.onUnitSphere * 2f
                        : transform.position;
                    rearSource.spatialBlend = 1f;
                    rearSource.volume = 0.09f;
                    rearSource.PlayOneShot(floorCreakClip, 0.18f);
                }
            }
        }

        private void UpdateLampFlicker()
        {
            float flicker = 1f;
            if (!microFlickerPaused && !sensoryFrozen)
            {
                float slow = Mathf.PerlinNoise(Time.unscaledTime * 2.17f, 0.31f) - 0.5f;
                float fast = Mathf.PerlinNoise(Time.unscaledTime * 7.91f, 0.73f) - 0.5f;
                flicker += (slow * 1.45f + fast * 0.55f) * flickerAmplitude;
            }
            if (lampLight != null && lampLight.enabled)
            {
                lampLight.intensity = primaryLightBase * flicker * flickerDipMultiplier;
            }
            if (secondRoomLampLight != null && secondRoomLampLight.enabled)
            {
                secondRoomLampLight.intensity = secondLightBase * flicker * flickerDipMultiplier;
            }
        }

        private void StartLightFade(float primaryTarget, float secondTarget, float duration)
        {
            if (lightFadeRoutine != null)
            {
                StopCoroutine(lightFadeRoutine);
            }
            lightFadeRoutine = StartCoroutine(LightFadeRoutine(primaryTarget, secondTarget, duration));
        }

        private IEnumerator LightFadeRoutine(float primaryTarget, float secondTarget, float duration)
        {
            float primaryStart = primaryLightBase;
            float secondStart = secondLightBase;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                primaryLightBase = Mathf.Lerp(primaryStart, primaryTarget, t);
                secondLightBase = Mathf.Lerp(secondStart, secondTarget, t);
                yield return null;
            }
            primaryLightBase = primaryTarget;
            secondLightBase = secondTarget;
            lightFadeRoutine = null;
        }

        private IEnumerator CameraShakeRoutine(float amplitude, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                scriptedShake = Mathf.Lerp(amplitude, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            scriptedShake = 0f;
        }

        private void CacheThreatMaterials()
        {
            threatRenderers.Clear();
            if (threatSilhouette == null)
            {
                return;
            }
            foreach (Renderer renderer in threatSilhouette.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }
                renderer.material = new Material(renderer.sharedMaterial);
                threatRenderers.Add(renderer);
            }
        }

        private void SetThreatAlpha(float alpha)
        {
            foreach (Renderer renderer in threatRenderers)
            {
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }
                Material material = renderer.material;
                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    color.a = Mathf.Clamp01(alpha);
                    material.SetColor("_BaseColor", color);
                }
                if (material.HasProperty("_Color"))
                {
                    Color color = material.GetColor("_Color");
                    color.a = Mathf.Clamp01(alpha);
                    material.SetColor("_Color", color);
                }
            }
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (oneShotSource != null && clip != null)
            {
                oneShotSource.PlayOneShot(clip, volume);
            }
        }

        private void StartClockLoop()
        {
            if (clockSource == null || clockLoopClip == null)
            {
                return;
            }

            if (clockSource.clip != clockLoopClip)
            {
                clockSource.Stop();
                clockSource.clip = clockLoopClip;
            }
            clockSource.loop = true;
            if (!clockSource.isPlaying)
            {
                clockSource.Play();
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
            return blackboard != null
                && fact != null
                && blackboard.GetValue(fact).BoolValue;
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

        private static void SetLightEnabled(Light light, bool enabled)
        {
            if (light != null)
            {
                light.enabled = enabled;
                foreach (Renderer renderer in light.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = enabled;
                }
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
            flickerAmplitude = Mathf.Clamp(flickerAmplitude, 0f, 0.05f);
            threatApproachDuration = Mathf.Max(1f, threatApproachDuration);
            focusedLookDot = Mathf.Clamp(focusedLookDot, 0.5f, 0.999f);
            windowGazeDuration = Mathf.Max(0.1f, windowGazeDuration);
            rearImpactAngle = Mathf.Clamp(rearImpactAngle, 90f, 179f);
        }
    }
}

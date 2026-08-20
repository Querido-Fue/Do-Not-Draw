using System;
using System.Collections;
using System.Collections.Generic;
using DoNotDraw.Audio;
using DoNotDraw.Interaction;
using DoNotDraw.Narrative;
using DoNotDraw.UI;
using UnityEngine;
using UnityEngine.UI;

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
        ShowEnding,
        CloseSecondDoorOnLook
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
        private const float EndingTransitionFadeSeconds = 1f;
        private const float WindowCardScareDelaySeconds = 0.2f;
        private const float WindowCardScareDurationSeconds = 0.86f;
        private static readonly int CeilingEmissionColorId = Shader.PropertyToID("_Emission_Color");
        private static readonly int CeilingEmissionMapId = Shader.PropertyToID("_Emission_Map");

        private sealed class CeilingEmissionBinding
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color InitialColor;
            public MaterialPropertyBlock PropertyBlock;
        }

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

        [Header("Ending Top View")]
        [SerializeField] private Camera endingTopViewCamera;
        [SerializeField, Min(0.1f)] private float endingTopViewZoomSize = 1.45f;

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

        [Header("Scripted Apparitions")]
        [SerializeField] private ImpossibleWindowWatcher secondRoomWindowWatcher;
        [SerializeField] private RoomFaceInfestation endingFaceInfestation;

        [Header("Lights")]
        [SerializeField] private Light lampLight;
        [SerializeField] private Light secondRoomLampLight;
        [SerializeField] private Renderer[] ceilingSurfaceRenderers = Array.Empty<Renderer>();
        [SerializeField] private Light moonLight;
        [SerializeField] private Light rearDoorRimLight;
        [SerializeField] private Transform firstRoomRimAnchor;
        [SerializeField] private Transform secondRoomRimAnchor;
        [SerializeField] private Light silhouetteBacklight;
        [SerializeField] private Light exitLight;
        [SerializeField, Range(0.1f, 1f)] private float litStateBrightnessMultiplier = 0.7f;
        [SerializeField, Range(0f, 0.05f)] private float flickerAmplitude;
        [SerializeField, Min(0.05f)] private float switchResidualDarkeningDuration = 1f;
        [SerializeField, Range(0f, 1f)] private float switchResidualLightMultiplier = 0.48f;

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
        [SerializeField] private HorrorPerceptionOverlay perceptionOverlay;
        [SerializeField] private bool enableClimaxThreat;

        [Header("Horror Perception Tuning")]
        [SerializeField, Range(0.15f, 0.3f)] private float huntLightMultiplier = 0.225f;
        [SerializeField, Range(0.15f, 0.35f)] private float huntCeilingMultiplier = 0.24f;
        [SerializeField, Range(0f, 0.05f)] private float huntFlickerAmplitude = 0.022f;
        [SerializeField, Range(5f, 6f)] private float exitPressureDelay = 5.5f;
        [SerializeField, Min(0.02f)] private float exitMovementThreshold = 0.08f;
        [SerializeField, Min(0.1f)] private float finalDoorLightInterval = 1f;
        [SerializeField, Range(0.02f, 0.25f)] private float finalDoorLightPulseDuration = 0.1f;
        [SerializeField, Min(0.1f)] private float finalDoorFaceRevealDuration = 2f;

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
        [SerializeField] private AudioClip windowFaceLungeClip;
        [SerializeField] private AudioClip finalDoorInfestationClip;

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
        private readonly List<CeilingEmissionBinding> ceilingEmissionBindings =
            new List<CeilingEmissionBinding>();

        private RandomFootstepPlayer playerFootsteps;
        private AudioListener playerAudioListener;
        private AudioListener endingTopViewAudioListener;
        private Graphic screenFadeGraphic;
        private Renderer[] playerVisualRenderers = Array.Empty<Renderer>();
        private bool[] initialPlayerVisualRendererStates = Array.Empty<bool>();
        private float initialLampIntensity;
        private float initialSecondLampIntensity;
        private float primaryLightBase;
        private float secondLightBase;
        private float initialAmbientVolume;
        private float ambientLogicalVolume;
        private float clockLogicalVolume;
        private float rearLogicalVolume;
        private float threatLogicalVolume;
        private float transitionLogicalVolume;
        private float windLogicalVolume;
        private float initialCameraFov;
        private float initialEndingTopViewFov;
        private float initialEndingTopViewOrthographicSize;
        private float initialLampColorTemperature;
        private float initialSecondLampColorTemperature;
        private Color initialLampColor;
        private Color initialSecondLampColor;
        private Color initialScreenFadeColor = Color.black;
        private Color initialAmbientLight;
        private Color initialAmbientSkyColor;
        private Color initialAmbientEquatorColor;
        private Color initialAmbientGroundColor;
        private float initialReflectionIntensity;
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
        private float narrativeFlickerAmplitude;
        private float horrorCeilingMultiplier = 1f;
        private float exitPressureArmedAt;
        private float exitPressureBaselineDistance;
        private float exitPreviousDistance;
        private float exitTowardProgress;
        private float nextClimaxStrobeAt;
        private float climaxStrobeUntil;
        private float climaxStrobeMultiplier = 1f;
        private float climaxSilenceUntil;
        private float exitFovImpulse;
        private float exitPrePressurePrimaryLight;
        private float exitPrePressureSecondLight;
        private Vector3 exitPreviousPlayerPosition;
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
        private bool horrorEmissionOverride;
        private bool exitPressureArmed;
        private bool exitPressureActive;
        private bool exitPressureSuppressed;
        private bool exitTrackingPositionValid;
        private bool lightRuleArmed;
        private bool lightRuleBlackoutActive;
        private bool ambientLightingCached;
        private bool finalDoorLightingOverride;
        private bool finalDoorLightPulseOn;
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
        private Coroutine endingTransitionRoutine;
        private Coroutine presenterSwitchRoutine;
        private Coroutine ambientDarkeningRoutine;
        private Coroutine exitWindPulseRoutine;
        private Coroutine windowCardScareRoutine;
        private Coroutine finalDoorHorrorRoutine;

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
            playerAudioListener = playerCamera != null
                ? playerCamera.GetComponent<AudioListener>()
                : null;
            endingTopViewAudioListener = endingTopViewCamera != null
                ? endingTopViewCamera.GetComponent<AudioListener>()
                : null;
            screenFadeGraphic = screenFade != null
                ? screenFade.GetComponentInChildren<Graphic>(true)
                : null;
            if (screenFadeGraphic != null)
            {
                initialScreenFadeColor = screenFadeGraphic.color;
            }
            CachePlayerVisualState();

            float sourceLampIntensity = lampLight != null ? lampLight.intensity : 1f;
            float sourceSecondLampIntensity = secondRoomLampLight != null
                ? secondRoomLampLight.intensity
                : sourceLampIntensity * 0.9f;
            initialLampIntensity = sourceLampIntensity * litStateBrightnessMultiplier;
            initialSecondLampIntensity = sourceSecondLampIntensity * litStateBrightnessMultiplier;
            primaryLightBase = initialLampIntensity;
            secondLightBase = initialSecondLampIntensity;
            if (lampLight != null)
            {
                lampLight.intensity = initialLampIntensity;
            }
            if (secondRoomLampLight != null)
            {
                secondRoomLampLight.intensity = initialSecondLampIntensity;
            }
            initialLampColor = lampLight != null ? lampLight.color : new Color(1f, 0.73f, 0.46f);
            initialSecondLampColor = secondRoomLampLight != null
                ? secondRoomLampLight.color
                : initialLampColor;
            initialLampColorTemperature = lampLight != null ? lampLight.colorTemperature : 4200f;
            initialSecondLampColorTemperature = secondRoomLampLight != null
                ? secondRoomLampLight.colorTemperature
                : initialLampColorTemperature;
            initialAmbientLight = ScaleRgb(RenderSettings.ambientLight, litStateBrightnessMultiplier);
            initialAmbientSkyColor = ScaleRgb(RenderSettings.ambientSkyColor, litStateBrightnessMultiplier);
            initialAmbientEquatorColor = ScaleRgb(RenderSettings.ambientEquatorColor, litStateBrightnessMultiplier);
            initialAmbientGroundColor = ScaleRgb(RenderSettings.ambientGroundColor, litStateBrightnessMultiplier);
            initialReflectionIntensity = RenderSettings.reflectionIntensity * litStateBrightnessMultiplier;
            ambientLightingCached = true;
            initialAmbientVolume = ambientSource != null ? ambientSource.volume : 0f;
            ambientLogicalVolume = initialAmbientVolume;
            clockLogicalVolume = clockSource != null ? clockSource.volume : 0f;
            initialCameraFov = playerCamera != null ? playerCamera.fieldOfView : 60f;
            initialEndingTopViewFov = endingTopViewCamera != null
                ? endingTopViewCamera.fieldOfView
                : initialCameraFov;
            initialEndingTopViewOrthographicSize = endingTopViewCamera != null
                ? endingTopViewCamera.orthographicSize
                : 2.65f;
            baseViewLocalPosition = playerView != null ? playerView.localPosition : Vector3.zero;
            initialViewLocalRotation = playerView != null ? playerView.localRotation : Quaternion.identity;
            threatBaseScale = threatSilhouette != null ? threatSilhouette.localScale : Vector3.one;
            shadowInitialRotation = shadowCaster != null ? shadowCaster.localRotation : Quaternion.identity;

            CacheThreatMaterials();
            CacheCeilingEmissionBindings();
            playerFootsteps?.SetAlternateSurface(false);
            ResetSceneState();
        }

        private void OnEnable()
        {
            SubscribeToSignals();
            if (perceptionOverlay != null)
            {
                perceptionOverlay.ClimaxHardCut += HandleClimaxHardCut;
            }
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
            if (endingTransitionRoutine != null)
            {
                StopCoroutine(endingTransitionRoutine);
                endingTransitionRoutine = null;
            }
            endingRoutine = null;
            primaryPresenter?.SetPresentationBlocked(false);
            secondRoomPresenter?.SetPresentationBlocked(false);
            if (windowCardScareRoutine != null)
            {
                StopCoroutine(windowCardScareRoutine);
                windowCardScareRoutine = null;
            }
            StopFinalDoorHorror(true);
            if (perceptionOverlay != null)
            {
                perceptionOverlay.ClimaxHardCut -= HandleClimaxHardCut;
                perceptionOverlay.StopAllEffects(0f);
            }
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
            SetEndingTopViewActive(false);
            SetPlayerVisualsVisible(true);
            RestoreAmbientLighting();
            SetCeilingEmissionMultiplier(1f);
            ResetHorrorPerceptionState();
            if (screenFade != null)
            {
                screenFade.alpha = 0f;
                screenFade.blocksRaycasts = false;
            }
            RestoreScreenFadeColor();
            presenterSwitchRoutine = null;
        }

        private void Update()
        {
            ApplyBgmVolumeToLoopingSources();
            UpdateAmbientDetails();
            UpdateLampFlicker();
            UpdateRearLookRule();
            UpdateWindowGaze();
            UpdateThreat();
            UpdateTurnTest();
            UpdateExitPressure();
            UpdateExitCamera();
        }

        private void ResetHorrorPerceptionState()
        {
            narrativeFlickerAmplitude = 0f;
            horrorCeilingMultiplier = 1f;
            horrorEmissionOverride = false;
            exitPressureArmed = false;
            exitPressureActive = false;
            exitPressureSuppressed = false;
            exitTrackingPositionValid = false;
            exitTowardProgress = 0f;
            nextClimaxStrobeAt = 0f;
            climaxStrobeUntil = 0f;
            climaxStrobeMultiplier = 1f;
            climaxSilenceUntil = 0f;
            exitFovImpulse = 0f;
            finalDoorLightingOverride = false;
            finalDoorLightPulseOn = false;
            if (exitWindPulseRoutine != null)
            {
                StopCoroutine(exitWindPulseRoutine);
                exitWindPulseRoutine = null;
            }
        }

        // 오디오소스별로 "원래 의도한(스케일 전) 볼륨"을 logicalVolume에 저장해 두고
        // 실제 AudioSource.volume에는 여기에 BgmVolume.Scale을 곱한 값만 반영합니다.
        // 이 소스들(ambient/clock/rear/threat/transition/wind)은 전부 루프/지속 재생용
        // 배경 사운드 채널이라 SFX가 아닌 BGM 슬라이더를 따라갑니다. 설정 팝업에서
        // BGM 슬라이더를 바꾸면 매 프레임 ApplyBgmVolumeToLoopingSources가 재적용하므로
        // 이미 재생 중인 사운드도 곧바로 볼륨이 따라갑니다.
        private void SetAudioVolume(AudioSource source, ref float logicalVolume, float value)
        {
            logicalVolume = value;
            if (source != null)
            {
                float audibleScale = IsClimaxHardCutSilent ? 0f : BgmVolume.Scale;
                source.volume = value * audibleScale;
            }
        }

        private void ApplyBgmVolumeToLoopingSources()
        {
            float scale = IsClimaxHardCutSilent ? 0f : BgmVolume.Scale;
            if (ambientSource != null)
            {
                ambientSource.volume = ambientLogicalVolume * scale;
            }
            if (clockSource != null)
            {
                clockSource.volume = clockLogicalVolume * scale;
            }
            if (rearSource != null)
            {
                rearSource.volume = rearLogicalVolume * scale;
            }
            if (threatSource != null)
            {
                threatSource.volume = threatLogicalVolume * scale;
            }
            if (transitionSource != null)
            {
                transitionSource.volume = transitionLogicalVolume * scale;
            }
            if (windSource != null)
            {
                windSource.volume = windLogicalVolume * scale;
            }
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
            if (endingTransitionRoutine != null)
            {
                StopCoroutine(endingTransitionRoutine);
                endingTransitionRoutine = null;
            }
            if (endingRoutine != null)
            {
                StopCoroutine(endingRoutine);
                endingRoutine = null;
            }
            primaryPresenter?.SetPresentationBlocked(false);
            secondRoomPresenter?.SetPresentationBlocked(false);
            if (windowCardScareRoutine != null)
            {
                StopCoroutine(windowCardScareRoutine);
                windowCardScareRoutine = null;
            }
            StopFinalDoorHorror(true);
            inSecondRoom = false;
            endingActive = false;
            endingExitArmed = false;
            lookRuleActive = false;
            windowVisionArmed = false;
            huntActive = false;
            turnTestActive = false;
            sensoryFrozen = false;
            microFlickerPaused = false;
            lightRuleArmed = false;
            lightRuleBlackoutActive = false;
            lightRuleRevealCount = 0;
            secondDoorRuleRevealCount = 0;
            enterRuleRevealCount = 0;
            pendingCardDipMinimum = 0.84f;
            ResetHorrorPerceptionState();
            SetEndingTopViewActive(false);
            SetPlayerVisualsVisible(true);
            perceptionOverlay?.StopAllEffects(0f);
            RestoreAmbientLighting();
            SetCeilingEmissionMultiplier(1f);

            firstRoomSet?.SetActive(true);
            secondRoomSet?.SetActive(true);
            lightSwitchRoot?.SetActive(true);
            lightSwitch?.SetInteractionEnabled(true);
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
                screenFade.alpha = 0f;
                screenFade.blocksRaycasts = false;
                screenFade.interactable = false;
            }
            RestoreScreenFadeColor();
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
                    BeginEndingTransition();
                    break;
                case ClosedRoomCue.ShowEnding:
                    if (endingRoutine == null)
                    {
                        endingRoutine = StartCoroutine(EndingZoomRoutine());
                    }
                    break;
                case ClosedRoomCue.CloseSecondDoorOnLook:
                    secondDoor?.CloseWithSlam();
                    break;
            }
        }

        private IEnumerator OpeningRoutine()
        {
            bool openingLightsOn = lightSwitch == null || lightSwitch.IsOn;
            ApplyOpeningSwitchState(openingLightsOn);
            if (screenFade != null)
            {
                screenFade.alpha = 0f;
                screenFade.blocksRaycasts = false;
            }
            if (openingLightsOn)
            {
                PlayOneShot(fluorescentPowerClip, 0.55f);
            }
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
                SetAudioVolume(rearSource, ref rearLogicalVolume, 0.032f);
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
                SetAudioVolume(rearSource, ref rearLogicalVolume, Mathf.Lerp(0.032f, 0.063f, Mathf.InverseLerp(90f, 150f, angle)));
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
            lightRuleArmed = true;
            lightSwitchRoot?.SetActive(true);
            if (lightSwitch != null)
            {
                if (!lightSwitch.IsOn)
                {
                    lightSwitch.ResetSwitch(true);
                    ApplyOpeningSwitchState(true);
                }
                lightSwitch.SetInteractionEnabled(true);
            }
            QueueRuleCardDip(ref lightRuleRevealCount, true);
        }

        private void HandleLightSwitchStateChanged(HorrorLightSwitchInteractable source, bool isOn)
        {
            if (!lightRuleArmed)
            {
                ApplyOpeningSwitchState(isOn);
                source?.SetInteractionEnabled(true);
                return;
            }

            if (GetBool(lightSwitchUsedFact))
            {
                return;
            }
            if (!isOn)
            {
                lightRuleRevealCount = 0;
                RevealSecondDoor();
                SetFact(lightSwitchUsedFact, true);
                lightRuleArmed = false;
                runner?.RequestExternalAdvance();
                source.SetInteractionEnabled(true);

                lightRuleBlackoutActive = true;
                SetLightEnabled(lampLight, false);
                SetLightEnabled(secondRoomLampLight, false);
                SetCeilingEmissionMultiplier(0f);
                SetLightEnabled(moonLight, true);
                StartSwitchResidualDarkening();
                if (ambientSource != null)
                {
                    SetAudioVolume(ambientSource, ref ambientLogicalVolume, 0f);
                }
                clockSource?.Stop();
                return;
            }
            lightRuleRevealCount = 0;
            SetLightEnabled(moonLight, false);
            RestoreAlteredFluorescentLight();
            RevealSecondDoor();
            SetFact(lightSwitchUsedFact, true);
            lightRuleArmed = false;
            runner?.RequestExternalAdvance();
        }

        private void ApplyOpeningSwitchState(bool isOn)
        {
            lightRuleBlackoutActive = !isOn;
            SetLightEnabled(lampLight, isOn);
            SetLightEnabled(secondRoomLampLight, isOn);
            SetCeilingEmissionMultiplier(isOn ? 1f : 0f);
            SetLightEnabled(moonLight, false);

            if (isOn)
            {
                RestoreAmbientLighting();
            }
            else
            {
                StartSwitchResidualDarkening();
            }

            if (isOn)
            {
                primaryLightBase = initialLampIntensity;
                secondLightBase = initialSecondLampIntensity;
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
            }

            if (ambientSource != null)
            {
                SetAudioVolume(ambientSource, ref ambientLogicalVolume, isOn ? initialAmbientVolume : 0f);
                if (isOn && !ambientSource.isPlaying)
                {
                    ambientSource.Play();
                }
            }

            if (isOn)
            {
                StartClockLoop();
            }
            else
            {
                clockSource?.Stop();
            }
        }

        private void StartSwitchResidualDarkening()
        {
            if (!ambientLightingCached)
            {
                return;
            }
            if (ambientDarkeningRoutine != null)
            {
                StopCoroutine(ambientDarkeningRoutine);
            }
            ambientDarkeningRoutine = StartCoroutine(SwitchResidualDarkeningRoutine());
        }

        private IEnumerator SwitchResidualDarkeningRoutine()
        {
            Color ambientLightStart = RenderSettings.ambientLight;
            Color ambientSkyStart = RenderSettings.ambientSkyColor;
            Color ambientEquatorStart = RenderSettings.ambientEquatorColor;
            Color ambientGroundStart = RenderSettings.ambientGroundColor;
            float reflectionStart = RenderSettings.reflectionIntensity;

            Color ambientLightTarget = ScaleRgb(initialAmbientLight, switchResidualLightMultiplier);
            Color ambientSkyTarget = ScaleRgb(initialAmbientSkyColor, switchResidualLightMultiplier);
            Color ambientEquatorTarget = ScaleRgb(initialAmbientEquatorColor, switchResidualLightMultiplier);
            Color ambientGroundTarget = ScaleRgb(initialAmbientGroundColor, switchResidualLightMultiplier);
            float reflectionTarget = initialReflectionIntensity * switchResidualLightMultiplier;
            float elapsed = 0f;

            while (elapsed < switchResidualDarkeningDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / switchResidualDarkeningDuration);
                t = t * t * (3f - 2f * t);
                RenderSettings.ambientLight = Color.Lerp(ambientLightStart, ambientLightTarget, t);
                RenderSettings.ambientSkyColor = Color.Lerp(ambientSkyStart, ambientSkyTarget, t);
                RenderSettings.ambientEquatorColor = Color.Lerp(ambientEquatorStart, ambientEquatorTarget, t);
                RenderSettings.ambientGroundColor = Color.Lerp(ambientGroundStart, ambientGroundTarget, t);
                RenderSettings.reflectionIntensity = Mathf.Lerp(reflectionStart, reflectionTarget, t);
                yield return null;
            }

            RenderSettings.ambientLight = ambientLightTarget;
            RenderSettings.ambientSkyColor = ambientSkyTarget;
            RenderSettings.ambientEquatorColor = ambientEquatorTarget;
            RenderSettings.ambientGroundColor = ambientGroundTarget;
            RenderSettings.reflectionIntensity = reflectionTarget;
            ambientDarkeningRoutine = null;
        }

        private void RestoreAmbientLighting()
        {
            if (!ambientLightingCached)
            {
                return;
            }
            if (ambientDarkeningRoutine != null)
            {
                StopCoroutine(ambientDarkeningRoutine);
                ambientDarkeningRoutine = null;
            }
            RenderSettings.ambientLight = initialAmbientLight;
            RenderSettings.ambientSkyColor = initialAmbientSkyColor;
            RenderSettings.ambientEquatorColor = initialAmbientEquatorColor;
            RenderSettings.ambientGroundColor = initialAmbientGroundColor;
            RenderSettings.reflectionIntensity = initialReflectionIntensity;
        }

        private void SetAmbientLightingMultiplier(float multiplier)
        {
            if (!ambientLightingCached)
            {
                return;
            }

            if (ambientDarkeningRoutine != null)
            {
                StopCoroutine(ambientDarkeningRoutine);
                ambientDarkeningRoutine = null;
            }

            multiplier = Mathf.Clamp01(multiplier);
            RenderSettings.ambientLight = ScaleRgb(initialAmbientLight, multiplier);
            RenderSettings.ambientSkyColor = ScaleRgb(initialAmbientSkyColor, multiplier);
            RenderSettings.ambientEquatorColor = ScaleRgb(initialAmbientEquatorColor, multiplier);
            RenderSettings.ambientGroundColor = ScaleRgb(initialAmbientGroundColor, multiplier);
            RenderSettings.reflectionIntensity = initialReflectionIntensity * multiplier;
        }

        private static Color ScaleRgb(Color color, float multiplier)
        {
            return new Color(
                color.r * multiplier,
                color.g * multiplier,
                color.b * multiplier,
                color.a);
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
            RestoreAmbientLighting();
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
            SetCeilingEmissionMultiplier(1f);
            if (ambientSource != null)
            {
                SetAudioVolume(ambientSource, ref ambientLogicalVolume, initialAmbientVolume);
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
            secondRoomWindowWatcher?.ShowFaintUntilDismissed();
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
            secondRoomWindowWatcher?.FadeOutFaintPresence();
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
                SetAudioVolume(ambientSource, ref ambientLogicalVolume, initialAmbientVolume * 0.35f);
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
                SetAudioVolume(ambientSource, ref ambientLogicalVolume, 0f);
            }
            clockSource?.Stop();
        }

        private void ResumeAtmosphere()
        {
            sensoryFrozen = false;
            microFlickerPaused = false;
            if (ambientSource != null)
            {
                SetAudioVolume(ambientSource, ref ambientLogicalVolume, initialAmbientVolume);
                if (!ambientSource.isPlaying)
                {
                    ambientSource.Play();
                }
            }
            StartClockLoop();
        }

        private void ArmWindowVision()
        {
            if (windowCardScareRoutine != null)
            {
                StopCoroutine(windowCardScareRoutine);
                windowCardScareRoutine = null;
            }
            windowGazeElapsed = 0f;
            windowVisionArmed = false;
            windowVision?.SetActive(false);

            ImpossibleWindowWatcher watcher = secondRoomWindowWatcher;
            if (watcher == null)
            {
                windowVisionArmed = true;
                return;
            }

            windowCardScareRoutine = StartCoroutine(WindowCardScareRoutine(watcher));
        }

        private IEnumerator WindowCardScareRoutine(ImpossibleWindowWatcher watcher)
        {
            yield return new WaitForSecondsRealtime(WindowCardScareDelaySeconds);
            watcher.TriggerScriptedScare();
            PlayOneShot(windowFaceLungeClip, 0.86f);
            yield return new WaitForSecondsRealtime(WindowCardScareDurationSeconds);
            SetFact(windowVisionSeenFact, true);
            runner?.RequestExternalAdvance();
            windowCardScareRoutine = null;
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
                SetAudioVolume(transitionSource, ref transitionLogicalVolume, 0.18f);
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
            narrativeFlickerAmplitude = huntFlickerAmplitude;
            horrorEmissionOverride = true;
            horrorCeilingMultiplier = huntCeilingMultiplier;
            SetCeilingEmissionMultiplier(horrorCeilingMultiplier);
            StartLightFade(
                initialLampIntensity * huntLightMultiplier,
                initialSecondLampIntensity * huntLightMultiplier,
                6f);
            if (transitionSource != null && threatBreathingClip != null)
            {
                transitionSource.clip = threatBreathingClip;
                transitionSource.loop = true;
                SetAudioVolume(transitionSource, ref transitionLogicalVolume, 0f);
                transitionSource.Play();
            }
            float elapsed = 0f;
            const float duration = 3f;
            float ambientStart = ambientSource != null ? ambientLogicalVolume : 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (ambientSource != null)
                {
                    SetAudioVolume(ambientSource, ref ambientLogicalVolume, Mathf.Lerp(ambientStart, 0f, t));
                }
                if (transitionSource != null)
                {
                    SetAudioVolume(transitionSource, ref transitionLogicalVolume, Mathf.Lerp(0f, 0.2f, t));
                }
                yield return null;
            }
        }

        private void StartHunt(bool close)
        {
            if (!close)
            {
                perceptionOverlay?.BeginPeripheralHaunting();
            }
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
                SetAudioVolume(threatSource, ref threatLogicalVolume, close ? 0.31f : 0.14f);
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
                SetAudioVolume(threatSource, ref threatLogicalVolume, Mathf.Lerp(0.14f, 0.65f, shaped));
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
            float startVolume = threatSource != null ? threatLogicalVolume : 0f;
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
                    SetAudioVolume(threatSource, ref threatLogicalVolume, Mathf.Lerp(startVolume, 0f, t));
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
            perceptionOverlay?.StopAllEffects(0.18f);
            narrativeFlickerAmplitude = 0f;
            horrorEmissionOverride = false;
            horrorCeilingMultiplier = 1f;
            SetCeilingEmissionMultiplier(1f);
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
                SetAudioVolume(rearSource, ref rearLogicalVolume, 0.16f);
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
                SetAudioVolume(rearSource, ref rearLogicalVolume, 0.48f);
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
                        SetAudioVolume(rearSource, ref rearLogicalVolume, 0.36f + step * 0.04f);
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
            StartFinalDoorHorror();
            storyDoor?.OpenByStory(false);
            SetLightEnabled(exitLight, true);
            endingZone?.SetTriggerEnabled(true);
            if (windSource != null && windClip != null)
            {
                windSource.clip = windClip;
                windSource.loop = true;
                SetAudioVolume(windSource, ref windLogicalVolume, 0.12f);
                windSource.Play();
            }
            ArmExitPressure();
        }

        private void StartFinalDoorHorror()
        {
            if (finalDoorHorrorRoutine != null)
            {
                StopCoroutine(finalDoorHorrorRoutine);
            }

            finalDoorLightingOverride = true;
            finalDoorLightPulseOn = false;
            ApplyFinalDoorLightState(false);
            endingFaceInfestation?.BeginReveal(finalDoorFaceRevealDuration);
            PlayOneShot(finalDoorInfestationClip, 0.9f);
            finalDoorHorrorRoutine = StartCoroutine(FinalDoorHorrorRoutine());
        }

        private IEnumerator FinalDoorHorrorRoutine()
        {
            float nextPulseAt = Time.unscaledTime + finalDoorLightInterval;
            float pulseEndsAt = 0f;
            while (endingExitArmed && !endingActive)
            {
                float now = Time.unscaledTime;
                if (!finalDoorLightPulseOn && now >= nextPulseAt)
                {
                    finalDoorLightPulseOn = true;
                    pulseEndsAt = now + finalDoorLightPulseDuration;
                    nextPulseAt += finalDoorLightInterval;
                    ApplyFinalDoorLightState(true);
                }
                else if (finalDoorLightPulseOn && now >= pulseEndsAt)
                {
                    finalDoorLightPulseOn = false;
                    ApplyFinalDoorLightState(false);
                }

                yield return null;
            }

            finalDoorLightPulseOn = false;
            if (finalDoorLightingOverride)
            {
                ApplyFinalDoorLightState(false);
            }
            finalDoorHorrorRoutine = null;
        }

        private void StopFinalDoorHorror(bool clearFaces)
        {
            if (finalDoorHorrorRoutine != null)
            {
                StopCoroutine(finalDoorHorrorRoutine);
                finalDoorHorrorRoutine = null;
            }

            finalDoorLightingOverride = false;
            finalDoorLightPulseOn = false;
            if (clearFaces)
            {
                endingFaceInfestation?.ClearImmediately();
            }
        }

        private void ApplyFinalDoorLightState(bool lightsOn)
        {
            SetLightEnabled(lampLight, lightsOn);
            SetLightEnabled(secondRoomLampLight, lightsOn);
            if (lightsOn)
            {
                if (lampLight != null)
                {
                    lampLight.intensity = initialLampIntensity;
                }
                if (secondRoomLampLight != null)
                {
                    secondRoomLampLight.intensity = initialSecondLampIntensity;
                }
            }

            SetCeilingEmissionMultiplier(lightsOn ? 1f : 0f);
            SetAmbientLightingMultiplier(lightsOn ? 1f : 0f);
        }

        private void ArmExitPressure()
        {
            exitPressureActive = false;
            exitPressureSuppressed = false;
            exitPressureArmed = enableClimaxThreat && playerRoot != null && storyDoor != null;
            exitPressureArmedAt = Time.unscaledTime;
            exitTowardProgress = 0f;
            exitTrackingPositionValid = playerRoot != null;
            if (!exitTrackingPositionValid)
            {
                return;
            }

            exitPreviousPlayerPosition = playerRoot.position;
            exitPressureBaselineDistance = GetHorizontalExitDistance();
            exitPreviousDistance = exitPressureBaselineDistance;
        }

        private void UpdateExitPressure()
        {
            if (!endingExitArmed
                || !exitPressureArmed
                || exitPressureSuppressed
                || playerRoot == null
                || storyDoor == null)
            {
                return;
            }

            Vector3 currentPosition = playerRoot.position;
            float currentDistance = GetHorizontalExitDistance();
            float distanceStep = exitPreviousDistance - currentDistance;
            float movementTowardDoor = 0f;
            if (exitTrackingPositionValid)
            {
                Vector3 movement = currentPosition - exitPreviousPlayerPosition;
                movement.y = 0f;
                Vector3 toDoor = storyDoor.transform.position - currentPosition;
                toDoor.y = 0f;
                if (toDoor.sqrMagnitude > 0.001f)
                {
                    movementTowardDoor = Vector3.Dot(movement, toDoor.normalized);
                }
            }

            exitPreviousPlayerPosition = currentPosition;
            exitPreviousDistance = currentDistance;
            exitTrackingPositionValid = true;
            exitTowardProgress = Mathf.Max(
                exitTowardProgress,
                exitPressureBaselineDistance - currentDistance);

            float immediateMovementThreshold = Mathf.Max(0.006f, exitMovementThreshold * 0.075f);
            bool movingTowardDoor = distanceStep > immediateMovementThreshold
                || movementTowardDoor > immediateMovementThreshold;
            if (exitTowardProgress >= exitMovementThreshold
                || (exitPressureActive && movingTowardDoor))
            {
                CancelExitPressure(true);
                return;
            }

            if (!exitPressureActive
                && Time.unscaledTime - exitPressureArmedAt >= exitPressureDelay)
            {
                BeginExitPressure();
            }
        }

        private bool IsClimaxHardCutSilent => exitPressureActive
            && Time.unscaledTime < climaxSilenceUntil;

        private void BeginExitPressure()
        {
            if (exitPressureActive || exitPressureSuppressed)
            {
                return;
            }

            exitPressureActive = true;
            exitPrePressurePrimaryLight = primaryLightBase;
            exitPrePressureSecondLight = secondLightBase;
            narrativeFlickerAmplitude = Mathf.Max(narrativeFlickerAmplitude, huntFlickerAmplitude);
            horrorEmissionOverride = true;
            horrorCeilingMultiplier = Mathf.Min(huntCeilingMultiplier, 0.18f);
            nextClimaxStrobeAt = Time.unscaledTime + 0.08f;
            climaxStrobeUntil = 0f;
            climaxStrobeMultiplier = 1f;
            climaxSilenceUntil = 0f;
            perceptionOverlay?.BeginClimaxPressure();
            StartHunt(true);
            StartLightFade(
                exitPrePressurePrimaryLight * 0.52f,
                exitPrePressureSecondLight * 0.52f,
                0.4f);
        }

        private void CancelExitPressure(bool suppress)
        {
            bool wasActive = exitPressureActive;
            exitPressureArmed = false;
            exitPressureActive = false;
            exitPressureSuppressed = suppress;
            exitTrackingPositionValid = false;
            narrativeFlickerAmplitude = 0f;
            horrorEmissionOverride = false;
            horrorCeilingMultiplier = 1f;
            climaxStrobeMultiplier = 1f;
            climaxSilenceUntil = 0f;
            exitFovImpulse = 0f;

            if (exitWindPulseRoutine != null)
            {
                StopCoroutine(exitWindPulseRoutine);
                exitWindPulseRoutine = null;
            }
            if (endingExitArmed && windSource != null && windClip != null)
            {
                if (!windSource.isPlaying)
                {
                    windSource.clip = windClip;
                    windSource.loop = true;
                    windSource.Play();
                }
                SetAudioVolume(windSource, ref windLogicalVolume, 0.12f);
            }

            if (!wasActive)
            {
                return;
            }

            perceptionOverlay?.StopAllEffects(0.12f);
            if (!finalDoorLightingOverride)
            {
                SetCeilingEmissionMultiplier(1f);
                StartLightFade(exitPrePressurePrimaryLight, exitPrePressureSecondLight, 0.3f);
            }
            if (huntActive || (threatSilhouette != null && threatSilhouette.gameObject.activeSelf))
            {
                DismissThreat(0.35f);
            }
        }

        private float GetHorizontalExitDistance()
        {
            if (playerRoot == null || storyDoor == null)
            {
                return 0f;
            }

            Vector3 offset = storyDoor.transform.position - playerRoot.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void HandleClimaxHardCut()
        {
            if (!exitPressureActive)
            {
                return;
            }

            climaxSilenceUntil = Time.unscaledTime + 0.42f;
            exitFovImpulse = -6f;
            StartCoroutine(CameraShakeRoutine(0.016f, 0.34f));
            if (exitWindPulseRoutine != null)
            {
                StopCoroutine(exitWindPulseRoutine);
            }
            exitWindPulseRoutine = StartCoroutine(ExitWindPulseRoutine());
        }

        private IEnumerator ExitWindPulseRoutine()
        {
            if (windSource != null)
            {
                SetAudioVolume(windSource, ref windLogicalVolume, 0f);
            }
            yield return new WaitForSecondsRealtime(0.42f);

            if (!exitPressureActive || windSource == null || windClip == null)
            {
                exitWindPulseRoutine = null;
                yield break;
            }

            windSource.clip = windClip;
            windSource.loop = true;
            if (!windSource.isPlaying)
            {
                windSource.Play();
            }
            const float peakVolume = 0.48f;
            const float restingVolume = 0.16f;
            const float duration = 1.35f;
            SetAudioVolume(windSource, ref windLogicalVolume, peakVolume);
            float elapsed = 0f;
            while (elapsed < duration && exitPressureActive)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                SetAudioVolume(
                    windSource,
                    ref windLogicalVolume,
                    Mathf.Lerp(peakVolume, restingVolume, progress));
                yield return null;
            }
            exitWindPulseRoutine = null;
        }

        private void UpdateExitCamera()
        {
            if (playerCamera == null || endingActive)
            {
                return;
            }
            exitFovImpulse = Mathf.MoveTowards(exitFovImpulse, 0f, Time.unscaledDeltaTime * 11f);
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
            target += exitFovImpulse;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, Time.deltaTime * 5f);
        }

        private void EnterEndingExit()
        {
            if (!endingExitArmed)
            {
                return;
            }
            CancelExitPressure(true);
            endingZone?.SetTriggerEnabled(false);
            SetFact(leftRoomFact, true);
            runner?.RequestExternalAdvance();
        }

        private void BeginEndingTransition()
        {
            if (endingTransitionRoutine != null)
            {
                return;
            }

            endingActive = true;
            if (movementController != null)
            {
                movementController.enabled = false;
            }
            if (playerFootsteps != null)
            {
                playerFootsteps.enabled = false;
            }
            primaryPresenter?.SetPresentationBlocked(true);
            secondRoomPresenter?.SetPresentationBlocked(true);

            if (endingRoutine != null)
            {
                StopCoroutine(endingRoutine);
                endingRoutine = null;
            }
            endingTransitionRoutine = StartCoroutine(EndingTransitionRoutine());
        }

        private IEnumerator EndingTransitionRoutine()
        {
            if (screenFade == null)
            {
                PrepareEndingReset();
                endingRoutine = StartCoroutine(EndingZoomRoutine());
                yield return null;
                endingTransitionRoutine = null;
                yield break;
            }

            SetScreenFadeColor(Color.white);
            screenFade.blocksRaycasts = true;
            screenFade.interactable = false;
            yield return FadeScreenAlpha(1f, EndingTransitionFadeSeconds);

            screenFade.alpha = 1f;
            PrepareEndingReset();
            endingRoutine = StartCoroutine(EndingZoomRoutine());
            yield return FadeScreenAlpha(0f, EndingTransitionFadeSeconds);

            screenFade.alpha = 0f;
            screenFade.blocksRaycasts = false;
            RestoreScreenFadeColor();
            endingTransitionRoutine = null;
        }

        private IEnumerator FadeScreenAlpha(float targetAlpha, float duration)
        {
            float startAlpha = screenFade != null ? screenFade.alpha : targetAlpha;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                t = t * t * (3f - 2f * t);
                if (screenFade != null)
                {
                    screenFade.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                }
                yield return null;
            }
            if (screenFade != null)
            {
                screenFade.alpha = targetAlpha;
            }
        }

        private void SetScreenFadeColor(Color color)
        {
            if (screenFadeGraphic == null)
            {
                return;
            }
            color.a = initialScreenFadeColor.a;
            screenFadeGraphic.color = color;
        }

        private void RestoreScreenFadeColor()
        {
            if (screenFadeGraphic != null)
            {
                screenFadeGraphic.color = initialScreenFadeColor;
            }
        }

        private void PrepareEndingReset()
        {
            endingActive = true;
            StopFinalDoorHorror(false);
            CancelExitPressure(false);
            perceptionOverlay?.StopAllEffects(0f);
            if (lightFadeRoutine != null)
            {
                StopCoroutine(lightFadeRoutine);
                lightFadeRoutine = null;
            }
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
            SetPlayerVisualsVisible(false);
            SetEndingTopViewActive(true);
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
            RestoreAmbientLighting();
            SetLightEnabled(lampLight, true);
            SetLightEnabled(secondRoomLampLight, false);
            SetCeilingEmissionMultiplier(1f);
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
                SetAudioVolume(ambientSource, ref ambientLogicalVolume, initialAmbientVolume);
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
                primaryPresenter.ResetNextCardLayoutOffset();
                primaryPresenter.SetDeckThicknessMultiplier(3.7f);
                runner.SetPresenter(primaryPresenter, false);
            }
            primaryPresenter?.SetPresentationBlocked(false);
            secondRoomPresenter?.SetPresentationBlocked(false);
            if (playerCamera != null)
            {
                playerCamera.fieldOfView = initialCameraFov;
            }
        }

        private IEnumerator EndingZoomRoutine()
        {
            Camera endingCamera = endingTopViewCamera != null && endingTopViewCamera.enabled
                ? endingTopViewCamera
                : playerCamera;
            float startFov = endingCamera != null ? endingCamera.fieldOfView : initialCameraFov;
            float startOrthographicSize = endingCamera != null
                ? endingCamera.orthographicSize
                : initialEndingTopViewOrthographicSize;
            float targetOrthographicSize = Mathf.Min(
                startOrthographicSize,
                endingTopViewZoomSize);
            float elapsed = 0f;
            while (elapsed < EndingDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / EndingDurationSeconds);
                t = t * t * (3f - 2f * t);
                if (endingCamera != null)
                {
                    if (endingCamera.orthographic)
                    {
                        endingCamera.orthographicSize = Mathf.Lerp(
                            startOrthographicSize,
                            targetOrthographicSize,
                            t);
                    }
                    else
                    {
                        endingCamera.fieldOfView = Mathf.Lerp(startFov, 24f, t);
                    }
                }

                if (screenFade != null
                    && elapsed >= EndingDurationSeconds - EndingFadeSeconds)
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

        private void CachePlayerVisualState()
        {
            playerVisualRenderers = playerRoot != null
                ? playerRoot.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            initialPlayerVisualRendererStates = new bool[playerVisualRenderers.Length];
            for (int index = 0; index < playerVisualRenderers.Length; index++)
            {
                Renderer playerRenderer = playerVisualRenderers[index];
                initialPlayerVisualRendererStates[index] = playerRenderer != null && playerRenderer.enabled;
            }
        }

        private void SetPlayerVisualsVisible(bool visible)
        {
            for (int index = 0; index < playerVisualRenderers.Length; index++)
            {
                Renderer playerRenderer = playerVisualRenderers[index];
                if (playerRenderer == null)
                {
                    continue;
                }

                playerRenderer.enabled = visible && initialPlayerVisualRendererStates[index];
            }
        }

        private void SetEndingTopViewActive(bool active)
        {
            if (active)
            {
                if (endingTopViewCamera == null)
                {
                    return;
                }
                if (playerAudioListener != null)
                {
                    playerAudioListener.enabled = false;
                }
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                }
                endingTopViewCamera.gameObject.SetActive(true);
                endingTopViewCamera.enabled = true;
                if (endingTopViewAudioListener != null)
                {
                    endingTopViewAudioListener.enabled = true;
                }
                return;
            }

            if (endingTopViewAudioListener != null)
            {
                endingTopViewAudioListener.enabled = false;
            }
            if (endingTopViewCamera != null)
            {
                endingTopViewCamera.enabled = false;
                endingTopViewCamera.fieldOfView = initialEndingTopViewFov;
                endingTopViewCamera.orthographicSize = initialEndingTopViewOrthographicSize;
                endingTopViewCamera.gameObject.SetActive(false);
            }
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                playerCamera.fieldOfView = initialCameraFov;
            }
            if (playerAudioListener != null)
            {
                playerAudioListener.enabled = true;
            }
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
            perceptionOverlay?.StopAllEffects(0.12f);
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
                    SetAudioVolume(rearSource, ref rearLogicalVolume, 0.09f);
                    rearSource.PlayOneShot(floorCreakClip, 0.18f);
                }
            }
        }

        private void UpdateLampFlicker()
        {
            if (finalDoorLightingOverride)
            {
                if (finalDoorLightPulseOn)
                {
                    if (lampLight != null && lampLight.enabled)
                    {
                        lampLight.intensity = initialLampIntensity;
                    }
                    if (secondRoomLampLight != null && secondRoomLampLight.enabled)
                    {
                        secondRoomLampLight.intensity = initialSecondLampIntensity;
                    }
                }
                return;
            }

            float flicker = 1f;
            if (!microFlickerPaused && !sensoryFrozen)
            {
                float slow = Mathf.PerlinNoise(Time.unscaledTime * 2.17f, 0.31f) - 0.5f;
                float fast = Mathf.PerlinNoise(Time.unscaledTime * 7.91f, 0.73f) - 0.5f;
                float amplitude = flickerAmplitude + narrativeFlickerAmplitude;
                flicker += (slow * 1.45f + fast * 0.55f) * amplitude;
            }

            float strobe = 1f;
            float now = Time.unscaledTime;
            if (exitPressureActive)
            {
                if (now < climaxSilenceUntil)
                {
                    strobe = 0f;
                }
                else
                {
                    float intensity = perceptionOverlay != null
                        ? perceptionOverlay.ClimaxIntensity
                        : 0.5f;
                    if (now >= nextClimaxStrobeAt)
                    {
                        climaxStrobeMultiplier = UnityEngine.Random.value < 0.78f
                            ? UnityEngine.Random.Range(0.035f, 0.26f)
                            : UnityEngine.Random.Range(1.05f, 1.28f);
                        climaxStrobeUntil = now + UnityEngine.Random.Range(0.035f, 0.095f);
                        nextClimaxStrobeAt = climaxStrobeUntil
                            + UnityEngine.Random.Range(
                                Mathf.Lerp(0.34f, 0.08f, intensity),
                                Mathf.Lerp(0.62f, 0.18f, intensity));
                    }
                    if (now < climaxStrobeUntil)
                    {
                        strobe = climaxStrobeMultiplier;
                    }
                }
            }

            flicker *= strobe;
            if (lampLight != null && lampLight.enabled)
            {
                lampLight.intensity = primaryLightBase * flicker * flickerDipMultiplier;
            }
            if (secondRoomLampLight != null && secondRoomLampLight.enabled)
            {
                secondRoomLampLight.intensity = secondLightBase * flicker * flickerDipMultiplier;
            }
            if (horrorEmissionOverride)
            {
                SetCeilingEmissionMultiplier(
                    horrorCeilingMultiplier * Mathf.Clamp(flicker * flickerDipMultiplier, 0f, 1.25f));
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

        private void CacheCeilingEmissionBindings()
        {
            ceilingEmissionBindings.Clear();
            foreach (Renderer surfaceRenderer in ceilingSurfaceRenderers)
            {
                if (surfaceRenderer == null)
                {
                    continue;
                }

                Material[] materials = surfaceRenderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null
                        || !material.HasProperty(CeilingEmissionColorId)
                        || !material.HasProperty(CeilingEmissionMapId)
                        || material.GetTexture(CeilingEmissionMapId) == null)
                    {
                        continue;
                    }

                    ceilingEmissionBindings.Add(new CeilingEmissionBinding
                    {
                        Renderer = surfaceRenderer,
                        MaterialIndex = materialIndex,
                        InitialColor = ScaleRgb(
                            material.GetColor(CeilingEmissionColorId),
                            litStateBrightnessMultiplier),
                        PropertyBlock = new MaterialPropertyBlock()
                    });
                }
            }
        }

        private void SetCeilingEmissionMultiplier(float multiplier)
        {
            multiplier = Mathf.Clamp01(multiplier);
            foreach (CeilingEmissionBinding binding in ceilingEmissionBindings)
            {
                if (binding.Renderer == null)
                {
                    continue;
                }

                binding.Renderer.GetPropertyBlock(binding.PropertyBlock, binding.MaterialIndex);
                binding.PropertyBlock.SetColor(
                    CeilingEmissionColorId,
                    ScaleRgb(binding.InitialColor, multiplier));
                binding.Renderer.SetPropertyBlock(binding.PropertyBlock, binding.MaterialIndex);
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
                oneShotSource.PlayOneShot(clip, volume * SfxVolume.Scale);
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
            ceilingSurfaceRenderers ??= Array.Empty<Renderer>();
            litStateBrightnessMultiplier = Mathf.Clamp(litStateBrightnessMultiplier, 0.1f, 1f);
            flickerAmplitude = Mathf.Clamp(flickerAmplitude, 0f, 0.05f);
            huntLightMultiplier = Mathf.Clamp(huntLightMultiplier, 0.15f, 0.3f);
            huntCeilingMultiplier = Mathf.Clamp(huntCeilingMultiplier, 0.15f, 0.35f);
            huntFlickerAmplitude = Mathf.Clamp(huntFlickerAmplitude, 0f, 0.05f);
            exitPressureDelay = Mathf.Clamp(exitPressureDelay, 5f, 6f);
            exitMovementThreshold = Mathf.Max(0.02f, exitMovementThreshold);
            finalDoorLightInterval = Mathf.Max(0.1f, finalDoorLightInterval);
            finalDoorLightPulseDuration = Mathf.Clamp(finalDoorLightPulseDuration, 0.02f, 0.25f);
            finalDoorFaceRevealDuration = Mathf.Max(0.1f, finalDoorFaceRevealDuration);
            switchResidualDarkeningDuration = Mathf.Max(0.05f, switchResidualDarkeningDuration);
            switchResidualLightMultiplier = Mathf.Clamp01(switchResidualLightMultiplier);
            threatApproachDuration = Mathf.Max(1f, threatApproachDuration);
            focusedLookDot = Mathf.Clamp(focusedLookDot, 0.5f, 0.999f);
            windowGazeDuration = Mathf.Max(0.1f, windowGazeDuration);
            rearImpactAngle = Mathf.Clamp(rearImpactAngle, 90f, 179f);
            endingTopViewZoomSize = Mathf.Max(0.1f, endingTopViewZoomSize);
        }
    }
}

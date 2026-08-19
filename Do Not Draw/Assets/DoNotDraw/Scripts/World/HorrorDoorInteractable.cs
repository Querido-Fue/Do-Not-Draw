using System;
using System.Collections;
using DoNotDraw.Interaction;
using UnityEngine;

namespace DoNotDraw.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class HorrorDoorInteractable : PlayerInteractableBehaviour
    {
        [SerializeField] private Transform pivot;
        [SerializeField] private Transform handle;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private string prompt = "[F]  OPEN DOOR";
        [SerializeField] private bool interactionEnabled;
        [SerializeField] private float openAngle = 96f;
        [SerializeField] private float partialOpenAngle = 14f;
        [SerializeField, Min(0.05f)] private float openDuration = 1.1f;
        [SerializeField, Min(0.05f)] private float slamDuration = 0.22f;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip storyOpenSound;
        [SerializeField] private AudioClip slamSound;
        [SerializeField] private AudioClip handleTurnSound;
        [SerializeField] private AudioClip storyHandleTurnSound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.75f;

        private AudioSource audioSource;
        private Quaternion closedRotation;
        private Quaternion handleRestRotation;
        private Coroutine animationRoutine;
        private Coroutine handleRoutine;
        private bool initialized;
        private bool isOpen;
        private bool isMoving;

        public event Action<HorrorDoorInteractable> PlayerOpened;

        public override bool CanInteract => base.CanInteract
            && interactionEnabled
            && !isMoving
            && !isOpen;
        public override string InteractionPrompt => prompt;
        public override Vector3 InteractionPoint => interactionPoint != null
            ? interactionPoint.position
            : transform.position;
        public override float InteractionPriority => 2f;
        public bool IsOpen => isOpen;

        private void Awake()
        {
            Initialize();
        }

        public override void Interact(PlayerInteractionRouter router)
        {
            if (!CanExecuteInteraction)
            {
                return;
            }

            interactionEnabled = false;
            PlayHandleSound(handleTurnSound);
            AnimateTo(openAngle, openDuration, openSound, true);
            PlayerOpened?.Invoke(this);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        public void SnapClosed()
        {
            Initialize();
            StopAnimation();
            pivot.localRotation = closedRotation;
            if (handle != null)
            {
                handle.localRotation = handleRestRotation;
            }
            isOpen = false;
            isMoving = false;
        }

        public void OpenByStory(bool playSound = true)
        {
            AudioClip clip = storyOpenSound != null ? storyOpenSound : openSound;
            AnimateTo(openAngle, openDuration, playSound ? clip : null, true);
        }

        public void OpenPartially()
        {
            AnimateTo(partialOpenAngle, openDuration * 1.4f, openSound, true);
        }

        public void CloseSoftly()
        {
            AnimateTo(0f, openDuration, openSound, false);
        }

        public void CloseWithSlam()
        {
            AnimateTo(0f, slamDuration, slamSound, false);
        }

        public void TwistHandleByStory(float duration = 2f, float angle = 45f)
        {
            Initialize();
            if (handle == null)
            {
                PlayHandleSound(storyHandleTurnSound != null
                    ? storyHandleTurnSound
                    : handleTurnSound);
                return;
            }

            if (handleRoutine != null)
            {
                StopCoroutine(handleRoutine);
            }

            handleRoutine = StartCoroutine(TwistHandleRoutine(Mathf.Max(0.1f, duration), angle));
        }

        private void AnimateTo(float angle, float duration, AudioClip clip, bool openState)
        {
            Initialize();
            StopAnimation();
            animationRoutine = StartCoroutine(AnimateRoutine(angle, duration, clip, openState));
        }

        private IEnumerator AnimateRoutine(float angle, float duration, AudioClip clip, bool openState)
        {
            isMoving = true;
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }

            Quaternion start = pivot.localRotation;
            Quaternion target = closedRotation * Quaternion.Euler(0f, angle, 0f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                pivot.localRotation = Quaternion.Slerp(start, target, t);
                yield return null;
            }

            pivot.localRotation = target;
            isOpen = openState;
            isMoving = false;
            animationRoutine = null;
        }

        private IEnumerator TwistHandleRoutine(float duration, float angle)
        {
            PlayHandleSound(storyHandleTurnSound != null
                ? storyHandleTurnSound
                : handleTurnSound);

            Quaternion start = handle.localRotation;
            Quaternion target = handleRestRotation * Quaternion.Euler(0f, 0f, angle);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float shaped;
                if (t < 0.42f)
                {
                    shaped = Mathf.SmoothStep(0f, 0.52f, t / 0.42f);
                }
                else if (t < 0.58f)
                {
                    shaped = 0.52f;
                }
                else
                {
                    shaped = Mathf.SmoothStep(0.52f, 1f, (t - 0.58f) / 0.42f);
                }

                handle.localRotation = Quaternion.Slerp(start, target, shaped);
                yield return null;
            }

            handle.localRotation = target;
            handleRoutine = null;
        }

        private void PlayHandleSound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume * 0.7f);
            }
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            pivot ??= transform;
            audioSource = GetComponent<AudioSource>();
            closedRotation = pivot.localRotation;
            if (handle != null)
            {
                handleRestRotation = handle.localRotation;
            }
            initialized = true;
        }

        private void StopAnimation()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }


            if (handleRoutine != null)
            {
                StopCoroutine(handleRoutine);
                handleRoutine = null;
            }
        }

        private void OnValidate()
        {
            pivot ??= transform;
            prompt ??= string.Empty;
            openDuration = Mathf.Max(0.05f, openDuration);
            slamDuration = Mathf.Max(0.05f, slamDuration);
            volume = Mathf.Clamp01(volume);
        }
    }
}

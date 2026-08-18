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
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private string prompt = "[F]  OPEN DOOR";
        [SerializeField] private bool interactionEnabled;
        [SerializeField] private float openAngle = 96f;
        [SerializeField] private float partialOpenAngle = 14f;
        [SerializeField, Min(0.05f)] private float openDuration = 1.1f;
        [SerializeField, Min(0.05f)] private float slamDuration = 0.22f;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip slamSound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.75f;

        private AudioSource audioSource;
        private Quaternion closedRotation;
        private Coroutine animationRoutine;
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
            OpenByStory();
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
            isOpen = false;
            isMoving = false;
        }

        public void OpenByStory()
        {
            AnimateTo(openAngle, openDuration, openSound, true);
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

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            pivot ??= transform;
            audioSource = GetComponent<AudioSource>();
            closedRotation = pivot.localRotation;
            initialized = true;
        }

        private void StopAnimation()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
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

using System;
using System.Collections;
using DoNotDraw.Interaction;
using UnityEngine;

namespace DoNotDraw.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class HorrorLightSwitchInteractable : PlayerInteractableBehaviour
    {
        [SerializeField] private Transform lever;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private string prompt = "[F]  USE LIGHT SWITCH";
        [SerializeField] private bool interactionEnabled;
        [SerializeField] private float pressedAngle = -28f;
        [SerializeField, Min(0.05f)] private float animationDuration = 0.16f;
        [SerializeField] private AudioClip switchSound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private AudioSource audioSource;
        private Quaternion initialRotation;
        private bool activated;

        public event Action<HorrorLightSwitchInteractable> Activated;

        public override bool CanInteract => base.CanInteract && interactionEnabled && !activated;
        public override string InteractionPrompt => prompt;
        public override Vector3 InteractionPoint => interactionPoint != null
            ? interactionPoint.position
            : transform.position;
        public override float InteractionPriority => 3f;

        private void Awake()
        {
            lever ??= transform;
            audioSource = GetComponent<AudioSource>();
            initialRotation = lever.localRotation;
        }

        public override void Interact(PlayerInteractionRouter router)
        {
            if (!CanExecuteInteraction)
            {
                return;
            }

            activated = true;
            interactionEnabled = false;
            if (audioSource != null && switchSound != null)
            {
                audioSource.PlayOneShot(switchSound, volume);
            }

            StartCoroutine(AnimateLever());
            Activated?.Invoke(this);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        public void ResetSwitch()
        {
            StopAllCoroutines();
            activated = false;
            if (lever != null)
            {
                lever.localRotation = initialRotation;
            }
        }

        private IEnumerator AnimateLever()
        {
            Quaternion start = lever.localRotation;
            Quaternion target = initialRotation * Quaternion.Euler(pressedAngle, 0f, 0f);
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                lever.localRotation = Quaternion.Slerp(start, target, t);
                yield return null;
            }

            lever.localRotation = target;
        }

        private void OnValidate()
        {
            lever ??= transform;
            prompt ??= string.Empty;
            animationDuration = Mathf.Max(0.05f, animationDuration);
            volume = Mathf.Clamp01(volume);
        }
    }
}

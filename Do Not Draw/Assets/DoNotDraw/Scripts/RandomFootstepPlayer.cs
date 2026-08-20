using StarterAssets;
using UnityEngine;

namespace DoNotDraw.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class RandomFootstepPlayer : MonoBehaviour
    {
        [Header("Footsteps")]
        [SerializeField] private AudioClip[] footstepClips = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] alternateFootstepClips = System.Array.Empty<AudioClip>();
        [SerializeField, Min(0.1f)] private float stepDistance = 1.55f;
        [SerializeField, Min(0f)] private float minimumSpeed = 0.2f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.06f);

        private CharacterController characterController;
        private FirstPersonController firstPersonController;
        private AudioSource audioSource;
        private float distanceSinceLastStep;
        private int lastClipIndex = -1;
        private bool wasMoving;
        private bool useAlternateFootsteps;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            firstPersonController = GetComponent<FirstPersonController>();
            audioSource = GetComponent<AudioSource>();
            ConfigureAudioSource();
        }

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
            ConfigureAudioSource();
        }

        private void Update()
        {
            Vector3 velocity = characterController.velocity;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            bool isGrounded = firstPersonController != null
                ? firstPersonController.Grounded
                : characterController.isGrounded;
            bool isMoving = isGrounded && horizontalSpeed > minimumSpeed;

            if (!isMoving)
            {
                wasMoving = false;
                distanceSinceLastStep = Mathf.Min(distanceSinceLastStep, stepDistance * 0.5f);
                return;
            }

            if (!wasMoving)
            {
                distanceSinceLastStep = Mathf.Max(distanceSinceLastStep, stepDistance * 0.45f);
                wasMoving = true;
            }

            distanceSinceLastStep += horizontalSpeed * Time.deltaTime;
            if (distanceSinceLastStep < stepDistance)
            {
                return;
            }

            distanceSinceLastStep %= stepDistance;
            PlayRandomFootstep();
        }

        private void PlayRandomFootstep()
        {
            AudioClip[] activeClips = useAlternateFootsteps
                && alternateFootstepClips is { Length: > 0 }
                    ? alternateFootstepClips
                    : footstepClips;
            if (activeClips == null || activeClips.Length == 0)
            {
                return;
            }

            int clipIndex = Random.Range(0, activeClips.Length);
            if (activeClips.Length > 1 && clipIndex == lastClipIndex)
            {
                clipIndex = (clipIndex + Random.Range(1, activeClips.Length)) % activeClips.Length;
            }

            AudioClip clip = activeClips[clipIndex];
            if (clip == null)
            {
                return;
            }

            lastClipIndex = clipIndex;
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip, volume * SfxVolume.Scale);
        }

        public void SetAlternateSurface(bool alternate)
        {
            if (useAlternateFootsteps == alternate)
            {
                return;
            }

            useAlternateFootsteps = alternate;
            distanceSinceLastStep = 0f;
            lastClipIndex = -1;
            wasMoving = false;
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;
            audioSource.priority = 96;
        }

        private void OnDisable()
        {
            distanceSinceLastStep = 0f;
            wasMoving = false;

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.pitch = 1f;
            }
        }

        private void OnValidate()
        {
            stepDistance = Mathf.Max(0.1f, stepDistance);
            minimumSpeed = Mathf.Max(0f, minimumSpeed);
            volume = Mathf.Clamp01(volume);

            if (pitchRange.x > pitchRange.y)
            {
                pitchRange = new Vector2(pitchRange.y, pitchRange.x);
            }
        }
    }
}

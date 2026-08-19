using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoNotDraw.Narrative
{
    public enum CardTypographyStage
    {
        Clean,
        Uneven,
        Damaged,
        DoubleExposure
    }

    [CreateAssetMenu(fileName = "CardDefinition", menuName = "Do Not Draw/Narrative/Card Definition")]
    public sealed class CardDefinition : ScriptableObject
    {
        [SerializeField] private string stableId = "card.unassigned";
        [SerializeField] private string displayName = "Unassigned Card";
        [SerializeField, TextArea(2, 6)] private string faceText;
        [SerializeField] private Material faceAccentMaterial;
        [SerializeField] private Color faceTextColor = new Color(0.055f, 0.035f, 0.025f, 1f);
        [SerializeField] private AudioClip drawSoundOverride;
        [SerializeField] private AudioClip landingSoundOverride;
        [SerializeField] private AudioClip voiceClip;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.8f;
        [SerializeField, Min(0f)] private float voiceDelay = 0.12f;
        [Header("Presentation")]
        [SerializeField, Min(0f)] private float textFadeDuration = 0.28f;
        [SerializeField] private CardTypographyStage typographyStage;
        [SerializeField, Min(0f)] private float doubleExposureDuration;
        [SerializeField] private bool liftOnReveal;
        [SerializeField, Min(0f)] private float revealLiftHeight = 0.045f;
        [SerializeField, Min(0f)] private float revealLiftDuration = 0.4f;
        [SerializeField] private List<string> tags = new List<string>();

        public string StableId => stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string FaceText => faceText ?? string.Empty;
        public Material FaceAccentMaterial => faceAccentMaterial;
        public Color FaceTextColor => faceTextColor;
        public AudioClip DrawSoundOverride => drawSoundOverride;
        public AudioClip LandingSoundOverride => landingSoundOverride;
        public AudioClip VoiceClip => voiceClip;
        public float VoiceVolume => voiceVolume;
        public float VoiceDelay => voiceDelay;
        public float TextFadeDuration => textFadeDuration;
        public CardTypographyStage TypographyStage => typographyStage;
        public float DoubleExposureDuration => doubleExposureDuration;
        public bool LiftOnReveal => liftOnReveal;
        public float RevealLiftHeight => revealLiftHeight;
        public float RevealLiftDuration => revealLiftDuration;
        public IReadOnlyList<string> Tags => tags != null
            ? (IReadOnlyList<string>)tags
            : Array.Empty<string>();

        private void OnValidate()
        {
            stableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
            displayName = displayName?.Trim() ?? string.Empty;
            faceText ??= string.Empty;
            voiceVolume = Mathf.Clamp01(voiceVolume);
            voiceDelay = Mathf.Max(0f, voiceDelay);
            textFadeDuration = Mathf.Max(0f, textFadeDuration);
            doubleExposureDuration = Mathf.Max(0f, doubleExposureDuration);
            revealLiftHeight = Mathf.Max(0f, revealLiftHeight);
            revealLiftDuration = Mathf.Max(0f, revealLiftDuration);
            tags ??= new List<string>();

            for (int index = 0; index < tags.Count; index++)
            {
                tags[index] = tags[index]?.Trim() ?? string.Empty;
            }
        }
    }
}

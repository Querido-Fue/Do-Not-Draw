using UnityEngine;

namespace DoNotDraw.Narrative
{
    public static class CardFaceTextureApplicator
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static bool TryApply(Renderer targetRenderer, Texture2D faceTexture)
        {
            if (targetRenderer == null || faceTexture == null)
            {
                return false;
            }

            Material material = targetRenderer.sharedMaterial;
            if (material == null)
            {
                return false;
            }

            bool hasBaseMap = material.HasProperty(BaseMapId);
            bool hasMainTexture = material.HasProperty(MainTextureId);
            if (!hasBaseMap && !hasMainTexture)
            {
                return false;
            }

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(properties);
            if (hasBaseMap)
            {
                properties.SetTexture(BaseMapId, faceTexture);
            }
            if (hasMainTexture)
            {
                properties.SetTexture(MainTextureId, faceTexture);
            }
            if (material.HasProperty(BaseColorId))
            {
                properties.SetColor(BaseColorId, Color.white);
            }
            if (material.HasProperty(ColorId))
            {
                properties.SetColor(ColorId, Color.white);
            }

            targetRenderer.SetPropertyBlock(properties);
            return true;
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class CardFaceRendererBinding : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private CardDefinition cardDefinition;

        public CardDefinition CardDefinition => cardDefinition;

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
            ApplyBinding();
        }

        private void OnEnable()
        {
            ApplyBinding();
        }

        private void OnValidate()
        {
            targetRenderer ??= GetComponent<Renderer>();
            ApplyBinding();
        }

        public bool ApplyBinding()
        {
            targetRenderer ??= GetComponent<Renderer>();
            return CardFaceTextureApplicator.TryApply(
                targetRenderer,
                cardDefinition != null ? cardDefinition.FaceTexture : null);
        }
    }
}

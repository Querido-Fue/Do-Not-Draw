using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoNotDraw.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractableOuterGlow : MonoBehaviour
    {
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();

        private readonly Dictionary<Renderer, Material[]> originalMaterials =
            new Dictionary<Renderer, Material[]>();
        private bool visible;

        public bool IsVisible => visible;

        public void Configure(Material material, Renderer[] renderers)
        {
            SetVisible(false);
            glowMaterial = material;
            targetRenderers = renderers ?? Array.Empty<Renderer>();
        }

        public void SetVisible(bool shouldBeVisible)
        {
            if (shouldBeVisible == visible)
            {
                return;
            }

            if (!shouldBeVisible)
            {
                RestoreOriginalMaterials();
                visible = false;
                return;
            }

            if (glowMaterial == null || targetRenderers == null || targetRenderers.Length == 0)
            {
                visible = false;
                return;
            }

            bool appliedToAnyRenderer = false;
            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                Material[] materials = targetRenderer.sharedMaterials;
                if (Contains(materials, glowMaterial))
                {
                    appliedToAnyRenderer = true;
                    continue;
                }

                originalMaterials[targetRenderer] = materials;
                Material[] highlightedMaterials = new Material[materials.Length + 1];
                Array.Copy(materials, highlightedMaterials, materials.Length);
                highlightedMaterials[highlightedMaterials.Length - 1] = glowMaterial;
                targetRenderer.sharedMaterials = highlightedMaterials;
                appliedToAnyRenderer = true;
            }

            visible = appliedToAnyRenderer;
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            RestoreOriginalMaterials();
        }

        private void RestoreOriginalMaterials()
        {
            foreach (KeyValuePair<Renderer, Material[]> entry in originalMaterials)
            {
                if (entry.Key != null)
                {
                    entry.Key.sharedMaterials = entry.Value;
                }
            }

            originalMaterials.Clear();
        }

        private static bool Contains(Material[] materials, Material target)
        {
            foreach (Material material in materials)
            {
                if (material == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

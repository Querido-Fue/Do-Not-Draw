using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DoNotDraw.World
{
    [DisallowMultipleComponent]
    public sealed class RoomFaceInfestation : MonoBehaviour
    {
        private const float FaceAspect = 736f / 1063f;
        private static readonly int ApparitionAlphaId = Shader.PropertyToID("_ApparitionAlpha");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("References")]
        [SerializeField] private Mesh faceMesh;
        [SerializeField] private Material faceMaterial;

        [Header("Room Interior")]
        [SerializeField] private Vector3 interiorSize = new Vector3(5.76f, 2.82f, 4.66f);
        [SerializeField, Min(0f)] private float surfaceInset = 0.045f;
        [SerializeField, Min(1)] private int longWallColumns = 5;
        [SerializeField, Min(1)] private int shortWallColumns = 4;
        [SerializeField, Min(1)] private int wallRows = 2;
        [SerializeField, Min(1)] private int ceilingColumns = 4;
        [SerializeField, Min(1)] private int ceilingRows = 3;

        [Header("Appearance")]
        [SerializeField, Min(0.1f)] private float revealDuration = 2f;
        [SerializeField] private Vector2 faceHeightRange = new Vector2(0.68f, 1.12f);
        [SerializeField, Range(0f, 1f)] private float finalAlpha = 0.84f;
        [SerializeField, Range(0f, 2f)] private float apparitionIntensity = 0.72f;
        [SerializeField] private int layoutSeed = 8713;

        private readonly List<FaceInstance> faces = new List<FaceInstance>();
        private bool layoutBuilt;
        private bool infestationVisible;
        private bool revealComplete;
        private float revealStartedAt;
        private float activeRevealDuration;

        private sealed class FaceInstance
        {
            public Renderer Renderer;
            public MaterialPropertyBlock PropertyBlock;
            public float RevealThreshold;
            public float AlphaVariation;
        }

        private enum RoomSurface
        {
            North,
            South,
            East,
            West,
            Ceiling
        }

        public bool IsVisible => infestationVisible;
        public bool IsRevealComplete => infestationVisible && revealComplete;
        public int FaceCount => faces.Count;

        private void Awake()
        {
            BuildLayout();
            ClearImmediately();
        }

        private void Update()
        {
            if (!infestationVisible || revealComplete)
            {
                return;
            }

            float progress = Mathf.Clamp01(
                (Time.unscaledTime - revealStartedAt) / Mathf.Max(0.1f, activeRevealDuration));
            foreach (FaceInstance face in faces)
            {
                float localProgress = Mathf.InverseLerp(
                    face.RevealThreshold,
                    Mathf.Min(1f, face.RevealThreshold + 0.2f),
                    progress);
                float eased = SmoothStep01(localProgress);
                SetFaceAlpha(face, finalAlpha * face.AlphaVariation * eased);
            }

            if (progress < 1f)
            {
                return;
            }

            revealComplete = true;
            foreach (FaceInstance face in faces)
            {
                SetFaceAlpha(face, finalAlpha * face.AlphaVariation);
            }
        }

        public void BeginReveal(float duration = -1f)
        {
            BuildLayout();
            if (faces.Count == 0)
            {
                Debug.LogError(
                    "[RoomFaceInfestation] A face mesh and material are required before the reveal can begin.",
                    this);
                return;
            }

            activeRevealDuration = duration > 0f ? duration : revealDuration;
            revealStartedAt = Time.unscaledTime;
            infestationVisible = true;
            revealComplete = false;
            foreach (FaceInstance face in faces)
            {
                if (face.Renderer == null)
                {
                    continue;
                }

                face.Renderer.enabled = true;
                SetFaceAlpha(face, 0f);
            }
        }

        public void ClearImmediately()
        {
            infestationVisible = false;
            revealComplete = false;
            foreach (FaceInstance face in faces)
            {
                if (face.Renderer == null)
                {
                    continue;
                }

                SetFaceAlpha(face, 0f);
                face.Renderer.enabled = false;
            }
        }

        private void BuildLayout()
        {
            if (layoutBuilt || faceMesh == null || faceMaterial == null)
            {
                return;
            }

            layoutBuilt = true;
            var random = new System.Random(layoutSeed);
            CreateSurfaceGrid(RoomSurface.North, longWallColumns, wallRows, random);
            CreateSurfaceGrid(RoomSurface.South, longWallColumns, wallRows, random);
            CreateSurfaceGrid(RoomSurface.East, shortWallColumns, wallRows, random);
            CreateSurfaceGrid(RoomSurface.West, shortWallColumns, wallRows, random);
            CreateSurfaceGrid(RoomSurface.Ceiling, ceilingColumns, ceilingRows, random);
        }

        private void CreateSurfaceGrid(
            RoomSurface surface,
            int columns,
            int rows,
            System.Random random)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            float halfWidth = interiorSize.x * 0.5f;
            float halfHeight = interiorSize.y * 0.5f;
            float halfDepth = interiorSize.z * 0.5f;
            bool ceiling = surface == RoomSurface.Ceiling;
            bool longWall = surface is RoomSurface.North or RoomSurface.South;
            float horizontalSpan = ceiling || longWall ? interiorSize.x : interiorSize.z;
            float verticalSpan = ceiling ? interiorSize.z : interiorSize.y;
            float horizontalCell = horizontalSpan / columns;
            float verticalCell = verticalSpan / rows;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float horizontal = -horizontalSpan * 0.5f
                        + (column + 0.5f) * horizontalCell
                        + RandomRange(random, -horizontalCell * 0.16f, horizontalCell * 0.16f);
                    float vertical = -verticalSpan * 0.5f
                        + (row + 0.5f) * verticalCell
                        + RandomRange(random, -verticalCell * 0.12f, verticalCell * 0.12f);
                    float faceHeight = RandomRange(random, faceHeightRange.x, faceHeightRange.y);
                    Vector3 localPosition;
                    Quaternion localRotation;

                    switch (surface)
                    {
                        case RoomSurface.North:
                            localPosition = new Vector3(horizontal, vertical, halfDepth - surfaceInset);
                            localRotation = Quaternion.identity;
                            break;
                        case RoomSurface.South:
                            localPosition = new Vector3(horizontal, vertical, -halfDepth + surfaceInset);
                            localRotation = Quaternion.Euler(0f, 180f, 0f);
                            break;
                        case RoomSurface.East:
                            localPosition = new Vector3(halfWidth - surfaceInset, vertical, horizontal);
                            localRotation = Quaternion.Euler(0f, 90f, 0f);
                            break;
                        case RoomSurface.West:
                            localPosition = new Vector3(-halfWidth + surfaceInset, vertical, horizontal);
                            localRotation = Quaternion.Euler(0f, -90f, 0f);
                            break;
                        default:
                            localPosition = new Vector3(horizontal, halfHeight - surfaceInset, vertical);
                            localRotation = Quaternion.Euler(90f, 0f, 0f);
                            break;
                    }

                    localRotation *= Quaternion.Euler(
                        0f,
                        0f,
                        RandomRange(random, -7.5f, 7.5f));
                    CreateFace(
                        surface,
                        row,
                        column,
                        localPosition,
                        localRotation,
                        faceHeight,
                        random);
                }
            }
        }

        private void CreateFace(
            RoomSurface surface,
            int row,
            int column,
            Vector3 localPosition,
            Quaternion localRotation,
            float faceHeight,
            System.Random random)
        {
            var faceObject = new GameObject($"Infestation Face {surface} {row:00}-{column:00}")
            {
                hideFlags = HideFlags.DontSave
            };
            Transform faceTransform = faceObject.transform;
            faceTransform.SetParent(transform, false);
            faceTransform.localPosition = localPosition;
            faceTransform.localRotation = localRotation;
            faceTransform.localScale = new Vector3(faceHeight * FaceAspect, faceHeight, 1f);

            MeshFilter filter = faceObject.AddComponent<MeshFilter>();
            filter.sharedMesh = faceMesh;
            MeshRenderer faceRenderer = faceObject.AddComponent<MeshRenderer>();
            faceRenderer.sharedMaterial = faceMaterial;
            faceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            faceRenderer.receiveShadows = false;
            faceRenderer.lightProbeUsage = LightProbeUsage.Off;
            faceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            faceRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            faceRenderer.sortingOrder = 7;
            faceRenderer.enabled = false;

            faces.Add(new FaceInstance
            {
                Renderer = faceRenderer,
                PropertyBlock = new MaterialPropertyBlock(),
                RevealThreshold = RandomRange(random, 0f, 0.8f),
                AlphaVariation = RandomRange(random, 0.76f, 1f)
            });
        }

        private void SetFaceAlpha(FaceInstance face, float alpha)
        {
            if (face?.Renderer == null)
            {
                return;
            }

            face.Renderer.GetPropertyBlock(face.PropertyBlock);
            face.PropertyBlock.SetFloat(ApparitionAlphaId, Mathf.Clamp01(alpha));
            face.PropertyBlock.SetFloat(IntensityId, apparitionIntensity);
            face.Renderer.SetPropertyBlock(face.PropertyBlock);
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            float lower = Mathf.Min(minimum, maximum);
            float upper = Mathf.Max(minimum, maximum);
            return Mathf.Lerp(lower, upper, (float)random.NextDouble());
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void OnValidate()
        {
            interiorSize = new Vector3(
                Mathf.Max(0.5f, interiorSize.x),
                Mathf.Max(0.5f, interiorSize.y),
                Mathf.Max(0.5f, interiorSize.z));
            surfaceInset = Mathf.Max(0f, surfaceInset);
            longWallColumns = Mathf.Max(1, longWallColumns);
            shortWallColumns = Mathf.Max(1, shortWallColumns);
            wallRows = Mathf.Max(1, wallRows);
            ceilingColumns = Mathf.Max(1, ceilingColumns);
            ceilingRows = Mathf.Max(1, ceilingRows);
            revealDuration = Mathf.Max(0.1f, revealDuration);
            float minimumHeight = Mathf.Max(0.1f, Mathf.Min(faceHeightRange.x, faceHeightRange.y));
            float maximumHeight = Mathf.Max(minimumHeight, Mathf.Max(faceHeightRange.x, faceHeightRange.y));
            faceHeightRange = new Vector2(minimumHeight, maximumHeight);
            finalAlpha = Mathf.Clamp01(finalAlpha);
            apparitionIntensity = Mathf.Clamp(apparitionIntensity, 0f, 2f);
        }
    }
}

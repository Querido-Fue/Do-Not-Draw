using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BrightnessCalibrationScreen : MonoBehaviour
    {
        [Header("Markers")]
        [SerializeField] Graphic markerHidden;
        [SerializeField] Graphic markerThreshold;
        [SerializeField] Graphic markerVisible;

        [Header("Marker sRGB values")]
        [SerializeField, Range(0f, 0.3f)] float hiddenValue = 0.02f;
        [SerializeField, Range(0f, 0.3f)] float thresholdValue = 0.06f;
        [SerializeField, Range(0f, 0.3f)] float visibleValue = 0.12f;

        [Header("UI")]
        [SerializeField] Camera canvasCamera;
        [SerializeField] Slider slider;
        [SerializeField] TMP_Text valueLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] Button resetButton;

        [Header("Behaviour")]
        [SerializeField] bool closeOnEscape = true;
        [SerializeField] bool pauseWhileOpen = false;

        [Header("Popup")]
        [Tooltip("팝업이 열려 있는 동안 숨길 오브젝트. 메인 메뉴 캔버스 등.")]
        [SerializeField] GameObject[] hideWhileOpen;

        bool[] hiddenPrevState;

    /// <summary>확정(true) 또는 취소(false)로 닫힐 때 발생.</summary>
    public event Action<bool> Closed;

        float entryGamma;
        bool confirmed;
        float previousTimeScale = 1f;

        void Awake()
        {
            SetMarker(markerHidden, hiddenValue);
            SetMarker(markerThreshold, thresholdValue);
            SetMarker(markerVisible, visibleValue);

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            slider.onValueChanged.AddListener(OnSliderChanged);
            confirmButton.onClick.AddListener(OnConfirm);
            cancelButton.onClick.AddListener(OnCancel);
            resetButton.onClick.AddListener(OnReset);
        }

        void OnDestroy()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(OnSliderChanged);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancel);
            if (resetButton != null) resetButton.onClick.RemoveListener(OnReset);
        }

        /// <summary>외부에서 팝업을 여는 진입점.</summary>
        public void Open() => gameObject.SetActive(true);

        void OnEnable()
        {
            // 열릴 때마다 현재 값을 다시 기준점으로 잡는다.
            confirmed = false;
            entryGamma = DisplaySettings.Gamma;

            slider.SetValueWithoutNotify(DisplaySettings.GammaToSlider(entryGamma));
            UpdateLabel(entryGamma);

            if (pauseWhileOpen)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            hiddenPrevState = new bool[hideWhileOpen.Length];
            for (int i = 0; i < hideWhileOpen.Length; i++)
            {
                if (hideWhileOpen[i] == null) continue;
                hiddenPrevState[i] = hideWhileOpen[i].activeSelf;
                hideWhileOpen[i].SetActive(false);
            }
        }

        void OnDisable()
        {
            // 버튼을 거치지 않고 꺼진 경우에도 미리보기 값이 남지 않도록 되돌린다.
            if (!confirmed) DisplaySettings.SetGamma(entryGamma);

            if (pauseWhileOpen) Time.timeScale = previousTimeScale;

            if (hiddenPrevState != null)
            {
                for (int i = 0; i < hideWhileOpen.Length && i < hiddenPrevState.Length; i++)
                    if (hideWhileOpen[i] != null) hideWhileOpen[i].SetActive(hiddenPrevState[i]);
            }
        }

        void Update()
        {
            if (closeOnEscape && EscapePressed()) OnCancel();
        }

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
    return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        void OnSliderChanged(float t)
        {
            float g = DisplaySettings.SliderToGamma(t);
            DisplaySettings.SetGamma(g);   // 미리보기만. 저장은 확정 시점에.
            UpdateLabel(g);
        }

        void OnConfirm()
        {
            confirmed = true;
            DisplaySettings.Save();
            gameObject.SetActive(false);
            Closed?.Invoke(true);
        }

        void OnCancel()
        {
            DisplaySettings.SetGamma(entryGamma);
            gameObject.SetActive(false);    // confirmed == false → OnDisable에서 한 번 더 복구(무해)
            Closed?.Invoke(false);
        }

        void OnReset()
        {
            float g = DisplaySettings.DefaultGamma;
            slider.SetValueWithoutNotify(DisplaySettings.GammaToSlider(g));
            DisplaySettings.SetGamma(g);
            UpdateLabel(g);
        }

        void UpdateLabel(float g)
        {
            if (valueLabel != null) valueLabel.text = $"감마 {g:0.00}";
        }

        static void SetMarker(Graphic g, float srgb)
        {
            if (g != null) g.color = new Color(srgb, srgb, srgb, 1f);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            SetMarker(markerHidden, hiddenValue);
            SetMarker(markerThreshold, thresholdValue);
            SetMarker(markerVisible, visibleValue);
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ContextMenu("Dump marker pixels")]
        void Dump()
        {
            if (Application.isPlaying) StartCoroutine(DumpRoutine());
            else Debug.LogWarning("플레이 모드에서만 동작합니다.");
        }

        IEnumerator DumpRoutine()
        {
            yield return new WaitForEndOfFrame();

            var tex = ScreenCapture.CaptureScreenshotAsTexture();

            foreach (var g in new[] { markerHidden, markerThreshold, markerVisible })
            {
                if (g == null) continue;

                var rt = g.rectTransform;
                Vector3 sp = RectTransformUtility.WorldToScreenPoint(
                    canvasCamera, rt.TransformPoint(rt.rect.center));

                int x = Mathf.Clamp(Mathf.RoundToInt(sp.x), 0, tex.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(sp.y), 0, tex.height - 1);

                Color c = tex.GetPixel(x, y);
                Debug.Log($"{g.name}: {Mathf.RoundToInt(c.r * 255f)} / 255  (gamma {DisplaySettings.Gamma:0.00})");
            }

            Destroy(tex);
        }
#endif
    }
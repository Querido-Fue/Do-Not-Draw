using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pandemonium.Settings
{
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

        [Header("Flow")]
        [SerializeField] string returnSceneName = "MainMenu";

        float entryGamma;

        void Awake()
        {
            entryGamma = DisplaySettings.Gamma;

            SetMarker(markerHidden, hiddenValue);
            SetMarker(markerThreshold, thresholdValue);
            SetMarker(markerVisible, visibleValue);

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(DisplaySettings.GammaToSlider(entryGamma));
            UpdateLabel(entryGamma);

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

        void OnSliderChanged(float t)
        {
            float g = DisplaySettings.SliderToGamma(t);
            DisplaySettings.SetGamma(g);   // 미리보기만. 저장은 확정 시점에.
            UpdateLabel(g);
        }

        void OnConfirm()
        {
            DisplaySettings.Save();
            SceneManager.LoadScene(returnSceneName);
        }

        void OnCancel()
        {
            DisplaySettings.SetGamma(entryGamma);   // 저장하지 않고 진입 시점 값으로 복구
            SceneManager.LoadScene(returnSceneName);
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
        // 검증용. 화면에 실제로 찍힌 마커 픽셀값을 로그로 출력합니다.
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
            var targets = new[] { markerHidden, markerThreshold, markerVisible };

            foreach (var g in targets)
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
}
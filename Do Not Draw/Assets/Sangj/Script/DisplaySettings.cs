using System;
using UnityEngine;

public static class DisplaySettings
{
    public const string PrefKey = "settings.display.gamma";
    public const float DefaultGamma = 1.0f;
    public const float MinGamma = 0.5f;
    public const float MaxGamma = 2.0f;

    static readonly int GammaId = Shader.PropertyToID("_UserGamma");

    public static float Gamma { get; private set; } = DefaultGamma;
    public static event Action<float> GammaChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        GammaChanged = null;   // 도메인 리로드 비활성 시 구독 누수 방지
        Load();
    }

    public static void Load()
    {
        Gamma = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKey, DefaultGamma), MinGamma, MaxGamma);
        Apply();
    }

    public static void SetGamma(float gamma)
    {
        Gamma = Mathf.Clamp(gamma, MinGamma, MaxGamma);
        Apply();
        GammaChanged?.Invoke(Gamma);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(PrefKey, Gamma);
        PlayerPrefs.Save();
    }

    static void Apply() => Shader.SetGlobalFloat(GammaId, Gamma);

    public static float SliderToGamma(float t)
    => Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(MinGamma, 2f), Mathf.Log(MaxGamma, 2f), Mathf.Clamp01(t)));

    public static float GammaToSlider(float g)
        => Mathf.InverseLerp(Mathf.Log(MinGamma, 2f), Mathf.Log(MaxGamma, 2f),
             Mathf.Log(Mathf.Clamp(g, MinGamma, MaxGamma), 2f));
}
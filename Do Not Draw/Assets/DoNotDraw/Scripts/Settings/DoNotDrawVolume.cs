using UnityEngine;
using UnityEngine.UI;

namespace DoNotDraw.Settings
{
    // BGM(형광등버즈, 초침, 드론, 숨소리텍스처, 덱호버드론, 바람, 실루엣접근음 등
    // 루프/지속 사운드)과 SFX(카드뽑기, 스위치, 문, 발소리, 임팩트 등 원샷
    // 사운드)를 따로 조절할 수 있게 만든 정적 볼륨 저장소.
    //
    // 정적(static)이라 씬 어디서든 DoNotDrawVolume.BgmVolume /
    // DoNotDrawVolume.SfxVolume 로 바로 참조 가능. 기존 사운드 코드
    // (ClosedRoomStoryDirector.cs 등)를 크게 안 건드리고 볼륨 곱셈만
    // 추가하면 됩니다.
    public static class DoNotDrawVolume
    {
        private const string BgmPrefsKey = "DoNotDraw_BgmVolume";
        private const string SfxPrefsKey = "DoNotDraw_SfxVolume";

        private static float bgmVolume = 1f;
        private static float sfxVolume = 1f;
        private static bool loaded;

        public static float BgmVolume
        {
            get { EnsureLoaded(); return bgmVolume; }
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(BgmPrefsKey, bgmVolume);
                OnBgmVolumeChanged?.Invoke(bgmVolume);
            }
        }

        public static float SfxVolume
        {
            get { EnsureLoaded(); return sfxVolume; }
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxPrefsKey, sfxVolume);
            }
        }

        // 루프 재생 중인 BGM AudioSource들은 값이 바뀔 때마다 즉시 반영되어야
        // 하므로, 볼륨 변경 이벤트를 구독해서 자기 볼륨을 갱신하도록 함.
        public static event System.Action<float> OnBgmVolumeChanged;

        private static void EnsureLoaded()
        {
            if (loaded) return;
            bgmVolume = PlayerPrefs.GetFloat(BgmPrefsKey, 1f);
            sfxVolume = PlayerPrefs.GetFloat(SfxPrefsKey, 1f);
            loaded = true;
        }

        // SFX(원샷)는 재생하는 순간 이 배율만 곱해서 넘기면 됩니다.
        // 예: audioSource.PlayOneShot(clip, baseVolume * DoNotDrawVolume.SfxVolume);
    }

    // BGM 루프용 AudioSource에 붙이면 볼륨 슬라이더 변경이 자동 반영됩니다.
    // 예: ambientSource, droneSource, breathTextureSource 등 지속 재생
    // AudioSource 각각에 하나씩 붙이고 baseVolume에 원래 쓰던 볼륨값을 넣으세요.
    public class BgmVolumeFollower : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

        private void OnEnable()
        {
            ApplyVolume(DoNotDrawVolume.BgmVolume);
            DoNotDrawVolume.OnBgmVolumeChanged += ApplyVolume;
        }

        private void OnDisable()
        {
            DoNotDrawVolume.OnBgmVolumeChanged -= ApplyVolume;
        }

        private void ApplyVolume(float bgmVolume)
        {
            if (audioSource != null)
            {
                audioSource.volume = baseVolume * bgmVolume;
            }
        }
    }

    // 설정 화면 슬라이더 2개(BGM, SFX)를 이 스크립트에 연결하면 끝.
    public class VolumeSettingsUI : MonoBehaviour
    {
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        private void Awake()
        {
            if (bgmSlider != null)
            {
                bgmSlider.SetValueWithoutNotify(DoNotDrawVolume.BgmVolume);
                bgmSlider.onValueChanged.AddListener(v => DoNotDrawVolume.BgmVolume = v);
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(DoNotDrawVolume.SfxVolume);
                sfxSlider.onValueChanged.AddListener(v => DoNotDrawVolume.SfxVolume = v);
            }
        }
    }
}

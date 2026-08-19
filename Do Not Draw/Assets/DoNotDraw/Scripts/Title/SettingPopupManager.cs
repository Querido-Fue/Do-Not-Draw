using UnityEngine;
using UnityEngine.UI;

public class SettingPopupManager : MonoBehaviour
{
    private static SettingPopupManager instance = null;

    void Awake()
    {
        if (null == instance)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public static SettingPopupManager Instance
    {
        get
        {
            if (null == instance)
            {
                return null;
            }
            return instance;
        }
    }
    [SerializeField] private GameObject popup;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button brightBtn;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Button applyBtn;

    private float cachedBgmVolume;
    private float cachedSfxVolume;

    void Start()
    {
        bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        brightBtn.onClick.AddListener(OnBrightButtonClicked);
        applyBtn.onClick.AddListener(OnApplyButtonClicked);
        cancelBtn.onClick.AddListener(OnCancelButtonClicked);
    }

    public void enablePopup()
    {
        cachedBgmVolume = VolumeManager.Instance.bgmVolume;
        cachedSfxVolume = VolumeManager.Instance.sfxVolume;

        bgmSlider.value = cachedBgmVolume;
        sfxSlider.value = cachedSfxVolume;

        popup.SetActive(true);
    }

    public void disablePopup()
    {

        popup.SetActive(false);
    }

    private void OnBgmSliderChanged(float value)
    {
        VolumeManager.Instance.bgmVolume = value;
    }

    private void OnSfxSliderChanged(float value)
    {
        VolumeManager.Instance.sfxVolume = value;
    }

    private void OnBrightButtonClicked()
    {
        // TODO: 밝기 조절 기능 구현
    }

    private void OnApplyButtonClicked()
    {
        disablePopup();
    }

    private void OnCancelButtonClicked()
    {
        VolumeManager.Instance.bgmVolume = cachedBgmVolume;
        VolumeManager.Instance.sfxVolume = cachedSfxVolume;

        bgmSlider.value = cachedBgmVolume;
        sfxSlider.value = cachedSfxVolume;
    }
}

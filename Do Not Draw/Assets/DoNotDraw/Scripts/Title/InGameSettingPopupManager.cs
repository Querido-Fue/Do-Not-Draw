using DoNotDraw.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameSettingPopupManager : MonoBehaviour
{
    private static readonly Vector2 PopupReferenceSize = new Vector2(1820f, 980f);
    private static InGameSettingPopupManager instance = null;

    void Awake()
    {
        if (null == instance)
        {
            instance = this;
            ConfigureResponsiveLayout();
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public static InGameSettingPopupManager Instance
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
    [SerializeField] private Button titleBtn;

    private float cachedBgmVolume;
    private float cachedSfxVolume;

    public void ConfigureResponsiveLayout()
    {
        ResolutionIndependentCanvas.Configure(GetComponent<Canvas>());
        if (popup == null || popup.transform is not RectTransform popupRect)
        {
            return;
        }

        popupRect.anchorMin = Vector2.one * 0.5f;
        popupRect.anchorMax = Vector2.one * 0.5f;
        popupRect.pivot = Vector2.one * 0.5f;
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta = PopupReferenceSize;
    }

    void Start()
    {
        bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        brightBtn.onClick.AddListener(OnBrightButtonClicked);
        applyBtn.onClick.AddListener(OnApplyButtonClicked);
        cancelBtn.onClick.AddListener(OnCancelButtonClicked);
        titleBtn.onClick.AddListener(gotoTitle);
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
    private void gotoTitle()
    {
        disablePopup();
        SceneManager.LoadScene("ClosedRoom");
    }
}

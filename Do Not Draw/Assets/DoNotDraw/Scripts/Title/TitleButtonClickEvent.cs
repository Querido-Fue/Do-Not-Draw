using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class TitleButtonClickEvent : MonoBehaviour
{
    [SerializeField] private Button StartBtn;
    [SerializeField] private Button SettingBtn;
    [SerializeField] private Button ExitBtn;
    [SerializeField] private GameObject titlePresentationRoot;
    [SerializeField] private Camera titleCamera;
    [SerializeField] private string gameplayPlayerName = "Player - First Person Controller";

    private static bool startGameplayOnNextLoad;
    private Camera gameplayCamera;
    private AudioListener gameplayAudioListener;
    private GameObject gameplayPlayer;
    private bool listenersBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        startGameplayOnNextLoad = false;
    }

    void Awake()
    {
        gameplayCamera = Camera.main;
        gameplayAudioListener = gameplayCamera != null
            ? gameplayCamera.GetComponent<AudioListener>()
            : null;
        gameplayPlayer = GameObject.Find(gameplayPlayerName);

        if (startGameplayOnNextLoad)
        {
            startGameplayOnNextLoad = false;
            EnterGameplayMode();
            return;
        }

        BindButtons();
        EnterTitleMode();
    }

    void StartGame()
    {
        if (StartBtn != null)
        {
            StartBtn.interactable = false;
        }

        startGameplayOnNextLoad = true;
        SceneManager.LoadScene("ClosedRoom");
    }

    void ShowSettingPopup()
    {
        SettingPopupManager.Instance.enablePopup();
    }

    void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else 
            Application.Quit();
        #endif
    }

    private void BindButtons()
    {
        if (listenersBound)
        {
            return;
        }

        if (StartBtn != null)
        {
            StartBtn.onClick.AddListener(StartGame);
        }
        if (SettingBtn != null)
        {
            SettingBtn.onClick.AddListener(ShowSettingPopup);
        }
        if (ExitBtn != null)
        {
            ExitBtn.onClick.AddListener(ExitGame);
        }

        listenersBound = true;
    }

    private void EnterTitleMode()
    {
        if (gameplayPlayer != null)
        {
            gameplayPlayer.SetActive(false);
        }
        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = false;
        }
        if (gameplayAudioListener != null)
        {
            gameplayAudioListener.enabled = false;
        }
        if (titleCamera != null)
        {
            titleCamera.gameObject.SetActive(true);
            titleCamera.enabled = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void EnterGameplayMode()
    {
        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = true;
        }
        if (gameplayAudioListener != null)
        {
            gameplayAudioListener.enabled = true;
        }
        if (titleCamera != null)
        {
            titleCamera.gameObject.SetActive(false);
        }

        GameObject root = titlePresentationRoot != null
            ? titlePresentationRoot
            : gameObject;
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (!listenersBound)
        {
            return;
        }

        if (StartBtn != null)
        {
            StartBtn.onClick.RemoveListener(StartGame);
        }
        if (SettingBtn != null)
        {
            SettingBtn.onClick.RemoveListener(ShowSettingPopup);
        }
        if (ExitBtn != null)
        {
            ExitBtn.onClick.RemoveListener(ExitGame);
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleButtonClickEvent : MonoBehaviour
{
    [SerializeField] private Button StartBtn;
    [SerializeField] private Button SettingBtn;
    [SerializeField] private Button ExitBtn;
    void Awake()
    {
        StartBtn.onClick.AddListener(StartGame);
        SettingBtn.onClick.AddListener(ShowSettingPopup);
        ExitBtn.onClick.AddListener(ExitGame);
    }
    void StartGame()
    {
        //TODO - 밝기 조정 설정 씬으로 전환
        SceneManager.LoadScene("ClosedRoom");
    }
    void ShowSettingPopup()
    {
        //TODO - 설정 팝업 띄우기
    }    
    void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else 
            Application.Quit();
        #endif
    }
}

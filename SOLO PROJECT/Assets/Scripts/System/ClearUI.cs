using UnityEngine;
using UnityEngine.SceneManagement;

public class ClearUI : MonoBehaviour
{
    public static ClearUI Instance { get; private set; }

    [SerializeField] private GameObject clearPanel;                // 비활성 상태로 시작할 패널
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // 실제 메인 메뉴 씬 이름

    void Awake()
    {
        Instance = this;
        if (clearPanel != null) clearPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show()
    {
        Time.timeScale = 0f;              // 클리어 화면 동안 게임 정지
        clearPanel.SetActive(true);
    }

    // 버튼 OnClick에 연결
    public void OnClickMainMenu()
    {
        Time.timeScale = 1f;              // 반드시 복구
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
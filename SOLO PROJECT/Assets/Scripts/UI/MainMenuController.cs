using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Collections;

/// <summary>
/// 메인 메뉴 씬의 버튼/슬라이더 이벤트를 처리하는 컨트롤러
/// MainMenuSetup 에디터 도구가 Inspector 필드를 자동으로 연결합니다.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // ── 씬 이름 ───────────────────────────────────────────────
    [Header("씬 이름")]
    [SerializeField] private string gameSceneName = "Game";

    // ── 메인 버튼 ─────────────────────────────────────────────
    [Header("메인 버튼")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button scenarioButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    // ── 설정 패널 ─────────────────────────────────────────────
    [Header("설정 패널")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button closeSettingsButton;

    [Header("시나리오 비디오")]
    [SerializeField] private RawImage videoImage;
    [SerializeField] private VideoPlayer video;
    RawImage rawImage;

    private const string BGM_KEY = "BGMVolume";
    private const string SFX_KEY = "SFXVolume";

    void Awake()
    {
        settingsPanel?.SetActive(false);

        // 슬라이더 — PlayerPrefs에서 저장된 볼륨 로드 (기본값 1.0)
        if (bgmSlider != null)
        {
            bgmSlider.value = PlayerPrefs.GetFloat(BGM_KEY, 1f);
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat(SFX_KEY, 1f);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        startButton?.onClick.AddListener(StartGame);
        scenarioButton?.onClick.AddListener(Scenario);
        settingsButton?.onClick.AddListener(ToggleSettings);
        quitButton?.onClick.AddListener(QuitGame);
        closeSettingsButton?.onClick.AddListener(ToggleSettings);
    }

    void Start()
    {
        video.loopPointReached += EndVideo;
        rawImage = videoImage.GetComponent<RawImage>();
    }

    // ── 버튼 동작 ─────────────────────────────────────────────

    public void StartGame() => SceneManager.LoadScene(gameSceneName);

    void Scenario()
    {
        videoImage.gameObject.SetActive(true);
        rawImage.color = new Color(255, 255, 255, 255); //시나리오를 여러 번 시청할 수 있도록 하기 위하여 시나리오 버튼을 누르면 alpha 값을 255로 지정
        video.Play();
    }

    void EndVideo(VideoPlayer video)
    {
        StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(2f);
        float f = 1;
        while (f > 0)
        {
            f -= 0.1f;
            Color alpha = rawImage.color;
            alpha.a = f;
            rawImage.color = alpha;
            yield return new WaitForSeconds(0.02f);
        }
        videoImage.gameObject.SetActive(false);
    }

    public void ToggleSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── 슬라이더 콜백 ─────────────────────────────────────────

    void OnBGMVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(BGM_KEY, value);
        // AudioMixer 연결 시:
        // bgmMixer.SetFloat("BGMVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(SFX_KEY, value);
        // AudioMixer 연결 시:
        // sfxMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }
}

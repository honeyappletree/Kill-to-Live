using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Data;
using System.Collections;
using UnityEngine.UI;
using System;

/// <summary>
/// 게임 전체 상태를 관리하는 싱글톤 매니저
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Opening, Playing, LevelUp, BossIntro, Boss, GameOver }
    public GameState CurrentState { get; private set; } = GameState.Opening;

    [SerializeField] private TextMeshProUGUI gameOverText;
    public GraphicRaycaster raycaster;
    [SerializeField] private Image gameOverImage;
    float animSpeed = 2.7f;
    Color baseColor;
    public bool fadeOutFinish;
    WaitForSeconds waitForSeconds = new WaitForSeconds(0.08f);
    WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

    public string[] openingDialogue;
    [TextArea] public string[] openingTexts;
    int talkNum; //다음 대사 출력을 위한 정수
    public TextMeshProUGUI opening;
    public Button skipButton;

    private const float BossSpawnTime = 360f;
    private bool bossTriggered = false;

    // 전투가 진행 중인 상태인지 (일반 + 보스전)
    public bool IsCombatActive =>
        CurrentState == GameState.Playing || CurrentState == GameState.Boss;

    // ── 씬 이름 설정 ──────────────────────────────────────────
    [Header("씬 이름")]
    [SerializeField] private string resultSceneName   = "ResultScreen";
    [SerializeField] private string gameSceneName     = "Game";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ── 결과 데이터 ────────────────────────────────────────────
    /// <summary>씬 전환 후 ResultScreen이 읽어가는 게임 결과 데이터</summary>
    public struct GameResultData
    {
        public float survivalTime; // 실제 생존 경과 시간(초)
        public int   killCount;    // 처치 수
        public int   maxLevel;     // 달성 최고 레벨
        public int   currency;     // 획득 재화 (생존 시간(초) 수치 그대로)
    }
    public static GameResultData LastResult { get; private set; }

    public float survivalTimer; // Playing 상태일 때만 누적

    public event Action OnBossPhaseStart;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Time.timeScale = 1f;
        Init();
    }

    void Start()
    {
        StartTalk(openingTexts);
        skipButton.onClick.AddListener(SkipOpening);
    }

    void Update()
    {
        if (CurrentState == GameState.Playing || CurrentState == GameState.Boss)
            survivalTimer += Time.deltaTime;

        if (!bossTriggered && CurrentState == GameState.Playing && survivalTimer >= BossSpawnTime)
        {
            bossTriggered = true;
            CurrentState = GameState.BossIntro;
            OnBossPhaseStart?.Invoke();
        }
    }

    void SkipOpening()
    {
        EndTalk();
    }

    void StartTalk(string[] talks)
    {
        openingTexts = talks;
        StartCoroutine(Typing(openingTexts[talkNum]));
    }

    void NextTalk()
    {
        opening.text = null;
        talkNum++;

        if(talkNum == openingTexts.Length) //talkNum이 배열의 길이와 같으면 끝내기
        {
            EndTalk();
            return;
        }
        StartCoroutine(Typing(openingTexts[talkNum]));
    }

    void EndTalk()
    {
        CurrentState = GameState.Playing;
        talkNum = 0;
        skipButton.gameObject.SetActive(false);
    }

    IEnumerator Typing(string talk)
    {
        opening.text = null;
        for (int i = 0; i < talk.Length; i++)
        {
            opening.text += talk[i];
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(1.5f);
        NextTalk();
    }

    private GameState stateBeforeLevelUp;

    /// <summary>레벨업 화면으로 전환 (시간 정지)</summary>
    public void TriggerLevelUp()
    {
        // Playing 또는 Boss 상태에서만 레벨업 허용
        if (CurrentState != GameState.Playing && CurrentState != GameState.Boss) return;

        if (LevelUpPanel.Instance == null || LevelUpPanel.Instance.IsAllMaxed())
            return; // 모든 무기가 만렙이면 상태 변경 없이 종료

        stateBeforeLevelUp = CurrentState;   // Playing인지 Boss인지 기억
        CurrentState = GameState.LevelUp;
        Time.timeScale = 0f;
        LevelUpPanel.Instance.Show();
    }

    /// <summary>레벨업 선택 후 게임 재개</summary>
    public void ResumePlaying()
    {
        // 레벨업 중이 아닐 때 호출되면 상태를 건드리지 않음
        if (CurrentState == GameState.LevelUp)
            CurrentState = stateBeforeLevelUp;

        Time.timeScale = 1f;
    }

    /// <summary>생명 유지 시간 소진 시 결과 데이터를 저장하고 결과 씬으로 전환</summary>
    public void TriggerGameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;

        // 결과 데이터 수집
        PlayerStats ps = PlayerStats.Instance;
        LastResult = new GameResultData
        {
            survivalTime = survivalTimer,
            killCount    = ps != null ? PlayerStats.killCount : 0,
            maxLevel     = ps != null ? PlayerStats.level     : 1,
            currency     = Mathf.FloorToInt(survivalTimer)
        };

        Time.timeScale = 1f;
        StartCoroutine(FadeOut());
    }

    /// <summary>게임 씬 재시작</summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>메인 메뉴 씬 로드</summary>
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void Init()
    {
        raycaster.enabled = true;
        gameOverImage.enabled = false;
        baseColor = gameOverImage.color;
    }

    public void StartBossCombat()
    {
        CurrentState = GameState.Boss;
    }

    IEnumerator FadeOut()
    {
        raycaster.enabled = false;
        float alphaValue = 0f;
        gameOverImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alphaValue);
        gameOverImage.enabled = true;

        yield return waitForSeconds;

        while (1f > alphaValue)
        {
            yield return waitForSeconds;
            alphaValue += Time.deltaTime * animSpeed;
            gameOverImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alphaValue);
        }
        gameOverText.gameObject.SetActive(true);
        raycaster.enabled = true;
        fadeOutFinish = true;
    }
}

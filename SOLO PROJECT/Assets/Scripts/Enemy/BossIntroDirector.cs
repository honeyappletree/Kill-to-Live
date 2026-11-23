using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossIntroDirector : MonoBehaviour
{
    [SerializeField] private CanvasGroup[] hudGroups;         // 경험치바/생명유지시간 등 UI 캔버스들
    [SerializeField] private Camera cam;
    [SerializeField] private MonoBehaviour cameraFollow;      // 기존 카메라 추적 스크립트
    [SerializeField] private BossController bossPrefab;

    [Header("블러 (Animator)")]
    [SerializeField] private Animator blurAnimator;
    [SerializeField] private string blurInTrigger = "BlurIn";
    [SerializeField] private string blurOutTrigger = "BlurOut";

    [SerializeField] private float panDistance = 6f;
    [SerializeField] private float panTime = 1.5f;
    [SerializeField] private float bossStopFromCamX = 2f;
    [SerializeField] private float spawnMargin = 2f;
    [SerializeField] private float returnTime = 0.8f;       // 카메라 복귀 시간

    [Header("대사창")]
    private TextMeshProUGUI dialogueText;
    private GameObject dialoguePanel;
    [SerializeField] private float typeSpeed = 0.04f;           // 글자 하나당 출력 간격(초). 0이면 한 번에 표시
    [SerializeField] private float panelOpenDelay = 0.2f;       // 대사창이 뜬 뒤 첫 대사까지 대기


    [SerializeField]
    private string[] lines =
        { "그 냄새가 난다.", "나를 이 꼴로 만든 그 놈의 냄새가 난단 말이야!!", "용서하지 않겠다!" };
    [SerializeField] private float[] holdTimes = { 1.2f, 1.8f, 1.2f };

    [SerializeField] private string walkParam = "IsWalking";  // 애니메이터의 걷기 파라미터 이름
    [SerializeField] private bool spriteFacesRight = true;    // 보스 원본 스프라이트가 오른쪽을 보고 있으면 true

    private bool introPlaying = false;
    private BossController spawnedBoss;

    [Header("보스 프리팹 자식 이름 (정확히 일치해야 함)")]
    [SerializeField] private string panelName = "SpeechBalloon";
    [SerializeField] private string textName = "BossText";

    void Awake()
    {
        if (blurAnimator != null)
            blurAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    void Start()
    {
        GameManager.Instance.OnBossPhaseStart -= OnBossStart;  // 혹시 모를 중복 제거
        GameManager.Instance.OnBossPhaseStart += OnBossStart;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) 
            GameManager.Instance.OnBossPhaseStart -= OnBossStart;
    }

    void OnBossStart()
    {
        if (introPlaying || spawnedBoss != null) return;   // 이미 실행 중이거나 보스가 있으면 무시
        StartCoroutine(Intro());
    }

    IEnumerator Intro()
    {
        introPlaying = true;
        Debug.Log("Intro 시작");
        Time.timeScale = 0f;                               // 연출 동안 게임 정지

        Transform player = PlayerStats.Instance.transform;
        Vector3 camStart = cam.transform.position;

        // 1. UI 페이드아웃
        yield return FadeHud(0.5f, 1f, 0f);

        // 2. 블러 인 애니메이션 (기다리지 않고 바로 패닝 시작)
        if (blurAnimator != null)
            blurAnimator.SetTrigger(blurInTrigger);

        // 3. 카메라 우측 패닝 + 보스가 걸어 들어옴
        if (cameraFollow) cameraFollow.enabled = false;

        Vector3 camTarget = camStart + Vector3.right * panDistance;
        float halfW = cam.orthographicSize * cam.aspect;

        float stopX = camTarget.x + bossStopFromCamX;
        float spawnX = camStart.x + halfW + spawnMargin;
        float y = player.position.y;

        Vector3 spawn = new Vector3(spawnX, y, 0f);
        BossController boss = Instantiate(bossPrefab, spawn, Quaternion.identity);
        spawnedBoss = boss;
        Debug.Log("보스 스폰됨: " + boss.name);
        // Canvas 찾기
        Transform canvas = boss.transform.Find("Canvas");

        if (canvas == null)
        {
            Debug.LogError("Canvas를 찾을 수 없습니다.");
        }
        else
        {
            Debug.Log("Canvas 찾기 성공");

            // 말풍선 찾기
            Transform panel = canvas.Find(panelName);

            if (panel == null)
            {
                Debug.LogError("말풍선을 찾을 수 없습니다: " + panelName);
            }
            else
            {
                Debug.Log("말풍선 찾기 성공");

                dialoguePanel = panel.gameObject;
                dialoguePanel.SetActive(false);

                // 말풍선 내부 또는 하위 자식에서 TMP 찾기
                dialogueText = panel.GetComponentInChildren<TextMeshProUGUI>(true);

                if (dialogueText == null)
                {
                    Debug.LogError("말풍선 내부에서 TextMeshProUGUI를 찾을 수 없습니다.");
                }
                else
                {
                    Debug.Log("텍스트 찾기 성공: " + dialogueText.name);
                    dialogueText.text = "";
                }
            }
        }

        // 왼쪽을 보게 처리
        if (spriteFacesRight)
        {
            Vector3 s = boss.transform.localScale;
            s.x = -Mathf.Abs(s.x);
            boss.transform.localScale = s;
        }

        // 걷기 애니메이션 (timeScale=0에서도 재생)
        Animator anim = boss.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            anim.SetBool(walkParam, true);
        }

        float t = 0f;
        while (t < panTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / panTime);
            float eased = Mathf.SmoothStep(0f, 1f, k);   // 카메라/보스 공통 이징

            cam.transform.position = Vector3.Lerp(camStart, camTarget, eased);

            Vector3 p = boss.transform.position;
            p.x = Mathf.Lerp(spawnX, stopX, eased);       // 선형 → 동일 이징으로 변경
            boss.transform.position = p;

            yield return null;
        }
        cam.transform.position = camTarget;
        boss.transform.position = new Vector3(stopX, y, 0f);
        if (anim != null) anim.SetBool(walkParam, false);  // 도착: 멈춤
        Debug.Log("3 완료: 패닝/걷기 끝");

        // 4~6. 대사창 열기 → 대사 출력 → 대사창 닫기
        //dialogueText.text = "";
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        yield return new WaitForSecondsRealtime(panelOpenDelay);

        for (int i = 0; i < lines.Length; i++)
        {
            Debug.Log("대사: " + lines[i]);
            yield return TypeLine(lines[i]);
            yield return new WaitForSecondsRealtime(holdTimes[i]);
        }

        dialogueText.text = "";
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        // 7. 복귀: 카메라 이동 + 블러 아웃 + UI 인을 동시에
        if (blurAnimator != null)
            blurAnimator.SetTrigger(blurOutTrigger);

        Vector3 camEnd = new Vector3(player.position.x, player.position.y, camStart.z);
        Vector3 from = cam.transform.position;
        Coroutine hudFade = StartCoroutine(FadeHud(0.5f, 0f, 1f));

        t = 0f;
        while (t < returnTime)
        {
            t += Time.unscaledDeltaTime;
            cam.transform.position = Vector3.Lerp(from, camEnd, Mathf.SmoothStep(0f, 1f, t / returnTime));
            yield return null;
        }
        cam.transform.position = camEnd;
        yield return hudFade;
        if (cameraFollow) cameraFollow.enabled = true;

        // 전투 개시
        if (anim != null) anim.updateMode = AnimatorUpdateMode.Normal;
        Time.timeScale = 1f;
        GameManager.Instance.StartBossCombat();
        boss.BeginCombat();
        introPlaying = false;
        Debug.Log("7 완료: 전투 개시");

    }

    IEnumerator FadeHud(float time, float from, float to)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, to, t / time);
            foreach (var g in hudGroups) g.alpha = a;
            yield return null;
        }
        foreach (var g in hudGroups) g.alpha = to;
    }

    IEnumerator TypeLine(string line)
    {
        dialogueText.text = "";

        if (typeSpeed <= 0f)
        {
            //textName.text = line;
            yield break;
        }

        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSecondsRealtime(typeSpeed);
        }
    }
}

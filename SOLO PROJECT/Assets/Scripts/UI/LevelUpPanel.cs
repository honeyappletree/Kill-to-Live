using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 레벨업 시 3개의 무기 카드를 제시하는 패널.
/// 이미 보유 중인 무기는 레벨업, 미보유는 신규 획득.
/// </summary>
public class LevelUpPanel : MonoBehaviour
{
    public static LevelUpPanel Instance { get; private set; }

    [Header("UI 선택 버튼 (3개)")]
    public Button[]             optionButtons;
    public TextMeshProUGUI[]    optionTitles;
    public TextMeshProUGUI[]    optionDescs;

    [Header("패널 헤더")]
    public TextMeshProUGUI headerText;

    static readonly WeaponCardData[] Cards = new WeaponCardData[]
    {
        new WeaponCardData("Dark Bolt",   "가장 가까운 적에게\n마력탄을 발사한다", typeof(DarkBolt)),
        new WeaponCardData("Poison Cloud",   "독가스를 남겨\n범위 안 적에게 지속 피해",      typeof(PoisonCloud)),
        new WeaponCardData("Collapse Edge", "이동 방향으로 부채꼴 참격\n맞은 적은 밀려난다",         typeof(CollapseEdge)),
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.SetActive(false);
    }

    /// <summary>레벨업 패널을 열고 무기 카드 3개를 표시</summary>
    public void Show()
    {
        gameObject.SetActive(true);

        if (headerText != null && PlayerStats.Instance != null)
            headerText.text = $"레벨 {PlayerStats.level} 달성!\n무기를 선택하세요";

        var sm = WeaponSlotManager.Instance;

        for (int i = 0; i < Cards.Length; i++)
        {
            if (i >= optionButtons.Length) break;

            int idx = i;
            WeaponBase owned = FindOwned(sm, Cards[i].weaponType);
            bool maxed = owned != null && owned.IsMaxLevel;

            string levelTag;
            if (owned == null) levelTag = "\n<Color=purple>New</color>";
            else if (maxed) levelTag = $"\n<Color=purple>Lv.{owned.WeaponLevel} 최대</color>";
            else levelTag = $"\n<Color=purple>Lv.{owned.WeaponLevel} → Lv.{owned.WeaponLevel + 1}</color>";

            if (optionTitles != null && i < optionTitles.Length && optionTitles[i] != null)
                optionTitles[i].text = Cards[i].weaponName + "  " + levelTag;

            if (optionDescs != null && i < optionDescs.Length && optionDescs[i] != null)
                optionDescs[i].text = Cards[i].description;

            if (optionButtons[i] != null)
            {
                optionButtons[i].interactable = !maxed;
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => Select(idx));
            }
        }
    }

    void Select(int index)
    {
        WeaponSlotManager.Instance?.AddWeapon(Cards[index].weaponType);
        gameObject.SetActive(false);
        GameManager.Instance?.ResumePlaying();
    }

    public bool IsAllMaxed()
    {
        var sm = WeaponSlotManager.Instance;
        foreach(var card in Cards)
        {
            var owned = FindOwned(sm, card.weaponType);
            if(owned == null || !owned.IsMaxLevel)
            {
                return false; //하나라도 max가 아니면 false
            }
        }
        return true; //전부 max면 true 반환
    }

    WeaponBase FindOwned(WeaponSlotManager sm, System.Type type)
    {
        if (sm == null) return null;
        foreach (var w in sm.Slots)
            if (w != null && w.GetType() == type) return w;
        return null;
    }

    /// <summary>WeaponSlotManager.RegisterWeaponAbilities() 호환용 — 이 패널에서는 사용하지 않음</summary>
    public void RegisterAbility(string name, string desc, System.Action apply) { }
}

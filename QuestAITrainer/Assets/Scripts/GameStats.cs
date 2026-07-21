using UnityEngine;
using TMPro;

public class GameStats : MonoBehaviour
{
    public static GameStats Instance;

    [Header("Stats")]
    public int level = 1;
    public int xp = 0;
    public int xpNeeded = 100;
    public int questionsAnswered = 0;
    public int correctAnswers = 0;

    [Header("UI References")]
    public TextMeshProUGUI statsText;
    public UnityEngine.UI.Image xpFillBar;   // filled Image showing progress to next level

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        LoadStats();
        UpdateStatsUI();
    }

    public void AddXP(int amount)
    {
        AddXPInternal(amount, false);
    }

    void AddXPInternal(int amount, bool bonus)
    {
        xp += amount;
        questionsAnswered++;

        if (XPPopupManager.Instance != null)
            XPPopupManager.Instance.ShowXP(amount, bonus);

        // Level up
        while (xp >= xpNeeded)
        {
            xp -= xpNeeded;
            level++;
            xpNeeded = level * 100;

            Debug.Log("LEVEL UP! Now Level " + level);
            if (XPPopupManager.Instance != null)
                XPPopupManager.Instance.ShowLevelUp(level);
        }

        SaveStats();
        UpdateStatsUI();
    }

    public void AddCorrectAnswer()
    {
        correctAnswers++;
        AddXPInternal(20, true);
    }

    public void AddParticipation()
    {
        AddXPInternal(5, false);
    }

    void UpdateStatsUI()
    {
        if (statsText != null)
        {
            statsText.text =
                $"<b>LEVEL {level}</b>\n" +
                $"XP {xp} / {xpNeeded}\n" +
                $"Questions: {questionsAnswered}   Correct: {correctAnswers}";
        }
        if (xpFillBar != null)
            xpFillBar.fillAmount = xpNeeded > 0 ? (float)xp / xpNeeded : 0f;
    }

    void SaveStats()
    {
        PlayerPrefs.SetInt("level", level);
        PlayerPrefs.SetInt("xp", xp);
        PlayerPrefs.SetInt("xpNeeded", xpNeeded);
        PlayerPrefs.SetInt("questionsAnswered", questionsAnswered);
        PlayerPrefs.SetInt("correctAnswers", correctAnswers);

        PlayerPrefs.Save();
    }

    void LoadStats()
    {
        level = PlayerPrefs.GetInt("level", 1);
        xp = PlayerPrefs.GetInt("xp", 0);
        xpNeeded = PlayerPrefs.GetInt("xpNeeded", 100);
        questionsAnswered = PlayerPrefs.GetInt("questionsAnswered", 0);
        correctAnswers = PlayerPrefs.GetInt("correctAnswers", 0);
    }

    public void ResetStats()
    {
        level = 1;
        xp = 0;
        xpNeeded = 100;
        questionsAnswered = 0;
        correctAnswers = 0;

        SaveStats();
        UpdateStatsUI();

        Debug.Log("Stats Reset Successfully!");
    }
}
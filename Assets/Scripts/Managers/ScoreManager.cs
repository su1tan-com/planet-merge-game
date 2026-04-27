using UnityEngine;

/// <summary>
/// Tracks score for the current session.
/// Notifies UIManager on every change.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────
    public static ScoreManager Instance { get; private set; }

    // ── State ─────────────────────────────────────────────────────
    public int CurrentScore { get; private set; }
    public int HighScore    { get; private set; }

    private const string HighScoreKey = "PlanetMerge_HighScore";

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance  = this;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    // ─────────────────────────────────────────────────────────────

    public void AddScore(int points)
    {
        CurrentScore += points;

        if (CurrentScore > HighScore)
        {
            HighScore = CurrentScore;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
        }

        UIManager.Instance?.UpdateScore(CurrentScore, HighScore);
    }

    public void ResetScore()
    {
        CurrentScore = 0;
        UIManager.Instance?.UpdateScore(CurrentScore, HighScore);
    }
}

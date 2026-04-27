using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game-loop controller.
///
/// Responsibilities:
///   • Maintains the planet queue and loads one planet at a time to the spawn point.
///   • Delegates win/lose triggering (lose comes from GravityManager settle check).
///   • Coordinates restart / win sequence.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Planet Data  (Mercury=0 … Saturn=5)")]
    public PlanetData[] allPlanetData;

    [Header("Queue")]
    public int preQueueSize = 8;
    public int maxSpawnTier = 3;

    [Header("Timing")]
    public float nextPlanetDelay = 1.2f;

    // ── State ─────────────────────────────────────────────────────
    public bool CanShoot    { get; private set; }
    public bool IsGameOver  { get; private set; }
    public bool IsWon       { get; private set; }
    public int  CurrentLevel { get; private set; } = 1;

    private readonly Queue<PlanetData> _queue = new Queue<PlanetData>();

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => StartGame();

    // ─────────────────────────────────────────────────────────────
    #region Game Flow

    public void StartGame()
    {
        IsGameOver = false;
        IsWon      = false;
        CanShoot   = false;

        UIManager.Instance?.HideOverlays();
        BlackHoleController.Instance?.ResetVisuals();
        AudioManager.Instance?.ResumeMusic();

        Planet.ClearMergeState();
        GravityManager.Instance?.ResetTimers();

        foreach (var p in FindObjectsByType<Planet>(FindObjectsSortMode.None))
            Destroy(p.gameObject);

        ScoreManager.Instance?.ResetScore();

        _queue.Clear();
        for (int i = 0; i < preQueueSize; i++)
            _queue.Enqueue(PickRandom());

        UIManager.Instance?.UpdateQueue(PeekQueue());
        LoadNextPlanet();
    }

    public void OnPlanetLaunched()
    {
        CanShoot = false;
        StartCoroutine(WaitThenLoad());
    }

    IEnumerator WaitThenLoad()
    {
        yield return new WaitForSeconds(nextPlanetDelay);
        if (!IsGameOver && !IsWon) LoadNextPlanet();
    }

    void LoadNextPlanet()
    {
        while (_queue.Count < preQueueSize) _queue.Enqueue(PickRandom());

        PlanetData data = _queue.Dequeue();
        Vector2 pos   = SlingshotController.Instance != null
            ? (Vector2)SlingshotController.Instance.spawnPoint.position
            : Vector2.zero;
        Planet planet = PlanetFactory.Instance.SpawnPlanet(data, pos);

        SlingshotController.Instance?.LoadPlanet(planet);
        UIManager.Instance?.UpdateQueue(PeekQueue());

        CanShoot = true;
    }

    PlanetData PickRandom()
    {
        int t = Random.Range(0, Mathf.Min(maxSpawnTier, allPlanetData.Length));
        return allPlanetData[t];
    }

    List<PlanetData> PeekQueue() => new List<PlanetData>(_queue);

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Win / Lose

    public void TriggerLose()
    {
        if (IsGameOver || IsWon) return;
        IsGameOver = true;
        CanShoot   = false;
        AudioManager.Instance?.PauseMusic();
        UIManager.Instance?.ShowGameOver();
    }

    public void TriggerWin()
    {
        if (IsWon || IsGameOver) return;
        IsWon    = true;
        CanShoot = false;
        AudioManager.Instance?.PauseMusic();
        ScoreManager.Instance?.AddScore(1000);
        StartCoroutine(WinSequence());
    }

    IEnumerator WinSequence()
    {
        if (BlackHoleController.Instance)
            yield return StartCoroutine(BlackHoleController.Instance.PlayWinSequence());
        UIManager.Instance?.ShowWin(ScoreManager.Instance?.CurrentScore ?? 0);
    }

    public void RestartGame() => StartGame();

    #endregion
}

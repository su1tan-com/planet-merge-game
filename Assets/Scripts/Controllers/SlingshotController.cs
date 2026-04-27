using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles click-and-drag slingshot shooting.
///
/// How it works:
///   • The current planet sits frozen at SpawnPoint.
///   • Player presses mouse button over the planet and drags.
///   • A parabolic trajectory preview is rendered.
///   • On release the planet is launched with velocity proportional
///     to the drag vector (opposite direction, Angry-Birds style).
///
/// Attach to: any persistent Manager GameObject.
/// </summary>
public class SlingshotController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────
    public static SlingshotController Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Empty GameObject at the left-centre of the screen")]
    public Transform spawnPoint;
    [Tooltip("Camera used to convert mouse → world coords")]
    public Camera    mainCamera;

    [Header("Shooting")]
    [Tooltip("Multiplier: how much force per unit of drag")]
    public float forceMultiplier    = 5f;
    [Tooltip("Maximum launch speed (units/s)")]
    public float maxLaunchSpeed     = 9f;
    [Tooltip("Max drag radius around the spawn point")]
    public float maxDragRadius      = 3f;


    // ── Runtime ───────────────────────────────────────────────────
    private Planet   _currentPlanet;
    private bool     _isDragging;
    private Vector2  _dragWorldPos;
    private Vector2  _launchVelocity;

    private TrajectoryRenderer _trajectory;

    // ── Visual read-outs (used by SlingshotVisual) ────────────────
    public bool    IsDragging        => _isDragging;
    public Vector2 CurrentPlanetPos  => _currentPlanet != null
                                        ? (Vector2)_currentPlanet.transform.position
                                        : (spawnPoint != null ? (Vector2)spawnPoint.position : Vector2.zero);
    public bool    HasPlanetLoaded   => _currentPlanet != null;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (mainCamera == null) mainCamera = Camera.main;
        _trajectory = GetComponent<TrajectoryRenderer>();
    }

    void Update()
    {
        if (_currentPlanet == null) return;
        if (!GameManager.Instance.CanShoot)  return;

        HandleInput();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Input

    void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseScreen = mouse.position.ReadValue();
        Vector2 mouseWorld  = mainCamera.ScreenToWorldPoint(mouseScreen);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            float dist = Vector2.Distance(mouseWorld, spawnPoint.position);
            if (dist <= _currentPlanet.data.radius * 3f)
                _isDragging = true;
        }

        if (_isDragging && mouse.leftButton.isPressed)
        {
            _dragWorldPos = mouseWorld;

            Vector2 dragVec = _dragWorldPos - (Vector2)spawnPoint.position;
            if (dragVec.magnitude > maxDragRadius)
                dragVec = dragVec.normalized * maxDragRadius;

            _launchVelocity = -dragVec * forceMultiplier;
            _launchVelocity = Vector2.ClampMagnitude(_launchVelocity, maxLaunchSpeed);

            _currentPlanet.transform.position = (Vector2)spawnPoint.position + dragVec;
            _trajectory?.Show(spawnPoint.position, _launchVelocity);
        }

        if (_isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            _isDragging = false;
            _trajectory?.Hide();

            if (_launchVelocity.magnitude > 0.5f)
                Fire();
            else
                _currentPlanet.transform.position = spawnPoint.position;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Firing

    void Fire()
    {
        _currentPlanet.transform.position = spawnPoint.position;
        _currentPlanet.Unfreeze();
        _currentPlanet.Launch(_launchVelocity);
        _launchVelocity = Vector2.zero;

        GameManager.Instance.OnPlanetLaunched();
        _currentPlanet = null;
    }

    /// <summary>Called by GameManager to set the next planet ready for shooting.</summary>
    public void LoadPlanet(Planet planet)
    {
        _currentPlanet  = planet;
        _isDragging     = false;
        _launchVelocity = Vector2.zero;

        planet.transform.position = spawnPoint.position;
        planet.Freeze();
    }

    #endregion
}

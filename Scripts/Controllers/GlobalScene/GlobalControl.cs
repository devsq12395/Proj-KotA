using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class GlobalControl : MonoBehaviour
{
    public static GlobalControl I;
    private string _currentSceneName;

    [Header("Transition Overlay")]
    [SerializeField] private Canvas _overlayCanvas;
    [SerializeField] private Image _transitionImage;
    [SerializeField] private float _tweenDuration = 0.5f; // seconds for slide in/out
    [SerializeField] private float _delayBeforeLoadNew = 1f; // seconds after unload before loading new scene
    [SerializeField] private Sprite _transitionSprite; // optional: assign an image here
    [SerializeField] private Color _transitionColor = Color.black; // used if no sprite
    [SerializeField] private bool _tweenOutOnStart = true;
    [SerializeField] private bool _waitOneFrameBeforeStartTween = true;

    [Header("Transition Camera")]
    [SerializeField] private Camera _transitionCamera;
    [SerializeField] private bool _useTransitionCamera = true;

    private RectTransform _transitionRect;
    private bool _isTransitioning;

    private void SetOverlayActive(bool active)
    {
        if (_transitionImage == null) return;
        if (_transitionImage.gameObject.activeSelf == active) return;
        _transitionImage.gameObject.SetActive(active);
    }

    void Awake()
    {
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(gameObject);
            _currentSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[CHANGE_SCENE] Awake: Initial active scene tracked as '{_currentSceneName}'.");
            EnsureOverlayReady();
            HideTransitionCamera();
        }
        else if (I != this)
        {
            Debug.Log("[CHANGE_SCENE] Awake: Duplicate GlobalControl detected. Destroying this instance.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Kick off an initial reveal tween (slide out) if enabled
        if (_tweenOutOnStart)
        {
            StartCoroutine(InitialRevealRoutine());
        }
    }

    public void ChangeScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[CHANGE_SCENE] ChangeScene called with null/empty sceneName. Aborting.");
            return;
        }

        if (_isTransitioning)
        {
            Debug.LogWarning("[CHANGE_SCENE] ChangeScene requested while a transition is already running. Aborting this request.");
            return;
        }

        Debug.Log($"[CHANGE_SCENE] ChangeScene requested: '{sceneName}'. Current tracked scene: '{_currentSceneName}'.");

        if (SoundsController.I != null)
        {
            Debug.Log("[CHANGE_SCENE] Stopping all music via SoundsController.");
            SoundsController.I.StopAllMusic();
        }
        else
        {
            Debug.LogWarning("[CHANGE_SCENE] SoundsController.I is null. Skipping music stop.");
        }

        StartCoroutine(ChangeSceneRoutine(sceneName));
    }

    private IEnumerator ChangeSceneRoutine(string sceneName)
    {
        _isTransitioning = true;
        Debug.Log($"[CHANGE_SCENE] Routine start: target='{sceneName}', currentTracked='{_currentSceneName}'.");

        /*if (!string.IsNullOrEmpty(_currentSceneName) && _currentSceneName == sceneName)
        {
            Debug.Log($"[CHANGE_SCENE] Requested scene '{sceneName}' is already the current tracked scene. Exiting.");
            HideTransitionCamera();
            _isTransitioning = false;
            yield break;
        }*/

        // Ensure overlay is present and configured
        EnsureOverlayReady();

        // Show camera for transition (so overlay is visible while loading)
        ShowTransitionCamera();

        // Cache the currently active scene name (to unload after the cover is complete)
        string previousSceneName = _currentSceneName;
        Debug.Log($"[CHANGE_SCENE] Previous scene set to '{previousSceneName}'.");

        // Slide IN to cover screen
        yield return StartCoroutine(SlideOverlayIn());

        // Unload current scene under cover
        if (!string.IsNullOrEmpty(previousSceneName))
        {
            Scene prevScene = SceneManager.GetSceneByName(previousSceneName);
            if (prevScene.IsValid() && prevScene.isLoaded)
            {
                Debug.Log($"[CHANGE_SCENE] Unloading previous scene '{previousSceneName}' under cover.");
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(previousSceneName);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                    {
                        yield return null;
                    }
                }
                else
                {
                    Debug.LogWarning($"[CHANGE_SCENE] UnloadSceneAsync returned null for '{previousSceneName}'. It may already be unloading/unloaded.");
                }
            }
            else
            {
                Debug.Log($"[CHANGE_SCENE] Previous scene '{previousSceneName}' is not valid or already unloaded. Skipping unload.");
            }
        }
        else
        {
            Debug.Log("[CHANGE_SCENE] No previous scene to unload.");
        }

        // Wait before loading the new scene
        if (_delayBeforeLoadNew > 0f)
        {
            Debug.Log($"[CHANGE_SCENE] Waiting {_delayBeforeLoadNew:0.00}s before loading new scene '{sceneName}'.");
            yield return new WaitForSeconds(_delayBeforeLoadNew);
        }

        // Load new scene additively
        Debug.Log($"[CHANGE_SCENE] Loading new scene additively: '{sceneName}'.");
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOp != null)
        {
            while (!loadOp.isDone)
            {
                yield return null;
            }
            Debug.Log($"[CHANGE_SCENE] Scene '{sceneName}' load completed.");
        }
        else
        {
            Debug.LogWarning($"[CHANGE_SCENE] LoadSceneAsync returned null for '{sceneName}'. Falling back to synchronous load.");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            yield return null;
            Debug.Log($"[CHANGE_SCENE] Scene '{sceneName}' synchronously loaded.");
        }

        // Set the newly loaded scene as the active scene
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        if (newScene.IsValid())
        {
            bool setActiveOk = SceneManager.SetActiveScene(newScene);
            Debug.Log($"[CHANGE_SCENE] SetActiveScene('{sceneName}') result={setActiveOk}.");
        }
        else
        {
            Debug.LogError($"[CHANGE_SCENE] Newly loaded scene '{sceneName}' is not valid after load. Keeping overlay visible.");
            _isTransitioning = false;
            // NOTE: Intentionally keep TransitionCamera ON so overlay remains visible for debugging.
            yield break;
        }

        // Update tracker
        _currentSceneName = sceneName;
        Debug.Log($"[CHANGE_SCENE] Tracker updated. _currentSceneName='{_currentSceneName}'.");

        // Slide OUT to reveal the new scene
        yield return StartCoroutine(SlideOverlayOut());

        // Audio/Camera checks (optional safety)
        if (SoundsController.I == null)
        {
            Debug.LogWarning("[CHANGE_SCENE] SoundsController.I is null after scene change. Skipping audio/camera checks.");
            _isTransitioning = false;
            yield break;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[CHANGE_SCENE] Camera.main is null after scene change.");
            _isTransitioning = false;
            yield break;
        }

        AudioSource camAudio = mainCam.GetComponent<AudioSource>();
        if (camAudio == null)
        {
            Debug.LogWarning("[CHANGE_SCENE] AudioSource not found on Camera.main after scene change.");
            _isTransitioning = false;
            yield break;
        }

        Debug.Log("[CHANGE_SCENE] Routine end: Scene change sequence completed.");
        _isTransitioning = false;
    }

    // Ensure the overlay canvas and image exist and are configured
    private void EnsureOverlayReady()
    {
        if (_overlayCanvas == null)
        {
            GameObject canvasGO = new GameObject("GlobalTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            _overlayCanvas = canvasGO.GetComponent<Canvas>();

            // Use transition camera if provided, else overlay
            _overlayCanvas.renderMode = (_useTransitionCamera && _transitionCamera != null)
                ? RenderMode.ScreenSpaceCamera
                : RenderMode.ScreenSpaceOverlay;

            if (_overlayCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                _overlayCanvas.worldCamera = _transitionCamera;
                _overlayCanvas.planeDistance = 1f; // ensure it draws on top
            }

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            DontDestroyOnLoad(canvasGO);
            Debug.Log("[CHANGE_SCENE] Created transition Canvas.");
        }
        else
        {
            // Update existing canvas mode if camera was assigned later
            _overlayCanvas.renderMode = (_useTransitionCamera && _transitionCamera != null)
                ? RenderMode.ScreenSpaceCamera
                : RenderMode.ScreenSpaceOverlay;

            if (_overlayCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                _overlayCanvas.worldCamera = _transitionCamera;
                _overlayCanvas.planeDistance = 1f;
            }
            else
            {
                _overlayCanvas.worldCamera = null;
            }
        }

        if (_transitionImage == null)
        {
            GameObject imgGO = new GameObject("TransitionImage", typeof(RectTransform), typeof(Image));
            imgGO.transform.SetParent(_overlayCanvas.transform, false);

            _transitionImage = imgGO.GetComponent<Image>();
            if (_transitionSprite != null)
            {
                _transitionImage.sprite = _transitionSprite;
                _transitionImage.type = Image.Type.Sliced;
                _transitionImage.color = Color.white;
            }
            else
            {
                _transitionImage.color = _transitionColor; // solid color if no sprite assigned
            }

            _transitionRect = _transitionImage.rectTransform;
            _transitionRect.anchorMin = Vector2.zero;
            _transitionRect.anchorMax = Vector2.one;
            _transitionRect.pivot = new Vector2(0.5f, 0.5f);
            _transitionRect.sizeDelta = Vector2.zero;

            Debug.Log("[CHANGE_SCENE] Created transition Image.");
        }
        else
        {
            _transitionRect = _transitionImage.rectTransform;
        }

        // Make sure it starts off-screen (to the right)
        SetOverlayOffscreenRight();

        // Prevent the overlay from ever "leaking" on resize when idle.
        // It will be enabled only during SlideOverlayIn/SlideOverlayOut.
        SetOverlayActive(false);
    }

    private void ShowTransitionCamera()
    {
        if (!_useTransitionCamera) return;
        if (_transitionCamera == null)
        {
            Debug.LogWarning("[CHANGE_SCENE] TransitionCamera not assigned. Overlay will still work via ScreenSpaceOverlay.");
            return;
        }
        _transitionCamera.enabled = true;
        Debug.Log("[CHANGE_SCENE] TransitionCamera enabled.");
    }

    private void HideTransitionCamera()
    {
        if (!_useTransitionCamera) return;
        if (_transitionCamera == null) return;
        _transitionCamera.enabled = false;
        Debug.Log("[CHANGE_SCENE] TransitionCamera disabled.");
    }

    private void SetOverlayOffscreenRight()
    {
        float screenWidth = GetScreenWidth();
        _transitionRect.anchoredPosition = new Vector2(screenWidth, 0f);
    }

    private float GetScreenWidth()
    {
        // Screen.width is fine for ScreenSpace Overlay or Camera
        return Screen.width;
    }

    private IEnumerator InitialRevealRoutine()
    {
        Debug.Log("[CHANGE_SCENE] InitialRevealRoutine start.");

        // Make sure overlay exists
        EnsureOverlayReady();

        // Ensure camera is OFF on startup reveal
        HideTransitionCamera();

        // Ensure no old tweens are running
        _transitionRect.DOKill();

        SetOverlayActive(true);

        // Force the overlay to cover the screen first (anchoredPosition.x = 0)
        _transitionRect.anchoredPosition = new Vector2(0f, 0f);
        Debug.Log("[CHANGE_SCENE] InitialRevealRoutine: overlay set to covered position (x=0).");

        // Optional: wait 1 frame so canvas/layout are settled and to avoid flashes
        if (_waitOneFrameBeforeStartTween)
        {
            yield return null;
        }

        // Now slide out to reveal the current scene
        Debug.Log("[CHANGE_SCENE] InitialRevealRoutine: starting SlideOverlayOut.");
        yield return StartCoroutine(SlideOverlayOut());

        Debug.Log("[CHANGE_SCENE] InitialRevealRoutine complete.");
    }

    private IEnumerator SlideOverlayIn()
    {
        Debug.Log("[CHANGE_SCENE] SlideOverlayIn start (DOTween).");
        float duration = Mathf.Max(0.01f, _tweenDuration);

        SetOverlayActive(true);

        float startX = GetScreenWidth(); // from right
        float endX = 0f;

        Vector2 pos = _transitionRect.anchoredPosition;
        pos.y = 0f;
        _transitionRect.anchoredPosition = new Vector2(startX, pos.y);

        // Kill any previous tween on this rect to avoid conflicts
        _transitionRect.DOKill();

        Tween t = _transitionRect
            .DOAnchorPosX(endX, duration)
            .SetEase(Ease.InOutCubic)
            .SetUpdate(true); // unscaled time

        yield return t.WaitForCompletion();

        _transitionRect.anchoredPosition = new Vector2(endX, 0f);
        Debug.Log("[CHANGE_SCENE] SlideOverlayIn complete (screen covered, DOTween).");
    }

    private IEnumerator SlideOverlayOut()
    {
        Debug.Log("[CHANGE_SCENE] SlideOverlayOut start (DOTween).");
        float duration = Mathf.Max(0.01f, _tweenDuration);

        SetOverlayActive(true);

        float startX = 0f;
        float endX = -GetScreenWidth(); // slide to the left

        Vector2 pos = _transitionRect.anchoredPosition;
        pos.y = 0f;
        _transitionRect.anchoredPosition = new Vector2(startX, pos.y);

        // Kill any previous tween on this rect to avoid conflicts
        _transitionRect.DOKill();

        // Hide the transition camera exactly when tween-out begins
        HideTransitionCamera();

        Tween t = _transitionRect
            .DOAnchorPosX(endX, duration)
            .SetEase(Ease.InOutCubic)
            .SetUpdate(true); // unscaled time

        yield return t.WaitForCompletion();

        _transitionRect.anchoredPosition = new Vector2(endX, 0f);

        // Reset to the right for the next transition
        SetOverlayOffscreenRight();
        SetOverlayActive(false);
        Debug.Log("[CHANGE_SCENE] SlideOverlayOut complete (screen revealed, DOTween).");
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SceneControl : MonoBehaviour
{
    public static SceneControl I;

    #region Inspector
    [Header("Transition Overlay")]
    [SerializeField] private Canvas _overlayCanvas;
    [SerializeField] private Image _transitionImage;
    [SerializeField] private float _tweenDuration = 0.5f;
    [SerializeField] private float _delayBeforeLoadNew = 1f;
    [SerializeField] private float _delayAfterLoadBeforeReveal = 0f;
    [SerializeField] private Sprite _transitionSprite;
    [SerializeField] private Color _transitionColor = Color.black;

    [Header("Circle Wipe Transition")]
    [SerializeField] private float _circleWipeSoftness = 0.02f;
    #endregion // Inspector

    private string _persistentSceneName;
    private string _currentContentSceneName;
    private bool _isTransitioning;

    public bool IsTransitioning => _isTransitioning;
    public string CurrentSceneName => _currentContentSceneName;

    private Material _circleWipeMaterialInstance;

    private enum TransitionState
    {
        Idle,
        SceneCoverWipeIn,
        SceneUnload,
        ScenePreLoadDelay,
        SceneLoad,
        SceneSetActive,
        ScenePostLoadWaitFrame,
        ScenePostLoadDelay,
        SceneRevealWipeOut
    }

    private TransitionState _transitionState = TransitionState.Idle;
    private string _pendingSceneName;
    private AsyncOperation _unloadOperation;
    private AsyncOperation _loadOperation;
    private float _stateTimerRemaining;
    private string _unloadingSceneName;
    private bool _reloadPendingScene;

    private bool _wipeActive;
    private bool _wipeFallbackSolid;
    private float _wipeDuration;
    private float _wipeElapsedUnscaledTime;
    private float _wipeStartRadius;
    private float _wipeEndRadius;

    #region Singleton & Lifecycle
    public static SceneControl EnsureExists()
    {
        if (I != null) return I;

        var existing = FindFirstObjectByType<SceneControl>();
        if (existing != null)
        {
            I = existing;
            return I;
        }

        var go = new GameObject("SceneControl");
        I = go.AddComponent<SceneControl>();
        DontDestroyOnLoad(go);
        return I;
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        EnsureOverlayReady();
        SetOverlayActive(false);
    }

    #endregion // Singleton & Lifecycle

    private void Update()
    {
        UpdateTransitionState();
    }

    #region Public API
    public void Init()
    {
        EnsureOverlayReady();
        if (!_isTransitioning)
        {
            SetOverlayActive(false);
        }
    }

    public void LoadSceneInstant(string sceneName)
    {
        LoadSceneInstant(sceneName, reloadIfSame: false);
    }

    public void LoadSceneInstant(string sceneName, bool reloadIfSame)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        if (_isTransitioning)
        {
            return;
        }

        if (SoundsController.I != null)
        {
            SoundsController.I.StopAllMusic();
        }

        EnsureOverlayReady();
        SetOverlayActive(false);

        if (!reloadIfSame && !string.IsNullOrEmpty(_currentContentSceneName) && _currentContentSceneName == sceneName)
        {
            Scene alreadyLoaded = SceneManager.GetSceneByName(sceneName);
            if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded)
            {
                TrySetActivePersistentScene();
                SceneManager.SetActiveScene(alreadyLoaded);
            }
            return;
        }

        TrySetActivePersistentScene();
        UnloadAllNonPersistentScenesExcept(reloadIfSame ? null : sceneName);

        Scene alreadyLoadedScene = SceneManager.GetSceneByName(sceneName);
        if (alreadyLoadedScene.IsValid() && alreadyLoadedScene.isLoaded)
        {
            SceneManager.SetActiveScene(alreadyLoadedScene);
            _currentContentSceneName = sceneName;
            return;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        if (newScene.IsValid() && newScene.isLoaded)
        {
            SceneManager.SetActiveScene(newScene);
            _currentContentSceneName = sceneName;
        }
    }

    public void ChangeScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (_isTransitioning) return;
        StartCoroutine(SimpleChangeSceneRoutine(sceneName));
    }

    public void ChangeScene(string sceneName, bool reloadIfSame)
    {
        // Redirect to the simplified API
        ChangeScene(sceneName);
    }
    #endregion // Public API

    #region Transition - Circle Wipe
    private IEnumerator SimpleChangeSceneRoutine(string sceneName)
    {
        _isTransitioning = true;
        EnsureOverlayReady();

        // Cover current scene
        BeginWipe(isCover: true);
        while (!UpdateWipe()) { yield return null; }

        // Load new scene (Single keeps DontDestroyOnLoad objects like this controller)
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOp != null)
        {
            while (!loadOp.isDone) { yield return null; }
        }
        else
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
        }

        // After load, ensure overlay/material are bound again
        EnsureOverlayReady();

        // Reveal new scene
        BeginWipe(isCover: false);
        while (!UpdateWipe()) { yield return null; }

        SetOverlayActive(false);
        _isTransitioning = false;
    }
    #endregion // Transition - Circle Wipe

    private void BeginSceneTransition(string sceneName, bool reloadIfSame)
    {
        // Legacy state-machine path retained for compatibility, but unused by default
        _isTransitioning = true;

        if (!reloadIfSame && !string.IsNullOrEmpty(_currentContentSceneName) && _currentContentSceneName == sceneName)
        {
            _isTransitioning = false;
            _transitionState = TransitionState.Idle;
            return;
        }

        EnsureOverlayReady();

        _pendingSceneName = sceneName;
        _unloadOperation = null;
        _loadOperation = null;
        _stateTimerRemaining = 0f;
        _unloadingSceneName = null;
        _reloadPendingScene = reloadIfSame;

        BeginWipe(isCover: true);
        _transitionState = TransitionState.SceneCoverWipeIn;
    }

    private float EaseInOutCubic(float normalizedTime)
    {
        if (normalizedTime < 0.5f)
        {
            return 4f * normalizedTime * normalizedTime * normalizedTime;
        }
        float timeValue = -2f * normalizedTime + 2f;
        return 1f - (timeValue * timeValue * timeValue) / 2f;
    }

    private void SetOverlayActive(bool active)
    {
        if (_transitionImage == null) return;
        if (_transitionImage.gameObject.activeSelf == active) return;
        _transitionImage.gameObject.SetActive(active);
    }

    private void BeginWipe(bool isCover)
    {
        EnsureOverlayReady();
        EnsureCircleWipeMaterialReady();

        _wipeDuration = Mathf.Max(0.2f, _tweenDuration);
        _wipeElapsedUnscaledTime = 0f;
        _wipeFallbackSolid = (_circleWipeMaterialInstance == null);
        _wipeActive = true;

        if (isCover)
        {
            _wipeStartRadius = 0f;
            _wipeEndRadius = GetCircleWipeMaxRadius();
            SetCircleWipeRadius(_wipeStartRadius);
            SetOverlayActive(true);
        }
        else
        {
            _wipeStartRadius = GetCircleWipeMaxRadius();
            _wipeEndRadius = 0f;
            SetCircleWipeRadius(_wipeStartRadius);
            SetOverlayActive(true);
        }
    }

    private bool UpdateWipe()
    {
        if (!_wipeActive) return true;

        _wipeElapsedUnscaledTime += Time.unscaledDeltaTime;
        float normalizedTime = Mathf.Clamp01(_wipeElapsedUnscaledTime / Mathf.Max(0.0001f, _wipeDuration));

        if (!_wipeFallbackSolid)
        {
            float easedTime = EaseInOutCubic(normalizedTime);
            float radius = Mathf.Lerp(_wipeStartRadius, _wipeEndRadius, easedTime);
            SetCircleWipeRadius(radius);
        }

        if (normalizedTime < 1f) return false;

        SetCircleWipeRadius(_wipeEndRadius);
        _wipeActive = false;
        return true;
    }

    private void EnterScenePreLoadDelay()
    {
        _stateTimerRemaining = Mathf.Max(0f, _delayBeforeLoadNew);
        _transitionState = TransitionState.ScenePreLoadDelay;
    }

    private void UpdateTransitionState()
    {
        switch (_transitionState)
        {
            case TransitionState.Idle:
                return;

            case TransitionState.SceneCoverWipeIn:
                if (!UpdateWipe()) return;
                _transitionState = TransitionState.SceneUnload;
                return;

            case TransitionState.SceneUnload:
                if (_unloadOperation == null)
                {
                    string sceneToKeepLoaded = _reloadPendingScene ? null : _pendingSceneName;
                    string candidate = GetNextSceneToUnload(sceneToKeepLoaded);
                    if (string.IsNullOrEmpty(candidate))
                    {
                        EnterScenePreLoadDelay();
                        return;
                    }

                    _unloadingSceneName = candidate;
                    TrySetActivePersistentScene();
                    _unloadOperation = SceneManager.UnloadSceneAsync(candidate);
                    if (_unloadOperation == null)
                    {
                        _unloadingSceneName = null;
                        EnterScenePreLoadDelay();
                        return;
                    }
                    return;
                }

                if (!_unloadOperation.isDone) return;
                _unloadOperation = null;
                _unloadingSceneName = null;
                return;

            case TransitionState.ScenePreLoadDelay:
                if (_stateTimerRemaining > 0f)
                {
                    _stateTimerRemaining -= Time.unscaledDeltaTime;
                    return;
                }

                _stateTimerRemaining = 0f;
                _transitionState = TransitionState.SceneLoad;
                return;

            case TransitionState.SceneLoad:
                if (_loadOperation == null)
                {
                    _loadOperation = SceneManager.LoadSceneAsync(_pendingSceneName, LoadSceneMode.Additive);
                    if (_loadOperation == null)
                    {
                        SceneManager.LoadScene(_pendingSceneName, LoadSceneMode.Additive);
                        _transitionState = TransitionState.SceneSetActive;
                        return;
                    }
                }

                if (!_loadOperation.isDone) return;
                _transitionState = TransitionState.SceneSetActive;
                return;

            case TransitionState.SceneSetActive:
                Scene newScene = SceneManager.GetSceneByName(_pendingSceneName);
                if (!newScene.IsValid())
                {
                    _isTransitioning = false;
                    _transitionState = TransitionState.Idle;
                    return;
                }

                SceneManager.SetActiveScene(newScene);
                _currentContentSceneName = _pendingSceneName;

                EnsureOverlayReady();
                _transitionState = TransitionState.ScenePostLoadWaitFrame;
                return;

            case TransitionState.ScenePostLoadWaitFrame:
                _stateTimerRemaining = Mathf.Max(0f, _delayAfterLoadBeforeReveal);
                _transitionState = TransitionState.ScenePostLoadDelay;
                return;

            case TransitionState.ScenePostLoadDelay:
                if (_stateTimerRemaining > 0f)
                {
                    _stateTimerRemaining -= Time.unscaledDeltaTime;
                    return;
                }

                _stateTimerRemaining = 0f;
                BeginWipe(isCover: false);
                _transitionState = TransitionState.SceneRevealWipeOut;
                return;

            case TransitionState.SceneRevealWipeOut:
                if (!UpdateWipe()) return;
                SetOverlayActive(false);
                _isTransitioning = false;
                _transitionState = TransitionState.Idle;
                return;
        }
    }

    #region Overlay & Material
    private void EnsureOverlayReady()
    {
        if (_overlayCanvas == null)
        {
            GameObject canvasGO = new GameObject("GlobalTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            _overlayCanvas = canvasGO.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.worldCamera = null;
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 32767;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
        }
        else
        {
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.worldCamera = null;
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 32767;

            CanvasScaler scaler = _overlayCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
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
                _transitionImage.color = _transitionColor;
            }

            RectTransform transitionRect = _transitionImage.rectTransform;
            transitionRect.anchorMin = Vector2.zero;
            transitionRect.anchorMax = Vector2.one;
            transitionRect.pivot = new Vector2(0.5f, 0.5f);
            transitionRect.sizeDelta = Vector2.zero;
        }
        else
        {
            if (_transitionImage.transform.parent != _overlayCanvas.transform)
            {
                _transitionImage.transform.SetParent(_overlayCanvas.transform, false);
            }
        }

        EnsureCircleWipeMaterialReady();

        if (!_isTransitioning)
        {
            SetOverlayActive(false);
        }
    }

    private void EnsureCircleWipeMaterialReady()
    {
        if (_transitionImage == null)
        {
            _circleWipeMaterialInstance = null;
            return;
        }

        if (_circleWipeMaterialInstance == null)
        {
            Shader circleWipeShader = Shader.Find("UI/CircleWipeTransition");
            if (circleWipeShader == null)
            {
                return;
            }
            _circleWipeMaterialInstance = new Material(circleWipeShader);
        }

        _transitionImage.material = _circleWipeMaterialInstance;
        _circleWipeMaterialInstance.SetFloat("_Softness", _circleWipeSoftness);
        _circleWipeMaterialInstance.SetVector("_Center", new Vector4(0.5f, 0.5f, 0f, 0f));
        if (!_isTransitioning)
        {
            SetCircleWipeRadius(0f);
        }
    }

    private float GetCircleWipeMaxRadius()
    {
        float screenHeight = Mathf.Max(1f, Screen.height);
        float aspect = Screen.width / screenHeight;
        float maxDistance = 0.5f * Mathf.Sqrt(aspect * aspect + 1f);
        return maxDistance + Mathf.Max(0.0001f, _circleWipeSoftness) * 2f;
    }

    private void SetCircleWipeRadius(float radius)
    {
        if (_circleWipeMaterialInstance == null) return;
        _circleWipeMaterialInstance.SetFloat("_Radius", radius);
    }
    #endregion // Overlay & Material

    private void TrySetActivePersistentScene()
    {
        if (string.IsNullOrEmpty(_persistentSceneName)) return;
        Scene persistentScene = SceneManager.GetSceneByName(_persistentSceneName);
        if (!persistentScene.IsValid() || !persistentScene.isLoaded) return;
        SceneManager.SetActiveScene(persistentScene);
    }

    private void ResolvePersistentSceneName()
    {
        if (!string.IsNullOrEmpty(_persistentSceneName)) return;

        Scene globalScene = SceneManager.GetSceneByName("GlobalScene");
        if (globalScene.IsValid() && globalScene.isLoaded)
        {
            _persistentSceneName = "GlobalScene";
            return;
        }

        _persistentSceneName = SceneManager.GetActiveScene().name;
    }

    private void UnloadAllNonPersistentScenesExcept(string sceneNameToKeep)
    {
        var scenesToUnload = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded) continue;
            if (!string.IsNullOrEmpty(_persistentSceneName) && scene.name == _persistentSceneName) continue;
            if (!string.IsNullOrEmpty(sceneNameToKeep) && scene.name == sceneNameToKeep) continue;
            scenesToUnload.Add(scene.name);
        }

        for (int i = 0; i < scenesToUnload.Count; i++)
        {
            SceneManager.UnloadSceneAsync(scenesToUnload[i]);
        }
    }

    private string GetNextSceneToUnload(string sceneNameToKeep)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            if (!string.IsNullOrEmpty(_persistentSceneName) && scene.name == _persistentSceneName) continue;
            if (!string.IsNullOrEmpty(sceneNameToKeep) && scene.name == sceneNameToKeep) continue;
            if (!string.IsNullOrEmpty(_unloadingSceneName) && scene.name == _unloadingSceneName) continue;

            return scene.name;
        }

        return null;
    }
}
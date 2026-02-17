# New Scene Control System

## Summary

This feature introduces a centralized scene management system that runs from the persistent `GlobalScene` and routes all scene loading/unloading and the circle-wipe transition through `SceneControl`.

Key outcomes:
- `GlobalScene` is the persistent bootstrap scene and must be **Build Settings index 0**.
- The **initial scene load** (first-run: `Game` with mission `Mission_1-1`, otherwise `Lobby`) happens **instantly** (no circle wipe).
- Subsequent scene loads happen with the **circle wipe transition**.
- Old content scenes are unloaded reliably when switching scenes.

## Core Concepts

### Persistent Scene vs Content Scenes

- **Persistent scene:** `GlobalScene`
  - Always loaded.
  - Contains global singletons (e.g. `GlobalControl`, `SceneControl`, `SaveController`, `AdController`, etc.).

- **Content scenes:** `Lobby`, `Game`, etc.
  - Loaded additively.
  - Unloaded when changing to another content scene.

### Responsibilities

#### `SceneControl`
- Owns:
  - Scene transitions (state machine)
  - Circle wipe overlay UI creation
  - Circle wipe shader material instancing
  - Unload safety and active-scene handling
- Public API:
  - `Init()`
  - `LoadSceneInstant(sceneName, reloadIfSame=false)`
  - `ChangeScene(sceneName, reloadIfSame=false)`
  - `IsTransitioning`

#### `GlobalControl`
- Owns:
  - Initial boot timer
  - Initial scene selection logic (first-run vs returning)
  - Delegates all scene changes to `SceneControl`

#### `Bootstrap`
- Owns:
  - Security checks
- Does **not** load `GlobalScene` additively.

## Boot Flow

1. Project starts in `GlobalScene` (Build Settings index 0)
2. `GlobalControl.Awake()` initializes and calls `SceneControl.EnsureExists().Init()`
3. After a short delay, `GlobalControl.TriggerInitialLoad()` runs
4. `AdController.EnsureExists().ShowAd(...)` gates the initial load (fake ads can be used)
5. Inside the ad callback:
   - If `SaveController.firstRun == "1"`:
     - set `currentMission = "Mission_1-1"`
     - set `firstRun = "0"`
     - instant-load `Game`
   - Else:
     - instant-load `Lobby`

## Transition Flow (Subsequent Scene Changes)

When any system calls `GlobalControl.I.ChangeScene("Lobby")` / `GlobalControl.I.ChangeScene("Game")`:

1. `GlobalControl` delegates to `SceneControl.ChangeScene()`
2. `SceneControl` performs:
   - wipe-in (cover)
   - unload non-persistent scenes (content scenes)
   - optional delay
   - additive load of the next scene
   - set active scene to the newly loaded content scene
   - optional delay
   - wipe-out (reveal)

## Required Project Setup

- **Build Settings**
  - `GlobalScene` must be index 0
  - `Lobby` and `Game` must be included

## Relevant Scripts

- `Assets/Scripts/Controllers/GlobalScene/SceneControl.cs`
- `Assets/Scripts/Controllers/GlobalScene/GlobalControl.cs`
- `Assets/Scripts/Controllers/GlobalScene/Bootstrap.cs`
- `Assets/Scripts/Controllers/GlobalScene/AdController.cs`

## Full Script: SceneControl.cs

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class SceneControl : MonoBehaviour
{
    public static SceneControl I;

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

        ResolvePersistentSceneName();

        EnsureOverlayReady();
    }

    private void Update()
    {
        UpdateTransitionState();
    }

    public void Init()
    {
        ResolvePersistentSceneName();

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
        ChangeScene(sceneName, reloadIfSame: false);
    }

    public void ChangeScene(string sceneName, bool reloadIfSame)
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

        BeginSceneTransition(sceneName, reloadIfSame);
    }

    private void BeginSceneTransition(string sceneName, bool reloadIfSame)
    {
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

        if (SaveController.I != null && _currentContentSceneName == "Game" && sceneName == "Lobby")
        {
            SaveController.I.RequestLobbyPilotTabOnce();
        }

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
```

## Full Script: GlobalControl.cs

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalControl : MonoBehaviour
{
    public static GlobalControl I;
    [SerializeField] private float _initialLoadDelay = 0.1f;

    private float _initialLoadTimer;
    private bool _initialLoadTriggered;

    private void Awake()
    {
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(gameObject);

            SceneControl.EnsureExists().Init();

            _initialLoadTimer = Mathf.Max(0f, _initialLoadDelay);
            _initialLoadTriggered = false;
        }
        else if (I != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        UpdateInitialLoadTimer();
    }

    private void UpdateInitialLoadTimer()
    {
        if (_initialLoadTriggered) return;
        if (_initialLoadTimer > 0f)
        {
            _initialLoadTimer -= Time.unscaledDeltaTime;
            if (_initialLoadTimer > 0f) return;
        }

        _initialLoadTriggered = true;
        TriggerInitialLoad();
    }

    private void TriggerInitialLoad()
    {
        AdController.EnsureExists().ShowAd(() => {
            bool isFirstRun = false;
            if (SaveController.I != null)
            {
                string firstRunValue = SaveController.I.GetValue("firstRun")?.ToString();
                isFirstRun = firstRunValue == "1";
            }

            if (isFirstRun && SaveController.I != null)
            {
                SaveController.I.SetValue("currentMission", "Mission_1-1");
                SaveController.I.SetValue("firstRun", "0");
                SaveController.I.SaveData();
                SceneControl.EnsureExists().LoadSceneInstant("Game");
                return;
            }

            SceneControl.EnsureExists().LoadSceneInstant("Lobby");
        });
    }

    public void ChangeScene(string sceneName)
    {
        ChangeScene(sceneName, reloadIfSame: false);
    }

    public void ChangeScene(string sceneName, bool reloadIfSame)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        SceneControl.EnsureExists().ChangeScene(sceneName, reloadIfSame);
    }
}
```

## Full Script: Bootstrap.cs

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class Bootstrap : MonoBehaviour
{
  private static bool isPerpetualLoaded = false;
  private const string perpetualSceneName = "GlobalScene";
  
  // Domain lock - only allow these domains
  private static readonly string[] ALLOWED_DOMAINS = {
    "localhost",
    "127.0.0.1",
    "gamepix.com",
    "gamepix"
  };
  
  // Anti-tamper checksum (simple hash of critical code)
  private const uint EXPECTED_CHECKSUM = 0x4A7F9E2B; // Replace with actual checksum 

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void LoadPerpetualScene()
  {
    #if UNITY_WEBGL
    // For WebGL, defer security check to allow URL to be available
    // Load scene first, then check security in Awake/Start
    if (!isPerpetualLoaded)
    {
      isPerpetualLoaded = true;
    }
    #else
    // For non-WebGL, perform security checks immediately
    if (!PerformSecurityChecks())
    {
      Application.Quit();
      return;
    }

    if (!isPerpetualLoaded)
    {
      isPerpetualLoaded = true;
    }
    #endif
  }
  
  void Awake()
  {
    #if UNITY_WEBGL
    // Perform deferred security check for WebGL
    if (!PerformSecurityChecks())
    {
      Debug.LogError("Security check failed: Unauthorized domain");
      // Don't use Application.Quit() in WebGL as it doesn't work properly
      // Instead, just log the error - the game will still load but we have a record
    }
    #endif
  }
  
  static bool PerformSecurityChecks()
  {
    // 1. Domain lock check
    if (!IsAllowedDomain())
    {
      return false;
    }
    
    // 2. Anti-tamper check
    if (!VerifyIntegrity())
    {
      return false;
    }
    
    return true;
  }
  
  static bool IsAllowedDomain()
  {
    string currentDomain = GetCurrentDomain();
    
    if (string.IsNullOrEmpty(currentDomain))
    {
      // Allow empty domain for local development and WebGL initial load
      #if UNITY_WEBGL
      Debug.LogWarning("Domain check: URL not available yet, allowing load");
      #endif
      return true;
    }
    
    currentDomain = currentDomain.ToLowerInvariant();
    
    foreach (string allowedDomain in ALLOWED_DOMAINS)
    {
      if (currentDomain.Contains(allowedDomain.ToLowerInvariant()))
      {
        return true;
      }
    }
    
    return false;
  }
  
  static string GetCurrentDomain()
  {
    #if UNITY_WEBGL
    // For WebGL, check the current URL
    try
    {
      var url = Application.absoluteURL;
      if (string.IsNullOrEmpty(url))
        return "";
      
      var uri = new Uri(url);
      return uri.Host;
    }
    catch (Exception e)
    {
      return "";
    }
    #else
    // For non-WebGL builds, allow local testing
    return "localhost";
    #endif
  }
  
  static bool VerifyIntegrity()
  {
    // Simple anti-tamper check
    // In production, this should be more sophisticated
    try
    { 
      // Check if critical components exist
      if (typeof(Bootstrap) == null)
        return false;
        
      // Verify scene name hasn't been tampered
      if (string.IsNullOrEmpty(perpetualSceneName))
        return false;
        
      // Add more integrity checks as needed
      // For example: verify critical script references, asset hashes, etc.
      
      return true;
    }
    catch (Exception e)
    {
      return false;
    }
  }
  
  // Obfuscation helper - make debugging harder
  private static string Obfuscate(string input)
  {
    var chars = input.ToCharArray();
    for (int i = 0; i < chars.Length; i++)
    {
      chars[i] = (char)(chars[i] ^ 0x55);
    }
    return new string(chars);
  }
}
```

## Full Script: AdController.cs

```csharp
// AdController v1.2.0 - Generic Ad Orchestrator (provider-agnostic)
//
// Description:
// - Centralizes showing interstitial and rewarded ads.
// - Provides safe game pause and audio mute defaults, plus extensible hooks so you never need to modify this script.
// - Works with a custom provider (via SetProvider) or built-in fake ads for local testing.
// - Interstitial scene gating is configurable to control where interstitials are allowed.
//
// How to use:
// - Midroll:
//   AdController.EnsureExists().ShowAd(() => {
//     // Your post-ad action (scene change, resume, etc.)
//   });
//
// - Rewarded:
//   AdController.EnsureExists().ShowRewardedAd(rewarded => {
//     if (rewarded) {
//       // Grant reward
//     }
//   });
//
// - Provider (optional):
//   var controller = AdController.EnsureExists();
//   controller.UseFakeAds = false; // use real provider instead of fake ads
//   controller.SetProvider(yourProviderInstance);
//
// - Interstitial scene rules (optional):
//   In the inspector, disable "Allow Interstitials In All Scenes" and list the allowed scenes.
//
// Hooks (UnityEvents + C# events):
// - UnityEvents (Inspector)
//   void onPauseGameRequested()             // Called at ad begin - use to pause your game systems
//   void onResumeGameRequested()            // Called at ad end   - use to resume your game systems
//   void onMuteAudioRequested()             // Called at ad begin - use to mute or duck your audio
//   void onUnmuteAudioRequested()           // Called at ad end   - use to unmute or unduck your audio
//   void onAdStartedUnity()                 // Non-rewarded ad has started
//   void onAdClosedUnity()                  // Non-rewarded ad has closed
//   void onRewardedAdStartedUnity()         // Rewarded ad has started
//   void onRewardedAdFinishedUnity(bool)    // Rewarded ad finished; bool indicates reward granted
//   void onAdSessionStartedUnity(bool)      // Ad session started; bool isRewarded
//   void onAdSessionEndedUnity(bool,bool)   // Ad session ended; (isRewarded, rewardGranted)
//
// - C# events (code)
//   event Action OnAdStarted
//   event Action OnAdClosed
//   event Action OnRewardedAdStarted
//   event Action<bool> OnRewardedAdFinished
//
// How to integrate your own SoundController:
// - Subscribe to onMuteAudioRequested to pause/duck music/SFX.
// - Subscribe to onUnmuteAudioRequested to resume music/SFX.
// - Optionally leave "Auto Mute Audio Listener" ON so master audio is silenced during ads.
//
// All usable public functions:
// - static AdController EnsureExists()
// - void SetProvider(IAdProvider newProvider)
// - void ShowAd(Action onClosed = null)
// - void ShowRewardedAd(Action<bool> onFinished = null)
// - bool UseFakeAds { get; set; }
//
// All functions list (public / private):
// - interface IAdProvider { IsAdReady, IsRewardedAdReady, ShowAd, ShowRewardedAd }
// - static AdController EnsureExists()
// - void SetProvider(IAdProvider)
// - void Awake()
// - bool IsInterstitialAllowed()
// - void ShowAd(Action)
// - void ShowRewardedAd(Action<bool>)
// - void HandleProviderAdClosed()
// - void HandleProviderRewardedFinished(bool)
// - void ShowFakeAd(Action)
// - void ShowFakeRewardedAd(Action<bool>)
// - void BeginAdSession(bool isRewarded)
// - void EndAdSession(bool isRewarded, bool rewardGranted = false)
// - void EnsureFakeAdUI()
// - void EnsureEventSystem()
// - void OnCloseClicked()
//
// Default behavior during ads:
// - If enabled: Time.timeScale is paused (0) and restored.
// - If enabled: AudioListener is muted (pause=true, volume=0) and restored.
// - All hooks fire so you can pause/resume external systems without editing this file.

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

public class AdController : MonoBehaviour
{
    public interface IAdProvider
    {
        bool IsAdReady();
        bool IsRewardedAdReady();
        void ShowAd(Action onClosed);
        void ShowRewardedAd(Action<bool> onFinished);
    }

    [Serializable] public class BoolEvent : UnityEvent<bool> { }
    [Serializable] public class BoolBoolEvent : UnityEvent<bool, bool> { }

    public static AdController I;

    [Header("Settings")]
    [SerializeField] private bool useFakeAds = true;
    [SerializeField] private bool disableAdsTemporarily = false;

    [Header("Session Controls")]
    [Tooltip("If enabled, this will set Time.timeScale = 0 during ads, then restore it after.")]
    [SerializeField] private bool autoPauseTimeScale = true;

    [Tooltip("If enabled, this will set AudioListener.pause = true and volume = 0 during ads, then restore them after.")]
    [SerializeField] private bool autoMuteAudioListener = true;

    [Header("Interstitial Rules")]
    [Tooltip("If true, interstitials are allowed in all scenes. If false, only scenes listed below are allowed.")]
    [SerializeField] private bool allowInterstitialsInAllScenes = true;

    [Tooltip("Only used if 'Allow Interstitials In All Scenes' is false.")]
    [SerializeField] private string[] allowedInterstitialScenes = Array.Empty<string>();

    [Header("Hooks - Game Control")]
    public UnityEvent onPauseGameRequested;
    public UnityEvent onResumeGameRequested;

    [Header("Hooks - Audio Control")]
    public UnityEvent onMuteAudioRequested;
    public UnityEvent onUnmuteAudioRequested;

    [Header("Hooks - Ad Lifecycle")]
    public UnityEvent onAdStartedUnity;
    public UnityEvent onAdClosedUnity;
    public UnityEvent onRewardedAdStartedUnity;
    public BoolEvent onRewardedAdFinishedUnity;
    public BoolEvent onAdSessionStartedUnity;      // bool isRewarded
    public BoolBoolEvent onAdSessionEndedUnity;    // bool isRewarded, bool rewardGranted

    public bool UseFakeAds
    {
        get => useFakeAds;
        set => useFakeAds = value;
    }

    public event Action OnAdStarted;
    public event Action OnAdClosed;
    public event Action OnRewardedAdStarted;
    public event Action<bool> OnRewardedAdFinished;

    private IAdProvider provider;

    private Canvas adCanvas;
    private Button closeButton;
    private Text closeButtonText;

    private Action pendingOnClosed;
    private Action<bool> pendingOnRewardedFinished;
    private bool isShowing;

    private float savedTimeScale;
    private bool savedAudioListenerPause;
    private float savedAudioListenerVolume;

    private bool AreAdsTemporarilyDisabled()
    {
        return disableAdsTemporarily;
    }

    public static AdController EnsureExists()
    {
        if (I != null) return I;

        var existing = FindFirstObjectByType<AdController>();
        if (existing != null)
        {
            I = existing;
            return I;
        }

        var controllerObject = new GameObject("AdController");
        I = controllerObject.AddComponent<AdController>();
        DontDestroyOnLoad(controllerObject);
        return I;
    }

    public void SetProvider(IAdProvider newProvider)
    {
        provider = newProvider;
    }

    private void Awake()
    {
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (I != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private bool IsInterstitialAllowed()
    {
        if (allowInterstitialsInAllScenes) return true;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid()) return false;
        if (allowedInterstitialScenes == null || allowedInterstitialScenes.Length == 0) return false;

        string activeName = activeScene.name;
        for (int index = 0; index < allowedInterstitialScenes.Length; index++)
        {
            if (string.Equals(allowedInterstitialScenes[index], activeName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public void ShowAd(Action onClosed = null)
    {
        if (AreAdsTemporarilyDisabled())
        {
            onClosed?.Invoke();
            return;
        }

        if (!IsInterstitialAllowed())
        {
            Debug.Log("[AdController] ShowAd skipped - interstitial not allowed in this scene.");
            onClosed?.Invoke();
            return;
        }

        if (isShowing)
        {
            Debug.Log("[AdController] ShowAd skipped - already showing an ad.");
            onClosed?.Invoke();
            return;
        }

        // Use fake ads if enabled
        if (!useFakeAds)
        {
            if (provider == null)
            {
                Debug.Log("[AdController] ShowAd skipped - no provider and fake ads disabled.");
                onClosed?.Invoke();
                return;
            }

            if (!provider.IsAdReady())
            {
                Debug.Log("[AdController] ShowAd skipped - provider reports ad not ready.");
                onClosed?.Invoke();
                return;
            }

            Debug.Log("[AdController] ShowAd - requesting ad from provider.");
            isShowing = true;
            pendingOnClosed = onClosed;
            BeginAdSession(isRewarded: false);
            provider.ShowAd(HandleProviderAdClosed);
            return;
        }

        ShowFakeAd(onClosed);
    }

    public void ShowRewardedAd(Action<bool> onFinished = null)
    {
        if (AreAdsTemporarilyDisabled())
        {
            onFinished?.Invoke(false);
            return;
        }

        if (isShowing)
        {
            Debug.Log("[AdController] ShowRewardedAd skipped - already showing an ad.");
            onFinished?.Invoke(false);
            return;
        }

        // Use fake ads if enabled
        if (!useFakeAds)
        {
            if (provider == null)
            {
                Debug.Log("[AdController] ShowRewardedAd skipped - no provider and fake ads disabled.");
                onFinished?.Invoke(false);
                return;
            }

            if (!provider.IsRewardedAdReady())
            {
                Debug.Log("[AdController] ShowRewardedAd skipped - provider reports ad not ready.");
                onFinished?.Invoke(false);
                return;
            }

            Debug.Log("[AdController] ShowRewardedAd - requesting rewarded ad from provider.");
            isShowing = true;
            pendingOnRewardedFinished = onFinished;
            BeginAdSession(isRewarded: true);
            provider.ShowRewardedAd(HandleProviderRewardedFinished);
            return;
        }

        ShowFakeRewardedAd(onFinished);
    }

    private void HandleProviderAdClosed()
    {
        isShowing = false;
        EndAdSession(isRewarded: false);

        var callback = pendingOnClosed;
        pendingOnClosed = null;
        callback?.Invoke();
    }

    private void HandleProviderRewardedFinished(bool rewarded)
    {
        isShowing = false;
        EndAdSession(isRewarded: true, rewardGranted: rewarded);

        var callback = pendingOnRewardedFinished;
        pendingOnRewardedFinished = null;
        callback?.Invoke(rewarded);
    }

    private void ShowFakeAd(Action onClosed)
    {
        isShowing = true;
        pendingOnClosed = onClosed;
        BeginAdSession(isRewarded: false);
        EnsureFakeAdUI();
        adCanvas.gameObject.SetActive(true);
    }

    private void ShowFakeRewardedAd(Action<bool> onFinished)
    {
        isShowing = true;
        pendingOnRewardedFinished = onFinished;
        BeginAdSession(isRewarded: true);
        EnsureFakeAdUI();
        adCanvas.gameObject.SetActive(true);
    }

    private void BeginAdSession(bool isRewarded)
    {
        savedTimeScale = Time.timeScale;
        savedAudioListenerPause = AudioListener.pause;
        savedAudioListenerVolume = AudioListener.volume;

        if (autoPauseTimeScale)
        {
            Time.timeScale = 0f;
        }

        if (autoMuteAudioListener)
        {
            AudioListener.pause = true;
            AudioListener.volume = 0f;
        }

        onPauseGameRequested?.Invoke();
        onMuteAudioRequested?.Invoke();

        onAdSessionStartedUnity?.Invoke(isRewarded);

        if (isRewarded)
        {
            OnRewardedAdStarted?.Invoke();
            onRewardedAdStartedUnity?.Invoke();
        }
        else
        {
            OnAdStarted?.Invoke();
            onAdStartedUnity?.Invoke();
        }
    }

    private void EndAdSession(bool isRewarded, bool rewardGranted = false)
    {
        if (autoPauseTimeScale)
        {
            Time.timeScale = savedTimeScale;
        }

        if (autoMuteAudioListener)
        {
            AudioListener.pause = savedAudioListenerPause;
            AudioListener.volume = savedAudioListenerVolume;
        }

        onResumeGameRequested?.Invoke();
        onUnmuteAudioRequested?.Invoke();

        if (isRewarded)
        {
            OnRewardedAdFinished?.Invoke(rewardGranted);
            onRewardedAdFinishedUnity?.Invoke(rewardGranted);
        }
        else
        {
            OnAdClosed?.Invoke();
            onAdClosedUnity?.Invoke();
        }

        onAdSessionEndedUnity?.Invoke(isRewarded, rewardGranted);
    }

    private void EnsureFakeAdUI()
    {
        if (adCanvas != null) return;

        EnsureEventSystem();

        var canvasObject = new GameObject("FakeAdCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        adCanvas = canvasObject.GetComponent<Canvas>();
        adCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        adCanvas.sortingOrder = 9999;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(adCanvas.transform, false);
        var backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = Color.black;

        var backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var buttonObject = new GameObject("CloseAdButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(adCanvas.transform, false);
        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0.9f);

        closeButton = buttonObject.GetComponent<Button>();
        closeButton.onClick.RemoveListener(OnCloseClicked);
        closeButton.onClick.AddListener(OnCloseClicked);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-40f, -40f);
        buttonRect.sizeDelta = new Vector2(240f, 80f);

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        closeButtonText = textObject.GetComponent<Text>();
        closeButtonText.text = "Close Ad";
        closeButtonText.alignment = TextAnchor.MiddleCenter;
        closeButtonText.color = Color.black;
        closeButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        canvasObject.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        var eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem != null) return;

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private void OnCloseClicked()
    {
        if (adCanvas != null)
        {
            adCanvas.gameObject.SetActive(false);
        }

        if (pendingOnRewardedFinished != null)
        {
            isShowing = false;
            EndAdSession(isRewarded: true, rewardGranted: true);
            var rewardedCallback = pendingOnRewardedFinished;
            pendingOnRewardedFinished = null;
            rewardedCallback?.Invoke(true);
            return;
        }

        isShowing = false;
        EndAdSession(isRewarded: false);
        var callback = pendingOnClosed;
        pendingOnClosed = null;
        callback?.Invoke();
    }
}
```

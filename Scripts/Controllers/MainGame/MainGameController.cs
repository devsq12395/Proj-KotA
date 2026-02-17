using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainGameController : MonoBehaviour {
  public static MainGameController I;

  void Awake() {
    EnsureGlobalSceneLoaded();

    if (I == null) { I = this; } 
    else { Destroy(gameObject);} 
  }

  [Header("Define these on Editor")]
  public Camera mainCamera;

  [Header("System Variables")]
  public bool isPaused;

  #region -- Main --
  void Start(){
    // Load Data
    SaveController.I.LoadData();
    
    CharacterController.I.OnStart();

  }

  void Update(){
    if(isPaused){
      return;
    }

    CharacterController.I.OnUpdate();
    EffectsController.I.OnUpdate();
    MissileController.I.OnUpdate();
  }
  #endregion

  #region Load Global Scene
  static bool _hasRequestedEnsureGlobalScene; 
  static void EnsureGlobalSceneLoaded()
  {
    Scene globalScene = SceneManager.GetSceneByName("GlobalScene");
    if (globalScene.IsValid() && globalScene.isLoaded) return;
    if (_hasRequestedEnsureGlobalScene) return;
    if (!Application.CanStreamedLevelBeLoaded("GlobalScene")) return;

    _hasRequestedEnsureGlobalScene = true;
    SceneManager.LoadScene("GlobalScene", LoadSceneMode.Additive);
  }
  #endregion
}
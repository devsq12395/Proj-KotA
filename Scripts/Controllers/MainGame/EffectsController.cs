using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class EffectsController : MonoBehaviour {
  public static EffectsController I;
  // Effects Z should be in front of characters. Characters are around -20f; use a slightly higher (closer to camera) Z.
  public const float EffectsZ = -19.6f;
  void Awake() {
    if (gameObject.scene != null && gameObject.scene.IsValid())
    {
      if (gameObject.scene.name != "Game")
      {
        Destroy(gameObject);
        return;
      }
    }

    if (I == null) { I = this; } 
    else { Destroy(gameObject);}
  }

  public List<InGameObject> effects;
  List<InGameObject> effectsToDestroy = new List<InGameObject>();
  List<GameObject> effectGOsToDestroy = new List<GameObject>();

  private void MoveToGameScene(GameObject go)
  {
    if (go == null) return;
    Scene gameScene = SceneManager.GetSceneByName("Game");
    if (!gameScene.IsValid() || !gameScene.isLoaded) return;
    if (go.scene == gameScene) return;
    SceneManager.MoveGameObjectToScene(go, gameScene);
  }

  public InGameObject CreateEffect(string _name, Vector2 _pos, float _angle = 0f){
    if (Database_MainGame.I == null) return null;
    GameObject _origData = Database_MainGame.I.effects.Find(i => i != null && i.name == _name);

    if(_origData == null) {
      Debug.LogWarning($"Effect {_name} not found");
      return null;
    }

    GameObject _newEffect = Instantiate(_origData);
    MoveToGameScene(_newEffect);
    _newEffect.transform.position = new Vector3(_pos.x, _pos.y, EffectsZ);
    _newEffect.transform.rotation = Quaternion.Euler(0f, 0f, _angle);

    InGameObject _inGameObject = _newEffect.GetComponent<InGameObject>();
    if (_inGameObject == null){
      return null;
    }
    effects.Add(_inGameObject);

    _inGameObject.SetID();
    return _inGameObject;
  }

  public void CreateDelayedEffect(string _name, Vector2 _pos, float _angle = 0f, float _delay = 0f){
    StartCoroutine(_CreateDelayedEffectRoutine(_name, _pos, _angle, _delay));
  }

  IEnumerator _CreateDelayedEffectRoutine(string _name, Vector2 _pos, float _angle, float _delay){
    if (_delay > 0f) yield return new WaitForSeconds(_delay);
    CreateEffect(_name, _pos, _angle);
  }

  public void OnUpdate(){
    foreach(InGameObject _effect in effects){
      MovementController.I.MoveUpdate(_effect);
    }

    if(effectsToDestroy.Count > 0){
      foreach(var _e in effectsToDestroy){
        if(_e == null) continue;
        if(effects.Contains(_e)) effects.Remove(_e);
        Destroy(_e.gameObject);
      }
      effectsToDestroy.Clear();
    }

    if(effectGOsToDestroy.Count > 0){
      foreach(var go in effectGOsToDestroy){
        if(go == null) continue;
        Destroy(go);
      }
      effectGOsToDestroy.Clear();
    }
  }

  public void DestroyEffect(InGameObject _effect){
    if(_effect == null) return;
    if(!effectsToDestroy.Contains(_effect)) effectsToDestroy.Add(_effect);
  }

  public void DestroyEffect_GameObject(GameObject _effect){
    InGameObject _inGameObject = _effect.GetComponent<InGameObject>();
    if(_inGameObject){
      int destroyedId = _inGameObject.id;
      DestroyEffect(_inGameObject);
    }else{
      if(_effect != null && !effectGOsToDestroy.Contains(_effect)) effectGOsToDestroy.Add(_effect);
    }
  }

  #region -- Radiating Effects --
  // Creates multiple identical effects at a single origin and sends them outward at evenly spaced angles
  // with constant speed. Each spawned effect fades its opacity over 'lifetime' seconds, then is destroyed.
  // Usage:
  //   EffectsController.I.CreateRadiatingEffects(
  //     effectName: "smoke-01", origin: pos, count: 8, startAngleDeg: 0f, angleStepDeg: 45f,
  //     speed: 3f, lifetime: 1.25f, startAlpha: 1f, endAlpha: 0f);
  public void CreateRadiatingEffects(
    string effectName,
    Vector2 origin,
    int count = 8,
    float startAngleDeg = 0f,
    float angleStepDeg = 45f,
    float speed = 3f,
    float lifetime = 1.25f,
    float startAlpha = 1f,
    float endAlpha = 0f
  ){
    if (count <= 0) return;
    float angle = startAngleDeg;
    for (int i = 0; i < count; i++){
      var eff = CreateEffect(effectName, origin, angle);
      if (eff != null){
        eff.movement.isLinearMoving = true;
        eff.movement.linearAngle = angle;
        eff.movement.linearSpeed = speed;
        StartCoroutine(FadeAndDestroyRoutine(eff, lifetime, startAlpha, endAlpha));
      }
      angle += angleStepDeg;
    }
  }

  IEnumerator FadeAndDestroyRoutine(InGameObject effect, float lifetime, float startAlpha, float endAlpha){
    if (effect == null) yield break;
    float t = 0f;

    // Common renderers we might find on an effect
    SpriteRenderer sr = effect.GetComponentInChildren<SpriteRenderer>(true);
    CanvasGroup cg = effect.GetComponentInChildren<CanvasGroup>(true);
    Image uiImage = effect.GetComponentInChildren<Image>(true);
    TMP_Text tmp = effect.GetComponentInChildren<TMP_Text>(true);

    Color srStart = sr ? sr.color : Color.white;
    Color uiStart = uiImage ? uiImage.color : Color.white;
    Color tmpStart = tmp ? tmp.color : Color.white;

    while (t < lifetime){
      t += Time.deltaTime;
      float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, lifetime));
      float a = Mathf.Lerp(startAlpha, endAlpha, u);
      if (sr) { var c = srStart; c.a = a; sr.color = c; }
      if (uiImage) { var c = uiStart; c.a = a; uiImage.color = c; }
      if (tmp) { var c = tmpStart; c.a = a; tmp.color = c; }
      if (cg) cg.alpha = a;
      yield return null;
    }

    DestroyEffect(effect);
  }
  #endregion
}
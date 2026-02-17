using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissileController : MonoBehaviour {
  public static MissileController I;

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
    if (missiles == null) missiles = new List<InGameObject>();
  }

  public List<InGameObject> missiles;
  List<InGameObject> missilesToDestroy = new List<InGameObject>();


  #region -- Create --
  public InGameObject CreateMissile(string _name, Vector2 _pos, InGameObject _ownerObj){
    GameObject _origData = Database_MainGame.I.missiles.Find(i => i.name == _name);

    GameObject _newMissile = Instantiate(_origData);
    _newMissile.transform.position = new Vector3(_pos.x, _pos.y, -19.5f);

    InGameObject _inGameObject = _newMissile.GetComponent<InGameObject>();
    _inGameObject.ownerObj = _ownerObj;
    _inGameObject.owner = _ownerObj.owner;

    missiles.Add(_inGameObject);

    return _inGameObject;
  }

  public InGameObject CreateMissileOnMuzzleFlashPoint(string _name, InGameObject _owner){
    if (_owner == null) return null;

    Vector2 spawnPos = _owner.transform.position;
    // Prefer reference point named "muzzle" if present; otherwise use the first reference point if any
    Vector2 local;
    if (_owner.TryGetReferencePoint("muzzle", out local)){
      var world = _owner.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
      spawnPos = new Vector2(world.x, world.y);
    } else if (_owner.referencePoints != null && _owner.referencePoints.Count > 0){
      var rp = _owner.referencePoints[0];
      var world = _owner.transform.TransformPoint(new Vector3(rp.position.x, rp.position.y, 0f));
      spawnPos = new Vector2(world.x, world.y);
    }

    return CreateMissile(_name, spawnPos, _owner);
  }
  #endregion

  public void OnUpdate(){
    foreach(InGameObject _missile in missiles){
      if(_missile == null) continue;
      MovementController.I.MoveUpdate(_missile);
      Update_TrailEffects(_missile);
    }

    if(missilesToDestroy.Count > 0){
      foreach(var _m in missilesToDestroy){
        if(_m == null) continue;

        OnDestroyFunctions.I.HandleOnDestroyExtraCodes(_m);
        if(missiles.Contains(_m)) missiles.Remove(_m);
        Destroy(_m.gameObject);
      }
      missilesToDestroy.Clear();
    }
  }

  public void DestroyMissile(InGameObject _missile){
    if(_missile == null) return;
    if (_missile.tags != null && _missile.tags.Count > 0){
      for (int i = 0; i < _missile.tags.Count; i++){
        string t = _missile.tags[i];
        if (string.IsNullOrEmpty(t)) continue;
        if (t.ToLowerInvariant() == "indestructible"){
          return;
        }
      }
    }
    if(!missilesToDestroy.Contains(_missile)) missilesToDestroy.Add(_missile);
  }

  #region -- Set - Linear --
  public void SetMissileDetails_Linear(InGameObject _missile, InGameObject _ownerObj, float _speed, int _attack){
    _missile.owner = _ownerObj.owner;
    _missile.ownerObj = _ownerObj;
    MovementController.I.SetLinearMove(_missile, _speed, _ownerObj.angle);
    _missile.attack = _attack;
  }
  #endregion

  #region -- Trail Effects --
  void Update_TrailEffects(InGameObject _obj){
    if (_obj == null || _obj.gameObject == null) return;
    if (_obj.trailEffects == null || _obj.trailEffects.Count == 0) return;
    // Ensure runtime trailStates matches trailEffects count
    if (_obj.trailStates == null) _obj.trailStates = new List<TrailEffectState>(_obj.trailEffects.Count);
    if (_obj.trailStates.Count != _obj.trailEffects.Count){
      var newStates = new List<TrailEffectState>(_obj.trailEffects.Count);
      for (int i = 0; i < _obj.trailEffects.Count; i++){
        newStates.Add(new TrailEffectState{ config = _obj.trailEffects[i], timer = 0f });
      }
      _obj.trailStates = newStates;
    }

    for (int i = 0; i < _obj.trailEffects.Count; i++){
      var cfg = _obj.trailEffects[i];
      var st = _obj.trailStates[i];
      if (cfg == null) continue;
      float interval = Mathf.Max(0f, cfg.spawnEffectPerSec);
      if (interval <= 0f) continue;
      st.timer += Time.deltaTime;
      while (st.timer >= interval){
        st.timer -= interval;
        // Determine angle
        float ang = 0f;
        if (cfg.inheritAngleFromMovement){
          ang = _obj.movement.isLinearMoving ? _obj.movement.linearAngle : _obj.angle;
        }
        ang += cfg.extraAngleOffset;

        // Determine spawn position (optional reference point)
        Vector2 spawn = _obj.transform.position;
        if (!string.IsNullOrEmpty(cfg.sourceRefPointName)){
          Vector2 lp;
          if (_obj.TryGetReferencePoint(cfg.sourceRefPointName, out lp)){
            var w = _obj.transform.TransformPoint(new Vector3(lp.x, lp.y, 0f));
            spawn = new Vector2(w.x, w.y);
          }
        }

        if (EffectsController.I != null && !string.IsNullOrEmpty(cfg.effectName)){
          EffectsController.I.CreateEffect(cfg.effectName, spawn, ang);
        }
      }
    }
  }
  #endregion
}
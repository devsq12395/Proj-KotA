using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class CharacterController : MonoBehaviour {
  public static CharacterController I;

  void Awake() {
    // Only allow CharacterController to live in the Game scene
    if (gameObject.scene != null && gameObject.scene.IsValid())
    {
      if (gameObject.scene.name != "Game")
      {
        Destroy(gameObject);
        return;
      }

    }

    if (I == null) { I = this; }
    else { Destroy(gameObject); }

    if (characters == null) characters = new List<InGameObject>();
  }

  public List<InGameObject> characters;
  List<InGameObject> charactersToDestroy = new List<InGameObject>();

  #region -- On Start --
  public void OnStart(){
    
  }
  #endregion

  void Update(){
    OnUpdate();
  }

  #region -- Create --
  public InGameObject CreateCharacter(string _name, Vector2 _pos, int _owner){
    if (Database_MainGame.I.characters.Find(i => i.name == _name) == null){
      Debug.LogError($"Character '{_name}' not found in Database_MainGame.");
      return null;
    }

    GameObject _origData = Database_MainGame.I.characters.Find(i => i.name == _name);

    GameObject _newChar = Instantiate(_origData);
    MoveToGameScene(_newChar);
    _newChar.transform.position = new Vector3(_pos.x, _pos.y, -19.5f);
    
    InGameObject _inGameObject = _newChar.GetComponent<InGameObject>();
    characters.Add(_inGameObject);
    _inGameObject.owner = _owner;

    // Ensure AIBase is present to execute RTS orders and drive movement updates
    var ai = _newChar.GetComponent<AIBase>();
    if (ai == null) ai = _newChar.AddComponent<AIBase>();
    ai.enabled = true;

    return _inGameObject;
  }
  #endregion
  
  #region -- Create System Helpers --
  private void MoveToGameScene(GameObject go) {
    if (go == null) return;
    Scene gameScene = SceneManager.GetSceneByName("Game");
    if (!gameScene.IsValid() || !gameScene.isLoaded) return;
    if (go.scene == gameScene) return;

    SceneManager.MoveGameObjectToScene(go, gameScene);
  }
  #endregion

  #region -- Gets --
  public float GetDistance(InGameObject _a, InGameObject _b){
    if (_a == null || _b == null) return 0f;
    if (_a.gameObject == null || _b.gameObject == null) return 0f;
    return Vector3.Distance(_a.transform.position, _b.transform.position);
  }
  #endregion

  #region -- On Update --
  public void OnUpdate(){
    // Process deferred destroys once
    if(charactersToDestroy.Count > 0){
      foreach(var _c in charactersToDestroy){
        if(_c == null) continue; 
        OnDestroyFunctions.I.HandleOnDestroyExtraCodes(_c);
        if(characters != null && characters.Contains(_c)) characters.Remove(_c);
        if(_c.gameObject != null) Destroy(_c.gameObject);
      }
      charactersToDestroy.Clear();
    }

    // Drive per-character movement updates
    if (characters != null && MovementController.I != null){
      for(int i = 0; i < characters.Count; i++){
        var ch = characters[i];
        if (ch == null || ch.gameObject == null) continue;
        MovementController.I.MoveUpdate(ch);
      }
    }
  }
  #endregion

  #region -- Damage System --
  public void DealDamage(InGameObject _target, InGameObject _attacker, int _damage){
    if (!Checks_MainGame.I.CheckDealDamage(_target, _attacker, _damage)) return;

    _damage = OnDamageFunctions.I.HandleOnDamageExtraCodes(_target, _attacker, _damage);

    _target.hp -= _damage;

    if(_target.hp <= 0){
      DestroyCharacter(_target);
    }
  }

  public void DealDamage_AOE(InGameObject _attacker, int _damage, float _range){
    foreach(InGameObject _target in characters){
      if(_target.owner == _attacker.owner) continue;
      if(!_target || !_attacker) continue;

      if(Vector2.Distance(_target.transform.position, _attacker.transform.position) > _range) continue;
      DealDamage(_target, _attacker, _damage);
    }
  }

  public void DestroyCharacter(InGameObject _target){
    if(_target == null) return;

    if(!charactersToDestroy.Contains(_target)) charactersToDestroy.Add(_target);
  }
  #endregion
}
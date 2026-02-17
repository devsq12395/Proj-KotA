using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OnCollisionFunctions : MonoBehaviour {
  public static OnCollisionFunctions I;
  void Awake() {
    if (I == null) { I = this; } 
    else { Destroy(gameObject);}
  }

private List<CollisionHandled> collisionHandledList = new List<CollisionHandled>();

  private Dictionary<long, float> energyBallLastDamageTime = new Dictionary<long, float>();

  private long GetPairKey(int id1, int id2){
    int a = id1 < id2 ? id1 : id2;
    int b = id1 < id2 ? id2 : id1;
    return ((long)a << 32) | (uint)b;
  }

  #region -- Main --
  public void HandleCollision(InGameObject _collider, InGameObject _collided){
    int _colliderId = _collider.id, _collidedId = _collided.id;
    
    if(CheckCollisionIsHandled(_colliderId, _collidedId)) return;

    #region -- Missiles --
    switch(_collider.name){
      default: 
        if(_collider.type == "Missile"){
          HandleCollision_Generic(_collider, _collided);
        }
        break;
    }
    #endregion
  } 
  #endregion


  #region -- Helpers - Generic --
  private void HandleCollision_Generic(InGameObject _collider, InGameObject _collided){
    if(!Checks_MainGame.I.MissileCanHitEnemy(_collider, _collided)) return;
    
    CharacterController.I.DealDamage(_collided, _collider.ownerObj, _collider.attack);
    MissileController.I.DestroyMissile(_collider);
    EffectsController.I.CreateEffect("explosion-1", _collided.transform.position);
    if (SoundsController.I != null){
      Vector2 pos = new Vector2(_collided.transform.position.x, _collided.transform.position.y);
      SoundsController.I.PlaySoundInGame("explode-mini-missile", pos);
    }
  }

  private bool CheckCollisionIsHandled(int _id1, int _id2){
    foreach(CollisionHandled entry in collisionHandledList){
      if ((entry.id1 == _id1 && entry.id2 == _id2) || (entry.id1 == _id2 && entry.id2 == _id1)) {
        return true;
      }
    }
    return false;
  }
  #endregion

}
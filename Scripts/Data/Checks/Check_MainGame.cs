using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class Checks_MainGame : MonoBehaviour
{
  public static Checks_MainGame I;

  void Awake() {
      if (I == null) { I = this; } 
      else { Destroy(gameObject); }
  }
  public bool MissileCanHitEnemy(InGameObject _missile, InGameObject _enemy){
    if(_missile.owner == _enemy.owner) return false;
    return true;
  }

  public bool CheckDealDamage(InGameObject _target, InGameObject _attacker, int _damage){
    if (_target != null && _target.isInvul) return false;
    return true;
  }
}
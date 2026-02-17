using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OnDamageFunctions : MonoBehaviour {
  public static OnDamageFunctions I;
  void Awake() {
    if (I == null) { I = this; } 
    else { Destroy(gameObject);}
  }

  public int HandleOnDamageExtraCodes(InGameObject _target, InGameObject _attacker, int _damage){
    // Damage Calculation

    // All Damage Calculations are over from this point
    
    return _damage;
  }
}
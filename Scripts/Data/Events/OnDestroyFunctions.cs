using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OnDestroyFunctions : MonoBehaviour {
  public static OnDestroyFunctions I;
  void Awake() {
    if (I == null) { I = this; } 
    else { Destroy(gameObject);}
  }

  public void HandleOnDestroyExtraCodes(InGameObject _target){
    switch(_target.name){
      default: break;
    }
  }
}
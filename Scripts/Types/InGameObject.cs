using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InGameObject : MonoBehaviour {  
  [Header("------ UNITY EDITOR EDITABLE PARTS ------")]
  public string name; 
  public string nameUI;
  public Sprite portrait;
  public string type; // Character, Missile, Effect, Powerup
  public List<string> tags;

  public List<ReferencePoint> referencePoints; // For muzzles, attachments, etc.

  public int hp, hpMax, attack, defense;
  public int damage; // For missiles/effects that deal damage

  [Header("------ NON-EDITABLE PARTS ------")]
  public InGameObject ownerObj;
  public float zPos;
  public int id, owner;

  // RTS Orders
  public Order currentOrder;
  public float engagementRadius = 8f;

  // Booleans
  public bool isRunning, isAtk, isInvul, isAI;

  // Animation
  public Animator anim;
  public bool hasAnim;
  public int toAnim;
  public Renderer renderer;

  // Components
  public Rigidbody2D rb;

  // Movement & Facing
  public float angle, velocity, velocityDuration;
  public string facing;
  
  // Centralized movement data
  public MovementData movement = new MovementData();

  // Trail effects: typed configuration and runtime timers
  public List<TrailEffectConfig> trailEffects; // authoring-time
  public List<TrailEffectState> trailStates;   // runtime timers/state


  void Start(){
    renderer = GetComponent<Renderer>();

    rb = GetComponent<Rigidbody2D>();
    anim = GetComponent<Animator>();
    hasAnim = (anim != null);

    if (movement == null) movement = new MovementData();

    SetID();
  }

  public void SetID(){
    // Cannot set ID twice
    if(id != 0) return;

    // Set ID
    id = Helpers.GenerateRandomIntId(8);
  }

  // Helper: Try get a reference point's local position by name
  public bool TryGetReferencePoint(string rpName, out Vector2 localPos){
    localPos = Vector2.zero;
    if (referencePoints == null || string.IsNullOrEmpty(rpName)) return false;
    for (int i = 0; i < referencePoints.Count; i++){
      var rp = referencePoints[i];
      if (rp != null && rp.name == rpName){
        localPos = rp.position;
        return true;
      }
    }
    return false;
  }

  public void OnTriggerStay2D(Collider2D _collision) {
    InGameObject _obj2_scrpt = _collision.gameObject.GetComponent<InGameObject>();

    if (_obj2_scrpt != null) {
      OnCollisionFunctions.I.HandleCollision(this, _obj2_scrpt);
    }
  }
}

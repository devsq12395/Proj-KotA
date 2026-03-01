using System.Collections.Generic;
using UnityEngine;

public class AIBase : MonoBehaviour {
  public InGameObject objOwner;

  [Header("Combat")]
  public float preferredRange = 3f; // world units
  public float fireInterval = 0.5f; // seconds between DealDamage attempts
  public int damageOverride = 0;     // if > 0, overrides attack stat for damage

  float _fireTimer;

  void Awake(){
    // Only allow AIBase to live in the Game scene
    if (gameObject.scene != null && gameObject.scene.IsValid())
    {
      if (gameObject.scene.name != "Game")
      {
        Destroy(this);
        return;
      }
    }
  }

  void OnEnable(){
    if (objOwner == null) objOwner = GetComponent<InGameObject>();
  }

  void Update(){
    if (objOwner == null || objOwner.gameObject == null) return;
    UpdateAI();
  }

  public void UpdateAI(){
    if (objOwner.currentOrder == null || objOwner.currentOrder.type == OrderType.None) return;
    TickOrder(objOwner, objOwner.currentOrder);
  }

  bool TickOrder(InGameObject obj, Order order){
    if (obj == null || obj.gameObject == null) return false;
    switch(order.type){
      case OrderType.Move:
        return TickMoveOrder(obj, order);
      case OrderType.AttackMove:
        return TickAttackMoveOrder(obj, order);
      default:
        return false;
    }
  }

  bool TickMoveOrder(InGameObject obj, Order order){
    Vector2 pos = obj.transform.position;
    float stopDist = Mathf.Max(0.05f, order.stopDistance);
    Vector2 dest = order.targetPos;

    if ((dest - pos).sqrMagnitude <= stopDist * stopDist){
      if (MovementController.I != null) MovementController.I.StopLinearMove(obj);
      obj.currentOrder = null;
      return true;
    }

    if (MovementController.I != null){
      float speed = Mathf.Max(0.1f, obj.movement != null ? (obj.movement.linearSpeed > 0f ? obj.movement.linearSpeed : 3f) : 3f);
      MovementController.I.SetMoveToTarget(obj, dest, speed, stopDist);
    }

    // Opportunistic combat while moving
    InGameObject enemy = FindNearestEnemy(obj, GetEngagementRadius(obj));
    if (enemy != null){
      FaceTarget(obj, enemy.transform.position);
      TryDealDamage(obj, enemy);
    }
    return true;
  }

  bool TickAttackMoveOrder(InGameObject obj, Order order){
    Vector2 pos = obj.transform.position;
    float stopDist = Mathf.Max(0.05f, order.stopDistance);
    Vector2 dest = order.targetPos;

    InGameObject enemy = FindNearestEnemy(obj, GetEngagementRadius(obj));
    if (enemy != null && enemy.gameObject != null){
      Vector2 toEnemy = (Vector2)enemy.transform.position - pos;
      float dist = toEnemy.magnitude;
      float speed = Mathf.Max(0.1f, obj.movement != null ? (obj.movement.linearSpeed > 0f ? obj.movement.linearSpeed : 3f) : 3f);

      if (dist > Mathf.Max(0.1f, preferredRange)){
        // Approach enemy
        if (MovementController.I != null){
          float ang = Mathf.Atan2(toEnemy.y, toEnemy.x) * Mathf.Rad2Deg;
          MovementController.I.SetLinearMove(obj, speed, ang);
        }
      } else {
        // In range: stop to fire
        if (MovementController.I != null) MovementController.I.StopLinearMove(obj);
      }

      FaceTarget(obj, enemy.transform.position);
      TryDealDamage(obj, enemy);
      return true;
    }

    // No enemy: continue toward destination
    if ((dest - pos).sqrMagnitude <= stopDist * stopDist){
      if (MovementController.I != null) MovementController.I.StopLinearMove(obj);
      obj.currentOrder = null;
      return true;
    }

    if (MovementController.I != null){
      float speed = Mathf.Max(0.1f, obj.movement != null ? (obj.movement.linearSpeed > 0f ? obj.movement.linearSpeed : 3f) : 3f);
      MovementController.I.SetMoveToTarget(obj, dest, speed, stopDist);
    }
    return true;
  }

  float GetEngagementRadius(InGameObject obj){
    if (obj == null) return 8f;
    return obj.engagementRadius > 0f ? obj.engagementRadius : 8f;
  }

  void FaceTarget(InGameObject obj, Vector2 worldPos){
    if (MovementController.I == null) return;
    Vector2 pos = obj.transform.position;
    Vector2 to = worldPos - pos;
    if (to.sqrMagnitude <= 0.0001f) return;
    float ang = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
    MovementController.I.FaceAngle(obj, ang);
  }

  void TryDealDamage(InGameObject attacker, InGameObject target){
    if (attacker == null || target == null) return;
    _fireTimer += Time.deltaTime;
    if (_fireTimer < Mathf.Max(0.1f, fireInterval)) return;
    _fireTimer = 0f;

    int dmg = damageOverride > 0 ? damageOverride : (attacker.attack > 0 ? attacker.attack : 1);
    if (CharacterController.I != null){
      CharacterController.I.DealDamage(target, attacker, dmg);
    }
  }

  InGameObject FindNearestEnemy(InGameObject obj, float maxDist){
    if (obj == null || obj.gameObject == null) return null;
    if (CharacterController.I == null || CharacterController.I.characters == null) return null;

    float maxSqr = (maxDist <= 0f ? float.PositiveInfinity : maxDist * maxDist);
    InGameObject best = null;
    float bestSqr = maxSqr;
    Vector2 p = obj.transform.position;

    var chars = CharacterController.I.characters;
    for (int i = 0; i < chars.Count; i++){
      var ch = chars[i];
      if (ch == null || ch.gameObject == null) continue;
      if (ch.owner == obj.owner) continue;
      Vector2 d = (Vector2)ch.transform.position - p;
      float sqr = d.sqrMagnitude;
      if (sqr > bestSqr) continue;
      bestSqr = sqr;
      best = ch;
    }
    return best;
  }
}

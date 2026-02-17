using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MovementController : MonoBehaviour {
  public static MovementController I;
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

  #region -- Linear Movement --
  public void MoveUpdate(InGameObject _obj){
    if (_obj == null || _obj.gameObject == null) return;
    UpdateLinearMove(_obj);
  }

  public void SetLinearMove(InGameObject _obj, float _speed, float _angle){
    if (_obj == null || _obj.gameObject == null) return;
    _obj.movement.isLinearMoving = true;
    _obj.movement.linearSpeed = _speed;
    _obj.movement.linearAngle = _angle;
    UpdateFacingByAngle(_obj, _angle);
  }

  public void StopLinearMove(InGameObject _obj){
    if (_obj == null || _obj.gameObject == null) return;
    _obj.movement.isLinearMoving = false;
    _obj.movement.currentSpeed = 0f;
    _obj.movement.linearSpeed = 0f;
  }

  public void UpdateLinearMove(InGameObject _obj){
    if(_obj == null || _obj.gameObject == null) return;
    if (!_obj.movement.isLinearMoving || _obj.movement.linearSpeed == 0f) return;

    float rad = _obj.movement.linearAngle * Mathf.Deg2Rad;
    Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    float yPos = _obj.transform.position.y + (dir.y * _obj.movement.linearSpeed * Time.deltaTime);
    _obj.transform.position = new Vector3(
      _obj.transform.position.x + (dir.x * _obj.movement.linearSpeed * Time.deltaTime),
      yPos,
      -19.5f
    );
  }
  #endregion

  #region -- Helper Functions --
  // Helper: simple rotation to angle with offsets
  void UpdateFacingByAngle(InGameObject _obj, float _angle){
    if (_obj == null || _obj.gameObject == null) return;
    float z = _angle + _obj.movement.rotationOffset + _obj.movement.angleOffset;
    _obj.transform.rotation = Quaternion.Euler(0f, 0f, z);
  }
  #endregion
}

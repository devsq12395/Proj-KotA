using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class Character {
  public string name, nameUI, desc;
  public Sprite portrait;
}

[Serializable]
public class ReferencePoint {
  public string name;
  public Vector2 position;
}
[System.Serializable]
public class MovementData {
  // Runtime linear movement state
  public bool isLinearMoving;
  public float linearAngle;   // degrees
  public float linearSpeed;   // units/sec
  public float currentSpeed;  // optional runtime speed (used by StopLinearMove)

  // Visual/facing offsets
  public float rotationOffset; // degrees to align sprites
  public float angleOffset;    // extra rotation offset per object
}

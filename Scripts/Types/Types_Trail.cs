using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TrailEffectConfig {
  public string effectName;
  public float spawnEffectPerSec = 0.05f; // seconds between spawns
  public string sourceRefPointName; // optional reference point name on the object
  public bool inheritAngleFromMovement = true;
  public float extraAngleOffset = 0f;
}

[Serializable]
public class TrailEffectState {
  public TrailEffectConfig config;
  [NonSerialized] public float timer; // runtime only
}

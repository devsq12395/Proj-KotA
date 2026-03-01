using System.Collections.Generic;
using UnityEngine;

public class CommandModeController : MonoBehaviour {
  public static CommandModeController I;

  [Header("Mode")] public KeyCode toggleKey = KeyCode.Tab;
  [SerializeField] bool isCommandMode;

  [Header("Selection")] public float dragSelectThresholdPixels = 12f;
  public int ownedPlayerId = 1; // units with owner == this are selectable

  readonly List<InGameObject> selectedUnits = new List<InGameObject>();
  InGameObject selected;

  bool isPointerDown;
  bool isDraggingSelect;
  Vector2 dragStartScreen;
  Vector2 dragCurrentScreen;

  // Control groups (0-9)
  readonly Dictionary<int, List<int>> controlGroupIds = new Dictionary<int, List<int>>();
  readonly float doublePressWindow = 0.35f;
  readonly float[] _lastPressTimes = new float[10];

  public bool IsCommandMode => isCommandMode;
  public List<InGameObject> SelectedUnits => selectedUnits;

  void Awake(){
    if (gameObject.scene != null && gameObject.scene.IsValid()){
      if (gameObject.scene.name != "Game"){ Destroy(gameObject); return; }
    }
    if (I == null) I = this; else { Destroy(gameObject); return; }
  }

  void Update(){
    if (Input.GetKeyDown(toggleKey)) Toggle();
    if (!isCommandMode) return;

    HandleSelectionInput();
    HandleOrderInput();
    HandleControlGroupInput();
  }

  // --- Toggle ---
  public void Toggle(){
    if (!isCommandMode) Enter(); else Exit();
  }
  public void Enter(){
    isCommandMode = true;
    ClearSelection();
  }
  public void Exit(){
    isCommandMode = false;
    ClearSelection();
  }

  // --- Selection ---
  void HandleSelectionInput(){
    if (Input.GetMouseButtonDown(0)){
      isPointerDown = true; isDraggingSelect = false;
      dragStartScreen = Input.mousePosition; dragCurrentScreen = dragStartScreen;
    }
    if (isPointerDown && Input.GetMouseButton(0)){
      dragCurrentScreen = Input.mousePosition;
      if (!isDraggingSelect){
        float threshold = Mathf.Max(0f, dragSelectThresholdPixels);
        if ((dragCurrentScreen - dragStartScreen).sqrMagnitude >= threshold * threshold){
          isDraggingSelect = true;
        }
      }
    }
    if (isPointerDown && Input.GetMouseButtonUp(0)){
      isPointerDown = false;
      if (isDraggingSelect){
        isDraggingSelect = false;
        DragSelect(dragStartScreen, dragCurrentScreen);
      } else {
        ClickSelect(Input.mousePosition);
      }
    }
  }

  void ClickSelect(Vector2 screenPos){
    InGameObject obj = ResolveOwnedObjectAtScreen(screenPos);
    if (obj == null){
      ClearSelection();
      return;
    }
    SetSelectionSingle(obj);
  }

  void DragSelect(Vector2 startScreen, Vector2 endScreen){
    if (!TryGetWorldRectFromScreenRect(startScreen, endScreen, out var worldRect)){
      ClearSelection(); return;
    }
    selectedUnits.Clear();
    if (CharacterController.I != null && CharacterController.I.characters != null){
      var list = CharacterController.I.characters;
      for (int i = 0; i < list.Count; i++){
        var c = list[i]; if (c == null || c.gameObject == null) continue; if (c.owner != ownedPlayerId) continue;
        if (!TryGetSelectionBounds(c, out var b)) continue;
        Rect br = BoundsToRect2D(b);
        if (!worldRect.Overlaps(br, true)) continue;
        selectedUnits.Add(c);
      }
    }
    selected = selectedUnits.Count > 0 ? selectedUnits[0] : null;
  }

  InGameObject ResolveOwnedObjectAtScreen(Vector2 screenPos){
    if (!TryScreenToWorld2D(screenPos, out var wp)) return null;
    Collider2D c = Physics2D.OverlapPoint(wp);
    if (c == null) return null; var obj = c.GetComponentInParent<InGameObject>();
    if (obj == null || obj.gameObject == null) return null; if (obj.owner != ownedPlayerId) return null; return obj;
  }

  void SetSelectionSingle(InGameObject obj){
    selectedUnits.Clear(); if (obj != null) selectedUnits.Add(obj); selected = obj;
  }

  void ClearSelection(){
    selectedUnits.Clear(); selected = null;
  }

  // --- Orders ---
  void HandleOrderInput(){
    if (selectedUnits.Count == 0) return;
    if (Input.GetMouseButtonDown(1)){
      if (!TryScreenToWorld2D(Input.mousePosition, out var wp)) return;
      bool attack = Input.GetKey(KeyCode.A);
      IssueOrderToSelection(attack ? OrderType.AttackMove : OrderType.Move, wp);
    }
  }

  public void IssueOrderToSelection(OrderType type, Vector2 targetPos){
    if (selectedUnits == null || selectedUnits.Count == 0) return;
    // centroid for formation
    Vector2 centroid = Vector2.zero; int count = 0;
    if (selectedUnits.Count > 1){
      for (int i = 0; i < selectedUnits.Count; i++){
        var u = selectedUnits[i]; if (u == null || u.gameObject == null) continue;
        var p = u.transform.position; centroid += new Vector2(p.x, p.y); count++;
      }
      if (count > 0) centroid /= count;
    }
    for (int i = 0; i < selectedUnits.Count; i++){
      var u = selectedUnits[i]; if (u == null || u.gameObject == null) continue;
      Vector2 unitPos = u.transform.position; Vector2 offset = (selectedUnits.Count > 1 && count > 0) ? (unitPos - centroid) : Vector2.zero;
      Vector2 finalTarget = targetPos + offset;
      if (u.currentOrder == null) u.currentOrder = new Order();
      u.currentOrder.type = type; u.currentOrder.targetPos = finalTarget; u.currentOrder.stopDistance = 0.25f; u.currentOrder.issuedTime = Time.time;
    }
  }

  // --- Control Groups ---
  void HandleControlGroupInput(){
    for (int digit = 0; digit <= 9; digit++){
      KeyCode kc = KeyCode.Alpha0 + digit;
      if (Input.GetKeyDown(kc)){
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl){ SaveControlGroup(digit); }
        else {
          float t = Time.time;
          if (t - _lastPressTimes[digit] <= doublePressWindow){ RecallControlGroup(digit, focusCamera: true); _lastPressTimes[digit] = 0f; }
          else { RecallControlGroup(digit, focusCamera: false); _lastPressTimes[digit] = t; }
        }
      }
    }
  }

  void SaveControlGroup(int group){
    if (selectedUnits == null || selectedUnits.Count == 0){ if (controlGroupIds.ContainsKey(group)) controlGroupIds.Remove(group); return; }
    List<int> ids = new List<int>(selectedUnits.Count);
    for (int i = 0; i < selectedUnits.Count; i++){
      var u = selectedUnits[i]; if (u == null || u.gameObject == null) continue; if (u.id == 0) u.SetID(); if (u.id == 0) continue; if (!ids.Contains(u.id)) ids.Add(u.id);
    }
    if (ids.Count == 0){ if (controlGroupIds.ContainsKey(group)) controlGroupIds.Remove(group); return; }
    controlGroupIds[group] = ids;
  }

  void RecallControlGroup(int group, bool focusCamera){
    if (!controlGroupIds.TryGetValue(group, out var ids) || ids == null || ids.Count == 0){ ClearSelection(); return; }
    selectedUnits.Clear();
    if (CharacterController.I != null && CharacterController.I.characters != null){
      var chars = CharacterController.I.characters;
      for (int i = 0; i < chars.Count; i++){
        var c = chars[i]; if (c == null || c.gameObject == null) continue; if (c.owner != ownedPlayerId) continue; if (c.id == 0) c.SetID(); if (c.id == 0) continue; if (!ids.Contains(c.id)) continue; selectedUnits.Add(c);
      }
    }
    selected = selectedUnits.Count > 0 ? selectedUnits[0] : null;
    if (focusCamera) FocusCameraToSelectionCentroid();
  }

  void FocusCameraToSelectionCentroid(){
    if (selectedUnits == null || selectedUnits.Count == 0) return;
    Vector2 centroid = Vector2.zero; int count = 0;
    for (int i = 0; i < selectedUnits.Count; i++){
      var u = selectedUnits[i]; if (u == null || u.gameObject == null) continue; var p = u.transform.position; centroid += new Vector2(p.x, p.y); count++;
    }
    if (count == 0) return; centroid /= count;
    var cam = Camera.main; if (cam == null) return;
    var pos = cam.transform.position; cam.transform.position = new Vector3(centroid.x, centroid.y, pos.z);
  }

  // --- Utils ---
  bool TryScreenToWorld2D(Vector2 screen, out Vector2 world){
    world = Vector2.zero; var cam = Camera.main; if (cam == null) return false; var wp = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z)); world = new Vector2(wp.x, wp.y); return true;
  }

  bool TryGetWorldRectFromScreenRect(Vector2 s0, Vector2 s1, out Rect rect){
    rect = new Rect(); if (!TryScreenToWorld2D(s0, out var w0)) return false; if (!TryScreenToWorld2D(s1, out var w1)) return false;
    Vector2 min = Vector2.Min(w0, w1); Vector2 max = Vector2.Max(w0, w1); rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y); return true;
  }

  Rect BoundsToRect2D(Bounds b){
    return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
  }

  bool TryGetSelectionBounds(InGameObject unit, out Bounds b){
    b = new Bounds(); if (unit == null || unit.gameObject == null) return false;
    var col = unit.GetComponentInChildren<Collider2D>(); if (col != null){ b = col.bounds; return true; }
    var srs = unit.GetComponentsInChildren<SpriteRenderer>(true); if (srs != null && srs.Length > 0){
      bool init = false; for (int i = 0; i < srs.Length; i++){ var sr = srs[i]; if (sr == null) continue; if (!init){ b = sr.bounds; init = true; } else { b.Encapsulate(sr.bounds); } }
      if (init) return true;
    }
    return false;
  }
}

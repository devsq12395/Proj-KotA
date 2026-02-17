using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Save system using PlayerPrefs with dot-accessible JSON values.
/// Save schema version: 1.2
///
/// Example: SaveController.I.GetValue("player.stats.hp")
/// </summary>
public class SaveController : MonoBehaviour {
  #region Singleton & Events
  public static SaveController I;
  public event Action<int> OnMoneyChanged;
  public event Action<string> OnCombatSuitChanged;
  #endregion

  #region UI Flags
  bool _forceLobbyPilotTabOnce;
  #endregion

  #region Default Save JSON
  // Default JSON (your base save)
  public string gameName = "Set this like this";
  private const string PREF_KEY = "SET_THIS_LIKE_THIS";
  [TextArea(5, 20)]
  public string defaultJson = @"{
    ""schema-version"":""1.0"",

    ""money"":""0"",
    ""firstRun"":""1"",
    ""settings"": {
      ""musicVolume"": 0.2,
      ""sfxVolume"": 0.4
    }
  }";
  #endregion

  #region Unity Lifecycle
  private Dictionary<string, object> data;
  private bool FORCE_REPLACE = false;
  private const string LogPrefix = "[SaveController]";

  private string GetPrefsKey()
  {
    string safeGameName = string.IsNullOrEmpty(gameName) ? "default" : gameName.Trim();
    return $"{PREF_KEY}::{safeGameName}";
  }

  void Awake()
  {
    if (I != null && I != this)
    {
      Destroy(gameObject);
      return;
    }
    I = this;
    
    FORCE_REPLACE = false;
    DontDestroyOnLoad(gameObject);
    LoadData();
  }

  #endregion

  #region Save and Load Logic

  // Load JSON from PlayerPrefs or defaults
  public void LoadData()
  {
    string prefsKey = GetPrefsKey();
    string json = defaultJson;

    if (FORCE_REPLACE)
    {
      Debug.LogWarning($"{LogPrefix} FORCE_REPLACE active – writing defaults to PlayerPrefs once");
      try
      {
        PlayerPrefs.SetString(prefsKey, defaultJson);
        PlayerPrefs.Save();
        json = defaultJson;
      }
      catch (Exception exception)
      {
        Debug.LogError($"{LogPrefix} FORCE_REPLACE write failed. Exception: {exception}");
        json = defaultJson; // keep in-memory default
      }
      finally
      {
        FORCE_REPLACE = false;
      }
    }

    // Always attempt to read from PlayerPrefs; on failure, fall back to defaults in memory
    try
    {
      if (!PlayerPrefs.HasKey(prefsKey))
      {
        if (PlayerPrefs.HasKey(PREF_KEY))
        {
          string legacyJson = PlayerPrefs.GetString(PREF_KEY, defaultJson);
          PlayerPrefs.SetString(prefsKey, legacyJson);
          PlayerPrefs.Save();
          json = legacyJson;
        }
        else
        {
          PlayerPrefs.SetString(prefsKey, defaultJson);
          PlayerPrefs.Save();
          json = defaultJson;
        }
      }
      else
      {
        json = PlayerPrefs.GetString(prefsKey, defaultJson);
      }
    }
    catch (Exception exception)
    {
      Debug.LogError($"{LogPrefix} PlayerPrefs read failed. Exception: {exception}");
      json = defaultJson;
    }

    try
    {
      data = MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
    }
    catch (Exception exception)
    {
      Debug.LogError($"{LogPrefix} JSON parse failed, using defaults. Exception: {exception}");
      data = null;
    }

    if (data == null)
    {
      data = MiniJSON.Json.Deserialize(defaultJson) as Dictionary<string, object>;
      if (data == null) data = new Dictionary<string, object>();
      SaveData();
    }

    // Ensure critical keys exist for older saves
    if (data != null)
    {
      if (!data.ContainsKey("money") || data["money"] == null)
      {
        data["money"] = 200; // default starting money
        SaveData();
        // notify listeners
        OnMoneyChanged?.Invoke(200);

      }

      SaveData();
    }
  }

  // Save JSON to PlayerPrefs
  public void SaveData()
  {
    try
    {
      string json = MiniJSON.Json.Serialize(data);
      PlayerPrefs.SetString(GetPrefsKey(), json);
      PlayerPrefs.Save();
    }
    catch (Exception exception)
    {
      Debug.LogError($"{LogPrefix} SaveData failed. Exception: {exception}");
    }
  }
  
  // Dot-path getter: e.g., "settings.musicVolume" returns object or fallback if missing
  public object GetValue(string path, object fallback = null)
  {
    try
    {
      if (string.IsNullOrEmpty(path) || data == null) return fallback;
      object cur = data;
      var parts = path.Split('.');
      for (int i = 0; i < parts.Length; i++)
      {
        string key = parts[i];
        if (cur is Dictionary<string, object> dict)
        {
          if (!dict.TryGetValue(key, out cur) || cur == null)
          {
            return fallback;
          }
          continue;
        }
        else if (cur is List<object> list)
        {
          // Support numeric index for arrays if a segment is an integer
          if (!int.TryParse(key, out int idx) || idx < 0 || idx >= list.Count)
          {
            return fallback;
          }
          cur = list[idx];
          continue;
        }
        // Unexpected type in the path traversal
        return fallback;
      }
      return cur ?? fallback;
    }
    catch
    {
      return fallback;
    }
  }

  #endregion
}

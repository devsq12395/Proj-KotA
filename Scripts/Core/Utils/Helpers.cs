using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

public static class Helpers {
  public static int GenerateRandomIntId(int digits) {
    int min = (int)Mathf.Pow(10, digits - 1);
    int max = (int)Mathf.Pow(10, digits) - 1;
    return Random.Range(min, max);
  }

  public static string GenerateRandomStringId(int length) {
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    StringBuilder builder = new StringBuilder();
    for (int i = 0; i < length; i++) {
      int index = Random.Range(0, chars.Length);
      builder.Append(chars[index]);
    }
    return builder.ToString();
  }

  public static List<string> StringToListString(string origString){
    return origString.Split(';').ToList(); 
  }

#if UNITY_EDITOR 
  public static bool TEMP_IS_MOBILE = false;
#endif

  public static bool IsMobile(){
#if UNITY_EDITOR
    if (TEMP_IS_MOBILE) {
      Debug.LogWarning("WARNING: Forced mobile mode is on in Helpers.cs");
      return true;
    }
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
    return Application.isMobilePlatform;
#else
    return Application.isMobilePlatform || Input.touchSupported;
#endif
  }

}

using UnityEngine;

public class DB_Sounds : MonoBehaviour {
  public static DB_Sounds I;

  void Awake() {
    if (I == null) {
      I = this;
      DontDestroyOnLoad(gameObject);
    }
    else if (I != this) {
      Destroy(gameObject);
    }
  }

  [Header("Music Clips (configure in Inspector)")]
  public AudioClip[] bgmGame;

  [Header("SFX Clips (configure in Inspector)")]
  public AudioClip dash, explodeDestroy, explodeMiniMissile, explodeOnHit, explodeGrenade;
  public AudioClip getPowerup, noBullet, onHit, onKill;
  public AudioClip shootMissile1, shootPlasma1, shootPlasma2, shootPlasma3, shootMissilePack, shootGrenadeLauncher, shootMeleeAttack, shootLaserLoop;
  public AudioClip warning, click, defeat, getCash, getKillingSpree, getStar, onBuy, reload, win, spacerift, chargeShort, chargeLong;

  [Header("Voice Clips")]
  public AudioClip voiceDangerEjecting;
  public AudioClip voiceDoubleKill, voiceIncomingTransmission, voiceKillingSpree;
  public AudioClip voiceMissionComplete, voiceMissionStart, voiceOutOfAmmo, voiceQuadraKill;
  public AudioClip voiceRampage, voiceReloading, voiceSelectABonus, voiceTargetDestroyed;
  public AudioClip voiceTeleportingToMission, voiceTripleKill;
  public AudioClip voiceTakingDamage, voiceWelcomeAcePilot, voiceSelectAMission;

  public AudioClip GetClip(string soundId) {
    switch (soundId) {
      case "dash": return dash;

      case "explode-destroy": return explodeDestroy;
      case "explode-mini-missile": return explodeMiniMissile;
      case "explode-on-hit": return explodeOnHit;
      case "explode-grenade": return explodeGrenade;

      case "shoot-missile-1": return shootMissile1;
      case "shoot-plasma-1": return shootPlasma1;
      case "shoot-plasma-2": return shootPlasma2;
      case "shoot-plasma-3": return shootPlasma3;
      case "shoot-missile-pack": return shootMissilePack;
      case "shoot-grenade-launcher": return shootGrenadeLauncher;
      case "shoot-melee-attack": return shootMeleeAttack;
      case "shoot-laser-loop": return shootLaserLoop;

      case "get-powerup": return getPowerup;
      case "no-bullet": return noBullet;
      case "on-hit": return onHit;
      case "on-kill": return onKill;
      case "warning": return warning;
      case "click": return click;
      case "defeat": return defeat;
      case "get-cash": return getCash;
      case "get-killing-spree": return getKillingSpree;
      case "get-star": return getStar;
      case "on-buy": return onBuy;
      case "reload": return reload;
      case "win": return win;
      case "spacerift": return spacerift;
      case "charge-short": return chargeShort;
      case "charge-long": return chargeLong;

      case "voice-danger-ejecting": return voiceDangerEjecting;
      case "voice-double-kill": return voiceDoubleKill;
      case "voice-incoming-transmission": return voiceIncomingTransmission;
      case "voice-killing-spree": return voiceKillingSpree;
      case "voice-mission-complete": return voiceMissionComplete;
      case "voice-mission-start": return voiceMissionStart;
      case "voice-out-of-ammo": return voiceOutOfAmmo;
      case "voice-quadra-kill": return voiceQuadraKill;
      case "voice-rampage": return voiceRampage;
      case "voice-reloading": return voiceReloading;
      case "voice-select-a-bonus": return voiceSelectABonus;
      case "voice-target-destroyed": return voiceTargetDestroyed;
      case "voice-teleporting-to-mission": return voiceTeleportingToMission;
      case "voice-triple-kill": return voiceTripleKill;
      case "voice-taking-damage": return voiceTakingDamage;
      case "voice-welcome-ace-pilot": return voiceWelcomeAcePilot;
      case "voice-select-a-mission": return voiceSelectAMission;
    }
    return null;
  }

  public AudioClip FindMusicClipByName(string clipName) {
    if (string.IsNullOrEmpty(clipName) || bgmGame == null) return null;

    for (int i = 0; i < bgmGame.Length; i++) {
      AudioClip c = bgmGame[i];
      if (c != null && c.name == clipName) {
        return c;
      }
    }

    return null;
  }
}

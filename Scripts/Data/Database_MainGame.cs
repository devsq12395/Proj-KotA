using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Database_MainGame : MonoBehaviour
{
    public static Database_MainGame I;

    void Awake() {
        if (I == null) { I = this; } 
        else { Destroy(gameObject); }
    }

    public GameObject emptyPrefab;

    [Header("Define Characters Here")]
    public List<GameObject> characters = new List<GameObject>();
    
    [Header("Define Effects Here")]
    public List<GameObject> effects = new List<GameObject>();

    [Header("Define Missiles Here")]
    public List<GameObject> missiles = new List<GameObject>();
}
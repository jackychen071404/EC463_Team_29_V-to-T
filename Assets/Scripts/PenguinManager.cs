using System.Collections.Generic;
using UnityEngine;

public class PenguinManager : MonoBehaviour
{
    public static PenguinManager I;

    [Header("Word / Progress")]
    public List<string> words = new List<string> { "hat", "shirt", "pants", "shoes" };
    public int wordIndex = 0;
    public string CurrentWord => (wordIndex >= 0 && wordIndex < words.Count) ? words[wordIndex] : "";

    [Header("Unlocked Items")]
    public HashSet<string> unlockedItems = new HashSet<string>();

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetProgress()
    {
        wordIndex = 0;
        unlockedItems.Clear();
    }

    public bool IsUnlocked(string item) => unlockedItems.Contains(item);

    public void Unlock(string item) => unlockedItems.Add(item);

    public bool AdvanceWord()
    {
        if (wordIndex >= words.Count - 1) return false;
        wordIndex++;
        return true;
    }
}
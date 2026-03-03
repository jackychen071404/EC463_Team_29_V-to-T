using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PhonemeConverter
{
    private const string LogSource = "PhonemeConverter.cs";
    private static Dictionary<string, List<string>> cmuDict;

    private static readonly Dictionary<string, string> ArpaToVocab = new()
    {
        {"B","b"},{"CH","ch"},{"D","d"},{"DH","th"},{"F","f"},{"G","g"},{"HH","h"},
        {"JH","j"},{"K","k"},{"L","l"},{"M","m"},{"N","n"},{"NG","ng"},{"P","p"},
        {"R","r"},{"S","s"},{"SH","sh"},{"T","t"},{"TH","th"},{"V","v"},{"W","w"},
        {"Y","y"},{"Z","z"},

        {"AA","a"},{"AE","e"},{"AH","u"},{"AO","aw"},{"AW","oau"},{"AY","ay"},
        {"EH","e"},{"ER","or"},{"EY","ay"},{"IH","i"},{"IY","ee"},{"OW","oh"},
        {"OY","oi"},{"UH","uoh"},{"UW","oo"}
    };

    public static void LoadCMUDict(TextAsset cmuFile)
    {
        if (cmuFile == null)
        {
            cmuDict = new Dictionary<string, List<string>>();
            BackendLogger.Error(LogSource, "CMUDictLoadFailed", "reason=null_text_asset");
            return;
        }

        cmuDict = new Dictionary<string, List<string>>();

        foreach (string line in cmuFile.text.Split(BackendConfig.Cmu.LineSeparator))
        {
            if (line.StartsWith(BackendConfig.Cmu.CommentPrefix) || string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split(BackendConfig.Cmu.WordPhonemeSeparator);
            if (parts.Length < 2)
                continue;

            string word = parts[0].Trim().ToLower();
            string phonemes = parts[1].Trim();

            var clean = phonemes
                .Split(BackendConfig.Cmu.PhonemeSeparator)
                .Select(p => new string(p.Where(char.IsLetter).ToArray()))
                .ToList();

            cmuDict[word] = clean;
        }

        BackendLogger.Info(LogSource, "CMUDictLoaded", $"entries={cmuDict.Count}, source={cmuFile.name}");
    }

    public static List<string> ConvertWord(string word)
    {
        if (cmuDict == null)
        {
            BackendLogger.Warn(LogSource, "ConvertWordBeforeLoad", $"inputWord={word}");
            return new List<string>();
        }

        word = word.ToLower().Trim();
        if (!cmuDict.TryGetValue(word, out var arpaList))
            return new List<string>();

        return arpaList
            .Where(p => ArpaToVocab.ContainsKey(p))
            .Select(p => ArpaToVocab[p])
            .ToList();
    }

    public static string ConvertWordAsString(string word)
    {
        return string.Join(" ", ConvertWord(word));
    }
}

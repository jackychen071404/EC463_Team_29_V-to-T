using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PhonemeConverter
{
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
        cmuDict = new Dictionary<string, List<string>>();

        foreach (string line in cmuFile.text.Split('\n'))
        {
            if (line.StartsWith(";;;") || string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split("  ");
            if (parts.Length < 2)
                continue;

            string word = parts[0].Trim().ToLower();
            string phonemes = parts[1].Trim();

            var clean = phonemes
                .Split(' ')
                .Select(p => new string(p.Where(char.IsLetter).ToArray()))
                .ToList();

            cmuDict[word] = clean;
        }
    }

    public static List<string> ConvertWord(string word)
    {
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

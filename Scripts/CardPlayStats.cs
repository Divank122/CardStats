using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CardStats.Scripts;

public static class CardPlayStats
{
    private static Dictionary<string, int> _playCounts = new();
    private static Dictionary<string, int>? _combatBackup = null;
    private static Dictionary<CardModel, (string key, int count)> _pendingUpgrades = new();
    private static Dictionary<CardModel, int> _pendingRemoval = new();
    
    public static bool ShowPlayCount { get; set; } = true;
    public static bool ShowPilePlayCount { get; set; } = true;
    public static bool ShowHistoryPlayCount { get; set; } = true;
    
    private static long _currentRunStartTime = 0;

    private static string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "CardStats",
        "playstats.json"
    );

    private static string HistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "CardStats",
        "history.json"
    );

    private const string Separator = "|";

    private static CardModel? GetDeckCard(CardModel card)
    {
        return card.DeckVersion ?? card;
    }

    private static IReadOnlyList<CardModel>? GetDeck()
    {
        var state = RunManager.Instance?.DebugOnlyGetState();
        return state?.Players?.FirstOrDefault()?.Deck?.Cards;
    }

    public static string GetCardKey(CardModel? deckCard)
    {
        if (deckCard == null)
        {
            return $"unknown{Separator}0";
        }
        
        var actualCard = deckCard.DeckVersion ?? deckCard;
        var deck = GetDeck();
        if (deck == null)
        {
            return $"{actualCard.Id}{Separator}{actualCard.IsUpgraded}{Separator}{actualCard.FloorAddedToDeck ?? 0}{Separator}0";
        }
        
        var id = actualCard.Id.ToString();
        var isUpgraded = actualCard.IsUpgraded;
        var floor = actualCard.FloorAddedToDeck ?? 0;
        
        int index = 0;
        for (int i = 0; i < deck.Count; i++)
        {
            var c = deck[i];
            if (ReferenceEquals(c, actualCard))
            {
                index = CountSameCardsBefore(deck, id, isUpgraded, floor, i);
                break;
            }
        }
        
        return $"{id}{Separator}{isUpgraded}{Separator}{floor}{Separator}{index}";
    }

    private static int CountSameCardsBefore(IReadOnlyList<CardModel> deck, string id, bool isUpgraded, int floor, int targetIndex)
    {
        int count = 0;
        for (int i = 0; i <= targetIndex; i++)
        {
            var c = deck[i];
            if (c.Id.ToString() == id && c.IsUpgraded == isUpgraded && (c.FloorAddedToDeck ?? 0) == floor)
            {
                count++;
            }
        }
        return count - 1;
    }

    public static void Initialize()
    {
        _playCounts = new Dictionary<string, int>();
        _combatBackup = null;
        _currentRunStartTime = 0;
        ShowPlayCount = true;
        ShowHistoryPlayCount = true;
        LoadFromFile();
    }

    public static void Reset()
    {
        _playCounts.Clear();
        _combatBackup = null;
        _currentRunStartTime = 0;
        DeleteSaveFile();
    }

    public static void SetRunStartTime(long startTime)
    {
        _currentRunStartTime = startTime;
    }

    public static void OnCombatSetUp()
    {
        _combatBackup = new Dictionary<string, int>(_playCounts);
    }

    public static void OnCombatEnded()
    {
        _combatBackup = null;
        SaveToFile();
    }

    public static void RestoreFromBackup()
    {
        if (_combatBackup != null)
        {
            _playCounts = new Dictionary<string, int>(_combatBackup);
            _combatBackup = null;
            SaveToFile();
        }
    }

    public static void SaveToHistory()
    {
        if (_currentRunStartTime == 0) return;
        
        try
        {
            var history = LoadHistoryFile();
            var cardStats = new Dictionary<string, int>();
            
            foreach (var kvp in _playCounts)
            {
                var key = kvp.Key;
                var parts = key.Split(Separator);
                if (parts.Length >= 2)
                {
                    var historyKey = $"{parts[0]}{Separator}{parts[1]}";
                    if (cardStats.ContainsKey(historyKey))
                    {
                        cardStats[historyKey] += kvp.Value;
                    }
                    else
                    {
                        cardStats[historyKey] = kvp.Value;
                    }
                }
            }
            
            history[_currentRunStartTime.ToString()] = cardStats;
            
            var directory = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(history);
            File.WriteAllText(HistoryPath, json);
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to save history: {ex.Message}");
        }
    }

    public static Dictionary<string, int> GetHistoryStats(long startTime)
    {
        try
        {
            var history = LoadHistoryFile();
            if (history.TryGetValue(startTime.ToString(), out var stats))
            {
                return stats;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to load history: {ex.Message}");
        }
        return new Dictionary<string, int>();
    }

    public static int GetHistoryPlayCount(CardModel card, Dictionary<string, int> stats)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return 0;
        
        var key = $"{deckCard.Id.ToString()}{Separator}{deckCard.IsUpgraded}";
        return stats.TryGetValue(key, out var count) ? count : 0;
    }

    public static void RecordPlay(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return;
        
        if (!IsCardInDeck(deckCard)) return;
        
        var key = GetCardKey(deckCard);
        
        if (_playCounts.ContainsKey(key))
        {
            _playCounts[key]++;
        }
        else
        {
            _playCounts[key] = 1;
        }
    }

    private static bool IsCardInDeck(CardModel card)
    {
        var deck = GetDeck();
        if (deck == null) return false;
        
        foreach (var deckCard in deck)
        {
            if (ReferenceEquals(deckCard, card))
            {
                return true;
            }
        }
        return false;
    }

    public static int GetPlayCount(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _playCounts.TryGetValue(key, out var c) ? c : 0;
    }

    private static void SaveToFile()
    {
        try
        {
            var directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(_playCounts);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to save: {ex.Message}");
        }
    }

    private static void LoadFromFile()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                _playCounts = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to load: {ex.Message}");
            _playCounts = new Dictionary<string, int>();
        }
    }

    private static void DeleteSaveFile()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to delete save: {ex.Message}");
        }
    }

    private static Dictionary<string, Dictionary<string, int>> LoadHistoryFile()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var json = File.ReadAllText(HistoryPath);
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json) ?? new Dictionary<string, Dictionary<string, int>>();
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to load history file: {ex.Message}");
        }
        return new Dictionary<string, Dictionary<string, int>>();
    }

    public static bool HasPlayCount(string key)
    {
        return _playCounts.ContainsKey(key);
    }

    public static int GetPlayCountByKey(string key)
    {
        return _playCounts.TryGetValue(key, out var count) ? count : 0;
    }

    public static void SetPlayCountByKey(string key, int count)
    {
        _playCounts[key] = count;
    }

    public static void RemovePlayCountByKey(string key)
    {
        _playCounts.Remove(key);
    }

    public static void StorePendingUpgrade(CardModel card, string key, int count)
    {
        _pendingUpgrades[card] = (key, count);
    }

    public static (string key, int count) GetAndClearPendingUpgradeForCard(CardModel card)
    {
        if (_pendingUpgrades.TryGetValue(card, out var result))
        {
            _pendingUpgrades.Remove(card);
            return result;
        }
        
        return ("", 0);
    }

    public static void RebuildKeysAfterRemoval()
    {
        var deck = GetDeck();
        if (deck == null) return;
        
        var oldCounts = new Dictionary<string, int>(_playCounts);
        _playCounts.Clear();
        
        var cardGroups = new Dictionary<string, List<int>>();
        
        foreach (var kvp in oldCounts)
        {
            var parts = kvp.Key.Split(Separator);
            if (parts.Length >= 4)
            {
                var baseKey = $"{parts[0]}{Separator}{parts[1]}{Separator}{parts[2]}";
                var index = int.Parse(parts[3]);
                
                if (!cardGroups.ContainsKey(baseKey))
                {
                    cardGroups[baseKey] = new List<int>();
                }
                while (cardGroups[baseKey].Count <= index)
                {
                    cardGroups[baseKey].Add(0);
                }
                cardGroups[baseKey][index] = kvp.Value;
            }
        }
        
        var newCardGroups = new Dictionary<string, int>();
        
        for (int i = 0; i < deck.Count; i++)
        {
            var card = deck[i];
            var baseKey = $"{card.Id}{Separator}{card.IsUpgraded}{Separator}{card.FloorAddedToDeck ?? 0}";
            
            if (!newCardGroups.ContainsKey(baseKey))
            {
                newCardGroups[baseKey] = 0;
            }
            
            var newIndex = newCardGroups[baseKey];
            newCardGroups[baseKey]++;
            
            if (cardGroups.TryGetValue(baseKey, out var counts) && newIndex < counts.Count)
            {
                var newKey = $"{baseKey}{Separator}{newIndex}";
                _playCounts[newKey] = counts[newIndex];
            }
        }
    }

    public static void PrepareForRemoval(IReadOnlyList<CardModel> cardsToRemove)
    {
        _pendingRemoval.Clear();
        
        var deck = GetDeck();
        if (deck == null) return;
        
        var toRemoveSet = new HashSet<CardModel>();
        foreach (var card in cardsToRemove)
        {
            if (card == null) continue;
            var actualCard = card.DeckVersion ?? card;
            toRemoveSet.Add(actualCard);
        }
        
        foreach (var card in deck)
        {
            var key = GetCardKey(card);
            if (_playCounts.TryGetValue(key, out var count))
            {
                _pendingRemoval[card] = count;
            }
        }
        
        foreach (var card in toRemoveSet)
        {
            _pendingRemoval.Remove(card);
        }
        
        _playCounts.Clear();
    }

    public static void FinalizeRemoval()
    {
        var deck = GetDeck();
        if (deck == null)
        {
            _pendingRemoval.Clear();
            return;
        }
        
        _playCounts.Clear();
        
        var cardGroups = new Dictionary<string, int>();
        
        foreach (var card in deck)
        {
            var baseKey = $"{card.Id}{Separator}{card.IsUpgraded}{Separator}{card.FloorAddedToDeck ?? 0}";
            
            if (!cardGroups.ContainsKey(baseKey))
            {
                cardGroups[baseKey] = 0;
            }
            
            var index = cardGroups[baseKey];
            cardGroups[baseKey]++;
            
            if (_pendingRemoval.TryGetValue(card, out var count))
            {
                var key = $"{baseKey}{Separator}{index}";
                _playCounts[key] = count;
            }
        }
        
        _pendingRemoval.Clear();
    }
}

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

    private static string GetCardKey(CardModel? deckCard)
    {
        if (deckCard == null)
        {
            return $"unknown{Separator}0";
        }
        
        var deck = GetDeck();
        if (deck == null)
        {
            return $"{deckCard.Id}{Separator}{deckCard.IsUpgraded}{Separator}{deckCard.FloorAddedToDeck ?? 0}{Separator}0";
        }
        
        var id = deckCard.Id.ToString();
        var isUpgraded = deckCard.IsUpgraded;
        var floor = deckCard.FloorAddedToDeck ?? 0;
        
        int index = 0;
        for (int i = 0; i < deck.Count; i++)
        {
            var c = deck[i];
            if (ReferenceEquals(c, deckCard))
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
        Log.Info("[CardStats] Stats initialized");
    }

    public static void Reset()
    {
        _playCounts.Clear();
        _combatBackup = null;
        _currentRunStartTime = 0;
        DeleteSaveFile();
        Log.Info("[CardStats] Stats reset for new run");
    }

    public static void SetRunStartTime(long startTime)
    {
        _currentRunStartTime = startTime;
        Log.Info($"[CardStats] Set run start time: {startTime}");
    }

    public static void OnCombatSetUp()
    {
        _combatBackup = new Dictionary<string, int>(_playCounts);
        Log.Info($"[CardStats] Combat started, backed up {_combatBackup.Count} entries");
    }

    public static void OnCombatEnded()
    {
        _combatBackup = null;
        SaveToFile();
        Log.Info("[CardStats] Combat ended, cleared backup and saved");
    }

    public static void RestoreFromBackup()
    {
        if (_combatBackup != null)
        {
            _playCounts = new Dictionary<string, int>(_combatBackup);
            _combatBackup = null;
            SaveToFile();
            Log.Info($"[CardStats] Restored {_playCounts.Count} entries from backup and saved");
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
            Log.Info($"[CardStats] Saved history for run {_currentRunStartTime}, {cardStats.Count} card types");
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to save history: {ex.Message}");
        }
    }

    public static Dictionary<string, int> GetHistoryStats(long startTime)
    {
        Log.Info($"[CardStats] GetHistoryStats: startTime={startTime}");
        try
        {
            var history = LoadHistoryFile();
            Log.Info($"[CardStats] GetHistoryStats: history has {history.Count} runs");
            if (history.TryGetValue(startTime.ToString(), out var stats))
            {
                Log.Info($"[CardStats] GetHistoryStats: found stats with {stats.Count} entries");
                return stats;
            }
            Log.Info($"[CardStats] GetHistoryStats: startTime not found in history");
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
        Log.Info($"[CardStats] GetHistoryPlayCount: key={key}, stats count={stats.Count}");
        var result = stats.TryGetValue(key, out var count) ? count : 0;
        Log.Info($"[CardStats] GetHistoryPlayCount: result={result}");
        return result;
    }

    public static void RecordPlay(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null)
        {
            Log.Info($"[CardStats] RecordPlay: deckCard is null for {card?.Id}");
            return;
        }
        
        if (!IsCardInDeck(deckCard))
        {
            Log.Info($"[CardStats] RecordPlay: {deckCard.Id} not in deck");
            return;
        }
        
        var key = GetCardKey(deckCard);
        Log.Info($"[CardStats] RecordPlay: key={key}");
        
        if (_playCounts.ContainsKey(key))
        {
            _playCounts[key]++;
            Log.Info($"[CardStats] RecordPlay: incremented to {_playCounts[key]}");
        }
        else
        {
            _playCounts[key] = 1;
            Log.Info($"[CardStats] RecordPlay: set to 1");
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
        Log.Info($"[CardStats] IsCardInDeck: card {card.Id} not found by ReferenceEquals");
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
            Log.Info($"[CardStats] Saved {_playCounts.Count} entries");
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
                Log.Info($"[CardStats] Loaded {_playCounts.Count} entries");
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
                Log.Info("[CardStats] Deleted save file");
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
}

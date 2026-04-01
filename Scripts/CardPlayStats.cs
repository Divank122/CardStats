using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CardStats.Scripts;

public class CardStatsSaveData
{
    public Dictionary<string, int> PlayCounts { get; set; } = new();
    public Dictionary<string, int> DamageCounts { get; set; } = new();
    public Dictionary<string, int> BlockCounts { get; set; } = new();
    public Dictionary<string, int> FatalCounts { get; set; } = new();
    public Dictionary<string, int> DebuffCounts { get; set; } = new();
    public Dictionary<string, int> DrawCounts { get; set; } = new();
    public Dictionary<string, int> BuffCounts { get; set; } = new();
}

public static class CardPlayStats
{
    private static Dictionary<string, int> _playCounts = new();
    private static Dictionary<string, int> _damageCounts = new();
    private static Dictionary<string, int> _blockCounts = new();
    private static Dictionary<string, int> _fatalCounts = new();
    private static Dictionary<string, int> _debuffCounts = new();
    private static Dictionary<string, int> _drawCounts = new();
    private static Dictionary<string, int> _buffCounts = new();
    private static Dictionary<string, int>? _combatBackup = null;
    private static Dictionary<string, int>? _damageBackup = null;
    private static Dictionary<string, int>? _blockBackup = null;
    private static Dictionary<string, int>? _fatalBackup = null;
    private static Dictionary<string, int>? _debuffBackup = null;
    private static Dictionary<string, int>? _drawBackup = null;
    private static Dictionary<string, int>? _buffBackup = null;
    private static Dictionary<CardModel, (string key, int count)> _pendingUpgrades = new();
    private static Dictionary<CardModel, int> _pendingRemoval = new();
    private static Dictionary<CardModel, int> _pendingDamageRemoval = new();
    private static Dictionary<CardModel, int> _pendingBlockRemoval = new();
    private static Dictionary<CardModel, int> _pendingFatalRemoval = new();
    private static Dictionary<CardModel, int> _pendingDebuffRemoval = new();
    private static Dictionary<CardModel, int> _pendingDrawRemoval = new();
    private static Dictionary<CardModel, int> _pendingBuffRemoval = new();
    
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

    public static bool IsCardInDeck(CardModel card)
    {
        var deck = GetDeck();
        if (deck == null) return false;
        
        var actualCard = card.DeckVersion ?? card;
        return deck.Any(c => ReferenceEquals(c, actualCard));
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
        _damageCounts = new Dictionary<string, int>();
        _blockCounts = new Dictionary<string, int>();
        _fatalCounts = new Dictionary<string, int>();
        _debuffCounts = new Dictionary<string, int>();
        _drawCounts = new Dictionary<string, int>();
        _buffCounts = new Dictionary<string, int>();
        _combatBackup = null;
        _damageBackup = null;
        _blockBackup = null;
        _fatalBackup = null;
        _debuffBackup = null;
        _drawBackup = null;
        _buffBackup = null;
        _currentRunStartTime = 0;
        ShowPlayCount = true;
        ShowHistoryPlayCount = true;
        LoadFromFile();
    }

    public static void Reset()
    {
        _playCounts.Clear();
        _damageCounts.Clear();
        _blockCounts.Clear();
        _fatalCounts.Clear();
        _debuffCounts.Clear();
        _drawCounts.Clear();
        _buffCounts.Clear();
        _combatBackup = null;
        _damageBackup = null;
        _blockBackup = null;
        _fatalBackup = null;
        _debuffBackup = null;
        _drawBackup = null;
        _buffBackup = null;
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
        _damageBackup = new Dictionary<string, int>(_damageCounts);
        _blockBackup = new Dictionary<string, int>(_blockCounts);
        _fatalBackup = new Dictionary<string, int>(_fatalCounts);
        _debuffBackup = new Dictionary<string, int>(_debuffCounts);
        _drawBackup = new Dictionary<string, int>(_drawCounts);
        _buffBackup = new Dictionary<string, int>(_buffCounts);
    }

    public static void OnCombatEnded()
    {
        _combatBackup = null;
        _damageBackup = null;
        _blockBackup = null;
        _fatalBackup = null;
        _debuffBackup = null;
        _drawBackup = null;
        _buffBackup = null;
        SaveToFile();
    }

    public static void RestoreFromBackup()
    {
        if (_combatBackup != null)
        {
            _playCounts = new Dictionary<string, int>(_combatBackup);
            _combatBackup = null;
        }
        if (_damageBackup != null)
        {
            _damageCounts = new Dictionary<string, int>(_damageBackup);
            _damageBackup = null;
        }
        if (_blockBackup != null)
        {
            _blockCounts = new Dictionary<string, int>(_blockBackup);
            _blockBackup = null;
        }
        if (_fatalBackup != null)
        {
            _fatalCounts = new Dictionary<string, int>(_fatalBackup);
            _fatalBackup = null;
        }
        if (_debuffBackup != null)
        {
            _debuffCounts = new Dictionary<string, int>(_debuffBackup);
            _debuffBackup = null;
        }
        if (_drawBackup != null)
        {
            _drawCounts = new Dictionary<string, int>(_drawBackup);
            _drawBackup = null;
        }
        if (_buffBackup != null)
        {
            _buffCounts = new Dictionary<string, int>(_buffBackup);
            _buffBackup = null;
        }
        SaveToFile();
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
        var key = $"{card.Id}{Separator}{card.IsUpgraded}";
        return stats.TryGetValue(key, out var count) ? count : 0;
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

    public static int GetPlayCount(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _playCounts.TryGetValue(key, out var count) ? count : 0;
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
        if (_pendingUpgrades.ContainsKey(card))
        {
            _pendingUpgrades[card] = (key, count);
        }
        else
        {
            _pendingUpgrades.Add(card, (key, count));
        }
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

    private static void SaveToFile()
    {
        try
        {
            var directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var saveData = new CardStatsSaveData
            {
                PlayCounts = new Dictionary<string, int>(_playCounts),
                DamageCounts = new Dictionary<string, int>(_damageCounts),
                BlockCounts = new Dictionary<string, int>(_blockCounts),
                FatalCounts = new Dictionary<string, int>(_fatalCounts),
                DebuffCounts = new Dictionary<string, int>(_debuffCounts),
                DrawCounts = new Dictionary<string, int>(_drawCounts),
                BuffCounts = new Dictionary<string, int>(_buffCounts)
            };
            
            var json = JsonSerializer.Serialize(saveData);
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
                var saveData = JsonSerializer.Deserialize<CardStatsSaveData>(json);
                if (saveData != null)
                {
                    _playCounts = saveData.PlayCounts ?? new Dictionary<string, int>();
                    _damageCounts = saveData.DamageCounts ?? new Dictionary<string, int>();
                    _blockCounts = saveData.BlockCounts ?? new Dictionary<string, int>();
                    _fatalCounts = saveData.FatalCounts ?? new Dictionary<string, int>();
                    _debuffCounts = saveData.DebuffCounts ?? new Dictionary<string, int>();
                    _drawCounts = saveData.DrawCounts ?? new Dictionary<string, int>();
                    _buffCounts = saveData.BuffCounts ?? new Dictionary<string, int>();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[CardStats] Failed to load: {ex.Message}");
            _playCounts = new Dictionary<string, int>();
            _damageCounts = new Dictionary<string, int>();
            _blockCounts = new Dictionary<string, int>();
            _fatalCounts = new Dictionary<string, int>();
            _debuffCounts = new Dictionary<string, int>();
            _drawCounts = new Dictionary<string, int>();
            _buffCounts = new Dictionary<string, int>();
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

    public static void RecordDamage(CardModel card, int damage)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return;
        
        if (!IsCardInDeck(deckCard)) return;
        
        var key = GetCardKey(deckCard);
        
        if (_damageCounts.ContainsKey(key))
        {
            _damageCounts[key] += damage;
        }
        else
        {
            _damageCounts[key] = damage;
        }
    }

    public static int GetDamage(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _damageCounts.TryGetValue(key, out var d) ? d : 0;
    }

    public static bool HasDamage(string key)
    {
        return _damageCounts.ContainsKey(key);
    }

    public static int GetDamageByKey(string key)
    {
        return _damageCounts.TryGetValue(key, out var d) ? d : 0;
    }

    public static void SetDamageByKey(string key, int damage)
    {
        _damageCounts[key] = damage;
    }

    public static void RemoveDamageByKey(string key)
    {
        _damageCounts.Remove(key);
    }

    public static void StorePendingUpgradeDamage(CardModel card, string key, int damage)
    {
        if (_pendingDamageUpgrades.ContainsKey(card))
        {
            _pendingDamageUpgrades[card] = (key, damage);
        }
        else
        {
            _pendingDamageUpgrades.Add(card, (key, damage));
        }
    }

    public static (string key, int damage) GetAndClearPendingUpgradeDamageForCard(CardModel card)
    {
        if (_pendingDamageUpgrades.TryGetValue(card, out var result))
        {
            _pendingDamageUpgrades.Remove(card);
            return result;
        }
        
        return ("", 0);
    }

    private static Dictionary<CardModel, (string key, int damage)> _pendingDamageUpgrades = new();

    public static void PrepareDamageForRemoval(IReadOnlyList<CardModel> cardsToRemove)
    {
        _pendingDamageRemoval.Clear();
        
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
            if (_damageCounts.TryGetValue(key, out var damage))
            {
                _pendingDamageRemoval[card] = damage;
            }
        }
        
        foreach (var card in toRemoveSet)
        {
            _pendingDamageRemoval.Remove(card);
        }
        
        _damageCounts.Clear();
    }

    public static void FinalizeDamageRemoval()
    {
        var deck = GetDeck();
        if (deck == null)
        {
            _pendingDamageRemoval.Clear();
            return;
        }
        
        _damageCounts.Clear();
        
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
            
            if (_pendingDamageRemoval.TryGetValue(card, out var damage))
            {
                var key = $"{baseKey}{Separator}{index}";
                _damageCounts[key] = damage;
            }
        }
        
        _pendingDamageRemoval.Clear();
    }

    public static void RecordBlock(CardModel card, int block)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return;
        
        if (!IsCardInDeck(deckCard)) return;
        
        var key = GetCardKey(deckCard);
        
        if (_blockCounts.ContainsKey(key))
        {
            _blockCounts[key] += block;
        }
        else
        {
            _blockCounts[key] = block;
        }
    }

    public static int GetBlock(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _blockCounts.TryGetValue(key, out var b) ? b : 0;
    }

    public static bool HasBlock(string key)
    {
        return _blockCounts.ContainsKey(key);
    }

    public static int GetBlockByKey(string key)
    {
        return _blockCounts.TryGetValue(key, out var b) ? b : 0;
    }

    public static void SetBlockByKey(string key, int block)
    {
        _blockCounts[key] = block;
    }

    public static void RemoveBlockByKey(string key)
    {
        _blockCounts.Remove(key);
    }

    public static void StorePendingUpgradeBlock(CardModel card, string key, int block)
    {
        if (_pendingBlockUpgrades.ContainsKey(card))
        {
            _pendingBlockUpgrades[card] = (key, block);
        }
        else
        {
            _pendingBlockUpgrades.Add(card, (key, block));
        }
    }

    public static (string key, int block) GetAndClearPendingUpgradeBlockForCard(CardModel card)
    {
        if (_pendingBlockUpgrades.TryGetValue(card, out var result))
        {
            _pendingBlockUpgrades.Remove(card);
            return result;
        }
        
        return ("", 0);
    }

    private static Dictionary<CardModel, (string key, int block)> _pendingBlockUpgrades = new();

    public static void PrepareBlockForRemoval(IReadOnlyList<CardModel> cardsToRemove)
    {
        _pendingBlockRemoval.Clear();
        
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
            if (_blockCounts.TryGetValue(key, out var block))
            {
                _pendingBlockRemoval[card] = block;
            }
        }
        
        foreach (var card in toRemoveSet)
        {
            _pendingBlockRemoval.Remove(card);
        }
        
        _blockCounts.Clear();
    }

    public static void FinalizeBlockRemoval()
    {
        var deck = GetDeck();
        if (deck == null)
        {
            _pendingBlockRemoval.Clear();
            return;
        }
        
        _blockCounts.Clear();
        
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
            
            if (_pendingBlockRemoval.TryGetValue(card, out var block))
            {
                var key = $"{baseKey}{Separator}{index}";
                _blockCounts[key] = block;
            }
        }
        
        _pendingBlockRemoval.Clear();
    }

    public static void RecordFatal(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return;
        
        if (!IsCardInDeck(deckCard)) return;
        
        var key = GetCardKey(deckCard);
        
        if (_fatalCounts.ContainsKey(key))
        {
            _fatalCounts[key]++;
        }
        else
        {
            _fatalCounts[key] = 1;
        }
    }

    public static int GetFatal(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _fatalCounts.TryGetValue(key, out var f) ? f : 0;
    }

    public static bool HasFatal(string key)
    {
        return _fatalCounts.ContainsKey(key);
    }

    public static int GetFatalByKey(string key)
    {
        return _fatalCounts.TryGetValue(key, out var f) ? f : 0;
    }

    public static void SetFatalByKey(string key, int fatal)
    {
        _fatalCounts[key] = fatal;
    }

    public static void RemoveFatalByKey(string key)
    {
        _fatalCounts.Remove(key);
    }

    public static void StorePendingUpgradeFatal(CardModel card, string key, int fatal)
    {
        if (_pendingUpgradeFatals.ContainsKey(card))
        {
            _pendingUpgradeFatals[card] = (key, fatal);
        }
        else
        {
            _pendingUpgradeFatals.Add(card, (key, fatal));
        }
    }

    public static (string key, int fatal) GetAndClearPendingUpgradeFatalForCard(CardModel card)
    {
        if (_pendingUpgradeFatals.TryGetValue(card, out var result))
        {
            _pendingUpgradeFatals.Remove(card);
            return result;
        }
        
        return ("", 0);
    }

    private static Dictionary<CardModel, (string key, int fatal)> _pendingUpgradeFatals = new();

    public static void PrepareFatalForRemoval(IReadOnlyList<CardModel> cardsToRemove)
    {
        _pendingFatalRemoval.Clear();
        
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
            if (_fatalCounts.TryGetValue(key, out var fatal))
            {
                _pendingFatalRemoval[card] = fatal;
            }
        }
        
        foreach (var card in toRemoveSet)
        {
            _pendingFatalRemoval.Remove(card);
        }
        
        _fatalCounts.Clear();
    }

    public static void FinalizeFatalRemoval()
    {
        var deck = GetDeck();
        if (deck == null)
        {
            _pendingFatalRemoval.Clear();
            return;
        }
        
        _fatalCounts.Clear();
        
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
            
            if (_pendingFatalRemoval.TryGetValue(card, out var fatal))
            {
                var key = $"{baseKey}{Separator}{index}";
                _fatalCounts[key] = fatal;
            }
        }
        
        _pendingFatalRemoval.Clear();
    }

    public static void RecordDebuff(CardModel card, int stacks)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return;
        
        if (!IsCardInDeck(deckCard)) return;
        
        var key = GetCardKey(deckCard);
        
        if (_debuffCounts.ContainsKey(key))
        {
            _debuffCounts[key] += stacks;
        }
        else
        {
            _debuffCounts[key] = stacks;
        }
    }

    public static int GetDebuff(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _debuffCounts.TryGetValue(key, out var d) ? d : 0;
    }

    public static bool HasDebuff(string key)
    {
        return _debuffCounts.ContainsKey(key);
    }

    public static int GetDebuffByKey(string key)
    {
        return _debuffCounts.TryGetValue(key, out var d) ? d : 0;
    }

    public static void SetDebuffByKey(string key, int debuff)
    {
        _debuffCounts[key] = debuff;
    }

    public static void RemoveDebuffByKey(string key)
    {
        _debuffCounts.Remove(key);
    }

    public static void StorePendingUpgradeDebuff(CardModel card, string key, int debuff)
    {
        if (_pendingUpgradeDebuffs.ContainsKey(card))
        {
            _pendingUpgradeDebuffs[card] = (key, debuff);
        }
        else
        {
            _pendingUpgradeDebuffs.Add(card, (key, debuff));
        }
    }

    public static (string key, int debuff) GetAndClearPendingUpgradeDebuffForCard(CardModel card)
    {
        if (_pendingUpgradeDebuffs.TryGetValue(card, out var result))
        {
            _pendingUpgradeDebuffs.Remove(card);
            return result;
        }
        
        return ("", 0);
    }

    private static Dictionary<CardModel, (string key, int debuff)> _pendingUpgradeDebuffs = new();

    public static void PrepareDebuffForRemoval(IReadOnlyList<CardModel> cardsToRemove)
    {
        _pendingDebuffRemoval.Clear();
        
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
            if (_debuffCounts.TryGetValue(key, out var debuff))
            {
                _pendingDebuffRemoval[card] = debuff;
            }
        }
        
        foreach (var card in toRemoveSet)
        {
            _pendingDebuffRemoval.Remove(card);
        }
        
        _debuffCounts.Clear();
    }

    public static void FinalizeDebuffRemoval()
    {
        var deck = GetDeck();
        if (deck == null)
        {
            _pendingDebuffRemoval.Clear();
            return;
        }
        
        _debuffCounts.Clear();
        
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
            
            if (_pendingDebuffRemoval.TryGetValue(card, out var debuff))
            {
                var key = $"{baseKey}{Separator}{index}";
                _debuffCounts[key] = debuff;
            }
        }
        
        _pendingDebuffRemoval.Clear();
    }

    public static void RecordDraw(CardModel card, int count)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return;
        
        if (!IsCardInDeck(deckCard)) return;
        
        var key = GetCardKey(deckCard);
        
        if (_drawCounts.ContainsKey(key))
        {
            _drawCounts[key] += count;
        }
        else
        {
            _drawCounts[key] = count;
        }
    }

    public static int GetDraw(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _drawCounts.TryGetValue(key, out var d) ? d : 0;
    }

    public static bool HasDraw(string key)
    {
        return _drawCounts.ContainsKey(key);
    }

    public static int GetDrawByKey(string key)
    {
        return _drawCounts.TryGetValue(key, out var d) ? d : 0;
    }

    public static void SetDrawByKey(string key, int draw)
    {
        _drawCounts[key] = draw;
    }

    public static void RemoveDrawByKey(string key)
    {
        _drawCounts.Remove(key);
    }

    public static void StorePendingUpgradeDraw(CardModel card, string key, int draw)
    {
        if (_pendingDrawUpgrades.ContainsKey(card))
        {
            _pendingDrawUpgrades[card] = (key, draw);
        }
        else
        {
            _pendingDrawUpgrades.Add(card, (key, draw));
        }
    }

    public static (string key, int draw) GetAndClearPendingUpgradeDrawForCard(CardModel card)
    {
        if (_pendingDrawUpgrades.TryGetValue(card, out var result))
        {
            _pendingDrawUpgrades.Remove(card);
            return result;
        }
        
        return ("", 0);
    }

    private static Dictionary<CardModel, (string key, int draw)> _pendingDrawUpgrades = new();

    public static void PrepareDrawForRemoval(IReadOnlyList<CardModel> cardsToRemove)
    {
        _pendingDrawRemoval.Clear();
        
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
            if (_drawCounts.TryGetValue(key, out var draw))
            {
                _pendingDrawRemoval[card] = draw;
            }
        }
        
        foreach (var card in toRemoveSet)
        {
            _pendingDrawRemoval.Remove(card);
        }
        
        _drawCounts.Clear();
    }

    public static void FinalizeDrawRemoval()
    {
        var deck = GetDeck();
        if (deck == null)
        {
            _pendingDrawRemoval.Clear();
            return;
        }
        
        _drawCounts.Clear();
        
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
            
            if (_pendingDrawRemoval.TryGetValue(card, out var draw))
            {
                var key = $"{baseKey}{Separator}{index}";
                _drawCounts[key] = draw;
            }
        }
        
        _pendingDrawRemoval.Clear();
    }

    public static void RecordBuff(CardModel card, int count)
    {
        var deckCard = GetDeckCard(card);
        if (deckCard == null) return;
        
        if (!IsCardInDeck(deckCard)) return;
        
        var key = GetCardKey(deckCard);
        
        if (_buffCounts.ContainsKey(key))
        {
            _buffCounts[key] += count;
        }
        else
        {
            _buffCounts[key] = count;
        }
    }

    public static int GetBuff(CardModel card)
    {
        var deckCard = GetDeckCard(card);
        var key = GetCardKey(deckCard);
        return _buffCounts.TryGetValue(key, out var b) ? b : 0;
    }

    public static bool HasBuff(string key)
    {
        return _buffCounts.ContainsKey(key);
    }

    public static int GetBuffByKey(string key)
    {
        return _buffCounts.TryGetValue(key, out var b) ? b : 0;
    }

    public static void SetBuffByKey(string key, int buff)
    {
        _buffCounts[key] = buff;
    }

    public static void RemoveBuffByKey(string key)
    {
        _buffCounts.Remove(key);
    }

    public static void StorePendingUpgradeBuff(CardModel card, string key, int buff)
    {
        if (_pendingBuffUpgrades.ContainsKey(card))
        {
            _pendingBuffUpgrades[card] = (key, buff);
        }
        else
        {
            _pendingBuffUpgrades.Add(card, (key, buff));
        }
    }

    public static (string key, int buff) GetAndClearPendingUpgradeBuffForCard(CardModel card)
    {
        if (_pendingBuffUpgrades.TryGetValue(card, out var result))
        {
            _pendingBuffUpgrades.Remove(card);
            return result;
        }
        
        return ("", 0);
    }

    private static Dictionary<CardModel, (string key, int buff)> _pendingBuffUpgrades = new();

    public static void PrepareBuffForRemoval(IReadOnlyList<CardModel> cardsToRemove)
    {
        _pendingBuffRemoval.Clear();
        
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
            if (_buffCounts.TryGetValue(key, out var buff))
            {
                _pendingBuffRemoval[card] = buff;
            }
        }
        
        foreach (var card in toRemoveSet)
        {
            _pendingBuffRemoval.Remove(card);
        }
        
        _buffCounts.Clear();
    }

    public static void FinalizeBuffRemoval()
    {
        var deck = GetDeck();
        if (deck == null)
        {
            _pendingBuffRemoval.Clear();
            return;
        }
        
        _buffCounts.Clear();
        
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
            
            if (_pendingBuffRemoval.TryGetValue(card, out var buff))
            {
                var key = $"{baseKey}{Separator}{index}";
                _buffCounts[key] = buff;
            }
        }
        
        _pendingBuffRemoval.Clear();
    }
}

// Modified for ASP Assistant Regulation on 2026-11-11; see NOTICE.
using System.Text.Json;
using System.Text.Json.Serialization;
using ASPAssistant.Core.Models;
using ASPAssistant.Core.Services;

namespace ASPAssistant.Core.Data;

public class SettingsManager
{
    private readonly string _settingsDir;
    private readonly SemaphoreSlim _sidePanelPositionLock = new(1, 1);
    private readonly SemaphoreSlim _trackingLock = new(1, 1);
    private readonly SemaphoreSlim _gameConnectionLock = new(1, 1);
    private static readonly JsonSerializerOptions ThemeJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter<GameShopTheme>(allowIntegerValues: false) }
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsManager(string settingsDir)
    {
        _settingsDir = settingsDir;
    }

    public async Task<GameConnectionSettings> LoadGameConnectionAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(Path.Combine(_settingsDir, "game-connection.json"));
            var settings = JsonSerializer.Deserialize<GameConnectionSettings>(json, JsonOptions);
            if (settings is null || !Enum.IsDefined(settings.Source)
                || settings.AdbPath is null || settings.AdbSerial is null || settings.WindowTitle is null)
                throw new JsonException("游戏连接设置无效");
            return settings;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            AppLogger.Warn("Settings", $"Using PC capture settings: {ex.Message}");
            return new();
        }
    }

    public async Task SaveGameConnectionAsync(GameConnectionSettings settings)
    {
        await _gameConnectionLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var path = Path.Combine(_settingsDir, "game-connection.json");
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(path + ".tmp", path, overwrite: true);
        }
        finally { _gameConnectionLock.Release(); }
    }

    public async Task<bool> LoadLightThemeAsync()
    {
        var path = Path.Combine(_settingsDir, "light-theme.json");
        if (!File.Exists(path)) return false;
        return JsonSerializer.Deserialize<bool>(await File.ReadAllTextAsync(path));
    }

    public async Task SaveLightThemeAsync(bool isLight)
    {
        Directory.CreateDirectory(_settingsDir);
        await File.WriteAllTextAsync(Path.Combine(_settingsDir, "light-theme.json"),
            JsonSerializer.Serialize(isLight));
    }

    public async Task<GameShopTheme?> LoadGameShopThemeAsync()
    {
        var path = Path.Combine(_settingsDir, "game-theme.json");
        try
        {
            var theme = JsonSerializer.Deserialize<GameShopTheme>(await File.ReadAllTextAsync(path), ThemeJsonOptions);
            if (Enum.IsDefined(theme)) return theme;
            AppLogger.Warn("Settings", "模式主题值无效，启动时重新选择");
            return null;
        }
        catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (JsonException)
        {
            AppLogger.Warn("Settings", "模式主题配置无法解析，启动时重新选择");
            return null;
        }
    }

    public async Task SaveGameShopThemeAsync(GameShopTheme theme)
    {
        Directory.CreateDirectory(_settingsDir);
        await File.WriteAllTextAsync(Path.Combine(_settingsDir, "game-theme.json"),
            JsonSerializer.Serialize(theme, ThemeJsonOptions));
    }

    public async Task<bool> LoadTrackMultipleLineupsAsync()
    {
        var path = Path.Combine(_settingsDir, "multiple-lineups.json");
        if (!File.Exists(path)) return false;
        return JsonSerializer.Deserialize<bool>(await File.ReadAllTextAsync(path));
    }

    public async Task SaveTrackMultipleLineupsAsync(bool enabled)
    {
        Directory.CreateDirectory(_settingsDir);
        await File.WriteAllTextAsync(Path.Combine(_settingsDir, "multiple-lineups.json"),
            JsonSerializer.Serialize(enabled));
    }

    public async Task<SidePanelPositionSettings> LoadSidePanelPositionAsync()
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(Path.Combine(_settingsDir, "side-panel-position.json"));
        }
        catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SidePanelPositionSettings();
        }
        var settings = JsonSerializer.Deserialize<SidePanelPositionSettings>(json, JsonOptions)
            ?? throw new JsonException("侧边栏位置配置不能为空");
        if (settings.Placement is { } placement && (!double.IsFinite(placement.Left) || !double.IsFinite(placement.Top)
            || placement.Left < int.MinValue || placement.Left > int.MaxValue
            || placement.Top < int.MinValue || placement.Top > int.MaxValue
            || !double.IsFinite(placement.WidthDip) || placement.WidthDip <= 0
            || !double.IsFinite(placement.HeightDip) || placement.HeightDip < 1))
            throw new JsonException("侧边栏位置或尺寸无效");
        if (settings.FollowOffsetDip is { } offset && (!double.IsFinite(offset.X) || !double.IsFinite(offset.Y)))
            throw new JsonException("侧边栏跟随偏移无效");
        return settings;
    }

    public async Task SaveSidePanelPositionAsync(SidePanelPositionSettings settings)
    {
        await _sidePanelPositionLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var path = Path.Combine(_settingsDir, "side-panel-position.json");
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(path + ".tmp", path, overwrite: true);
        }
        finally { _sidePanelPositionLock.Release(); }
    }

    public async Task SaveTrackingEntriesAsync(List<TrackingEntry> entries)
    {
        Directory.CreateDirectory(_settingsDir);
        var path = Path.Combine(_settingsDir, "tracking.json");
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await _trackingLock.WaitAsync();
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json);
            File.Move(temporary, path, true);
        }
        finally { _trackingLock.Release(); File.Delete(temporary); }
    }

    public async Task<List<TrackingEntry>> LoadTrackingEntriesAsync(LineupEquipmentService equipmentRules)
    {
        var path = Path.Combine(_settingsDir, "tracking.json");
        if (!File.Exists(path))
            return [];

        var json = await File.ReadAllTextAsync(path);
        var entries = JsonSerializer.Deserialize<List<TrackingEntry>>(json, JsonOptions) ?? [];
        var changed = false;
        foreach (var entry in entries.Where(e => e.Type == TrackingType.Operator))
        {
            var slot = new LineupSlot { OperatorName = entry.Name, Equipments = entry.Equipments,
                IsElite = entry.IsElite, TransformedCovenant = entry.TransformedCovenant };
            if (!equipmentRules.UpgradeSlot(slot)) continue;
            entry.TransformedCovenant = slot.TransformedCovenant;
            changed = true;
        }
        if (changed)
        {
            foreach (var name in entries.Where(e => e.Type == TrackingType.Operator).SelectMany(e => e.Equipments).Distinct().ToList())
                if (!entries.Any(e => e.Type == TrackingType.Equipment && e.Name == name))
                    entries.Add(new TrackingEntry { Name = name, Type = TrackingType.Equipment, IsManual = false });
            File.Copy(path, path + $".pre-equipment-{Guid.NewGuid():N}.bak");
            await SaveTrackingEntriesAsync(entries);
        }
        return entries;
    }

    public async Task<string?> LoadSkippedVersionAsync()
    {
        var path = Path.Combine(_settingsDir, "skipped_version.txt");
        if (!File.Exists(path))
            return null;

        var tag = await File.ReadAllTextAsync(path);
        return tag.Trim();
    }

    public async Task SaveSkippedVersionAsync(string tag)
    {
        Directory.CreateDirectory(_settingsDir);
        var path = Path.Combine(_settingsDir, "skipped_version.txt");
        await File.WriteAllTextAsync(path, tag.Trim());
    }

}

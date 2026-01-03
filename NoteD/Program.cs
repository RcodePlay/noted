using Terminal.Gui;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoteD;

Application.Init();

var noteFiles = new ObservableCollection<string>();
Timer? autoSaveTimer = null;
SettingsManager.LoadSettings();
ThemeManager.Initialize();
ThemeManager.ApplyTheme(SettingsManager.Settings.Theme);

// Will use this line for later customization of accent color
// Colors.Base.Normal = Application.Driver.MakeAttribute(Color.Green, Color.Black);

var listView = new ListView
{
    X = 0,
    Y = 0,
    Width = 40,
    Height = Dim.Fill(),
    CanFocus = true
};

var textView = new TextView
{
    X = Pos.Right(listView) + 1,
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill(),
    AllowsTab = true,
    WordWrap = true,
    CanFocus = true
};

var context = new CommandHandlerContext(listView, textView, noteFiles, null, null, false, false);
var handler = new CommandHandler(context);

var notesFolder = handler.LoadOrChooseNotesFolder();
context.NotesFolder = notesFolder;

textView.KeyPress += (key) =>
{
    if (key.KeyEvent.Key >= Key.Space || 
        key.KeyEvent.Key == Key.Backspace || 
        key.KeyEvent.Key == Key.Delete || 
        key.KeyEvent.Key == Key.Enter)
    {
        context.IsNoteDirty = true;
    }
    key.Handled = false; 
};

var win = new Window
{
    Title = "NoteD - The Simple Note Taker",
    X = 0,
    Y = 1,
    Width = Dim.Fill(),
    Height = Dim.Fill()
};

var menu = new MenuBar
{
    Menus =
    [
        new MenuBarItem("_File",
        [
            new MenuItem("_Choose Notes Folder", "", handler.ChooseNotesFolder),
            new MenuItem("_New Note", "", handler.MenuCreateNewNote),
            new MenuItem("_Save Current Note", "", handler.SaveCurrentNote),
            new MenuItem("_Delete Selected Note", "", handler.DeleteSelectedNote),
            new MenuItem("Command _Palette", "", handler.OpenCommandPalette),
            new MenuItem("_Fuzzy Search", "", handler.ShowSearchBar),
            new MenuItem("_Move note to folder", "", handler.MoveFileToFolder),
            new MenuItem("_Quit", "", () => Application.RequestStop())
        ]),
        new MenuBarItem("_Settings",
        [
            new MenuItem("_Open Settings File", "", () =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SettingsManager.SettingsFilePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Error", $"Could not open settings file:\n{ex.Message}", "OK");
                }
            })
        ])
    ]
};

win.Add(listView, textView);

Application.Top.Add(menu, win);

if (SettingsManager.Settings.AutoSaveEnabled)
{
    autoSaveTimer = new Timer(
        callback: (_) => Application.MainLoop.Invoke(handler.SaveNoteIfDirty),
        state: null,
        dueTime: SettingsManager.Settings.AutoSaveDelayMs, 
        period: SettingsManager.Settings.AutoSaveDelayMs 
    );
}

handler.RefreshNoteList();

listView.SelectedItemChanged += (args) =>
{
    var sel = args.Item;
    if (noteFiles.Count == 0 || sel < 0 || sel >= noteFiles.Count) return;

    if (context.IsNoteDirty)
    {
        handler.SaveNoteIfDirty(); 
    }

    var selectedFile = Path.Combine(notesFolder, noteFiles[sel]);

    if (!File.Exists(selectedFile))
        return;
    
    textView.Text = File.ReadAllText(selectedFile).Replace("\r\n", "\n");
    context.CurrentFilePath = selectedFile;
    context.IsNoteDirty = false;
};

Application.Run();
autoSaveTimer?.Dispose();
Application.Shutdown();

public class NoteDSettings
{
    [JsonPropertyName("auto_save_enabled")]
    public bool AutoSaveEnabled { get; } = true;

    [JsonPropertyName("auto_save_delay_ms")]
    public int AutoSaveDelayMs { get; } = 5000;
    
    [JsonPropertyName("show_markdown_preview")]
    public bool ShowMarkdownPreview { get; }
    
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Classic";
}

public record SearchResult(string FileName, string Snippet)
{
    public override string ToString() => $"[{FileName}] {Snippet}";
}

public static class SettingsManager
{
    public static NoteDSettings Settings { get; private set; } = new ();

    public static string SettingsFilePath
    {
        get
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NoteD"
            );
            Directory.CreateDirectory(appDataFolder);
            return Path.Combine(appDataFolder, "settings.json");
        }
    }

    public static void LoadSettings()
    {
        var path = SettingsFilePath;
        if (File.Exists(path))
        {
            try
            {
                var jsonString = File.ReadAllText(path);
                var loadedSettings = JsonSerializer.Deserialize<NoteDSettings>(jsonString);
                
                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                }
                else
                {
                    Settings = new NoteDSettings(); 
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                Settings = new NoteDSettings();
                SaveSettings();
            }
        }
        else
        {
            Settings = new NoteDSettings();
            SaveSettings();
        }
    }

    public static void SaveSettings()
    {
        var path = SettingsFilePath;
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(path, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
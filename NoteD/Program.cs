using Terminal.Gui;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoteD;


Application.Init();

var noteFiles = new ObservableCollection<string>();
Timer? autoSaveTimer = null;
SettingsManager.LoadSettings();

if (SettingsManager.Settings.CustomThemes != null)
    foreach (var theme in SettingsManager.Settings.CustomThemes)
    {
        ThemeManager.Theme realTheme;
        
        if (theme.Value.Dialog != null)
        {
            realTheme = new ThemeManager.Theme
            {
                Main = ThemeManager.ParseScheme(theme.Value.Main),
                Dialog = ThemeManager.ParseScheme(theme.Value.Dialog)
            };
        }
        else
        {
            realTheme = new ThemeManager.Theme
            {
                Main = ThemeManager.ParseScheme(theme.Value.Main),
                Dialog = Colors.Dialog
            };
        }
        
        ThemeManager.Themes[theme.Key] = realTheme;
    }
        
ThemeManager.Initialize();
ThemeManager.ApplyTheme(SettingsManager.Settings.Theme);

string? masterPassword = null; 

var loginPromptDialog = new Dialog { Title = "NoteD Login", Modal = true };
var loginLabel = new Label("Enter your master password. If you forget this password, THERE IS NO WAY TO RESET IT!!") { X = 1, Y = 1 };
var loginInput = new TextField("") { X = 1, Y = 2, Width = Dim.Fill() - 2, Secret = true };
var okBtn = new Button("Login") { IsDefault = true };
var exitBtn = new Button("Exit");

loginPromptDialog.Add(loginLabel, loginInput);
loginPromptDialog.AddButton(okBtn);
loginPromptDialog.AddButton(exitBtn);

okBtn.Clicked += () => {
    if (string.IsNullOrWhiteSpace(loginInput.Text.ToString())) {
        MessageBox.ErrorQuery("Error", "Password cannot be empty", "OK");
        return;
    }
    masterPassword = loginInput.Text.ToString();
    Application.RequestStop();
};

exitBtn.Clicked += () => {
    Application.RequestStop();
    Environment.Exit(0);
};

Application.Run(loginPromptDialog);

if (string.IsNullOrWhiteSpace(masterPassword)) Environment.Exit(0);

var security = new SecurityModule();

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

const string cursor = "Line: 1, Col: 1";
const string stats = "Chars: 0, Words: 0";

var statusBar = new StatusBar([
    new StatusItem(Key.Null, cursor, null),
    new StatusItem(Key.Null, stats, null)
]);

var context = new CommandHandlerContext(listView, textView, statusBar, noteFiles, null, null, false, false, SettingsManager.Settings.ExternalEditor);
var handler = new CommandHandler(context);

var notesFolder = handler.LoadOrChooseNotesFolder();
context.NotesFolder = notesFolder;

handler.RefreshNoteList();

foreach (var note in context.NoteFiles)
{
    try
    {
        security.UnprotectFile(Path.Combine(notesFolder, note), masterPassword);
    }
    catch (InvalidDataException)
    {
        Console.WriteLine($"File {note} is not encrypted. Skipping decryption.");
    }
    catch (CryptographicException)
    {
        MessageBox.ErrorQuery("Access Denied", "Incorrect Password.", "OK");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error decrypting file {note}: {ex.Message}");
    }
}

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
            new MenuItem("_Recent Notes", "", handler.ShowRecentNotes),
            new MenuItem("Open in _External Editor", "", handler.OpenInEditorWrapper),
            new MenuItem("_Quit", "", () => Application.RequestStop())
        ]),
        new MenuBarItem("_Utilities", [
            new MenuItem("Command _Palette", "", handler.OpenCommandPalette),
            new MenuItem("_Fuzzy Search", "", handler.ShowSearchBar),
            new MenuItem("_Move note to folder", "", handler.MoveFileToFolder),
            new MenuItem("Open _Theme Switcher", "", handler.OpenThemeSwitcher)
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

var top = new Toplevel {
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill()
};

top.Add(menu, win, statusBar);

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

textView.ContentsChanged += _ =>
{
    handler.UpdateStatusBar();
};

textView.UnwrappedCursorPosition += _ =>
{
    handler.UpdateStatusBar();
};

Application.Run(top);

foreach (var note in noteFiles)
{
    var path = Path.Combine(notesFolder, note);
    if (File.Exists(path))
    {
        security.ProtectFile(path, masterPassword);
    }
}

autoSaveTimer?.Dispose();
Application.Shutdown();

public class NoteDSettings
{
    [JsonPropertyName("auto_save_enabled")]
    public bool AutoSaveEnabled { get; init; } = true;

    [JsonPropertyName("auto_save_delay_ms")]
    public int AutoSaveDelayMs { get; init; } = 5000;

    [JsonPropertyName("external_editor")]
    public string ExternalEditor { get; init; } = "";
    
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Classic";
    
    [JsonPropertyName("custom_themes")]
    public Dictionary<string, ThemeManager.ThemeProxy>? CustomThemes { get; init; }
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
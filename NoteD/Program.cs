using Terminal.Gui;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FuzzySharp;
using NStack;


Application.Init();

string? notesFolder = null;
string? currentFilePath = null;

var noteFiles = new ObservableCollection<string>();

// Global flags
var isNoteDirty = false;
var noteCreationSuccess = false;

Timer? autoSaveTimer = null;

SettingsManager.LoadSettings();

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

textView.KeyPress += (key) =>
{
    if (key.KeyEvent.Key >= Key.Space || 
        key.KeyEvent.Key == Key.Backspace || 
        key.KeyEvent.Key == Key.Delete || 
        key.KeyEvent.Key == Key.Enter)
    {
        isNoteDirty = true;
    }
    key.Handled = false; 
};

notesFolder = LoadOrChooseNotesFolder();

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
            new MenuItem("_Choose Notes Folder", "", ChooseNotesFolder),
            new MenuItem("_New Note", "", MenuCreateNewNote),
            new MenuItem("_Save Current Note", "", SaveCurrentNote),
            new MenuItem("_Delete Selected Note", "", DeleteSelectedNote),
            new MenuItem("_Fuzzy Search", "", ShowSearchBar),
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
        callback: (_) => Application.MainLoop.Invoke(SaveNoteIfDirty),
        state: null,
        dueTime: SettingsManager.Settings.AutoSaveDelayMs, 
        period: SettingsManager.Settings.AutoSaveDelayMs 
    );
}

RefreshNoteList();

listView.SelectedItemChanged += (args) =>
{
    var sel = args.Item;
    if (noteFiles.Count == 0 || sel < 0 || sel >= noteFiles.Count) return;

    if (isNoteDirty)
    {
        SaveNoteIfDirty(); 
    }

    var selectedFile = Path.Combine(notesFolder!, noteFiles[sel]);

    if (File.Exists(selectedFile))
    {
        textView.Text = File.ReadAllText(selectedFile).Replace("\r\n", "\n");
        currentFilePath = selectedFile;
        isNoteDirty = false;
    }
};

void RefreshNoteList()
{
    if (!Directory.Exists(notesFolder))
    {
        noteFiles.Clear();
        listView.SetSource(noteFiles);
        textView.Text = "";
        currentFilePath = null;
        return;
    }

    if (notesFolder == null)
    {
        noteFiles.Clear();
        listView.SetSource(noteFiles);
        return;
    }

    var files = Directory.GetFiles(notesFolder, "*.md")
        .Select(Path.GetFileName)
        .Cast<string>()
        .OrderByDescending(f => f)
        .ToList();
    
    
    noteFiles.Clear();
    foreach (var f in files) noteFiles.Add(f);
    listView.SetSource(noteFiles);

    if (noteFiles.Count > 0 && (listView.SelectedItem < 0 || listView.SelectedItem >= noteFiles.Count))
    {
        listView.SelectedItem = 0;
    }
}

void ChooseNotesFolder()
{
    var examplePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\Users\You\Documents\NoteD" : "/home/you/Documents/NoteD";
    
    var inputDialog = new Dialog
    {
        Title = "Choose Notes Folder",
        Width = 60,
        Height = 10
    };

    var label = new Label
    {
        Text = $"Enter full path to notes folder (e.g. {examplePath}):",
        X = 1,
        Y = 1,
        Width = Dim.Fill() - 2
    };

    var textField = new TextField
    {
        X = 1,
        Y = 3,
        Width = Dim.Fill() - 2
    };

    var okButton = new Button
    {
        Text = "OK",
        IsDefault = true
    };

    okButton.Clicked += () =>
    {
        var path = (textField.Text ?? ustring.Empty).ToString()!.Trim(); 
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                Directory.CreateDirectory(path);
                notesFolder = path;
                RefreshNoteList();
                MessageBox.Query("Success", $"Notes folder set to:\n{notesFolder}", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Invalid path:\n{ex.Message}", "OK");
            }
        }
        Application.RequestStop();
    };

    var cancelButton = new Button { Text = "Cancel" };
    cancelButton.Clicked += () => Application.RequestStop();

    inputDialog.Add(label, textField);
    inputDialog.AddButton(okButton);
    inputDialog.AddButton(cancelButton);

    Application.Run(inputDialog);
}

string LoadOrChooseNotesFolder()
{
    var defaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NoteD"
    );

    if (Directory.Exists(defaultFolder))
        return defaultFolder;

    ChooseNotesFolder();
    return notesFolder ?? throw new Exception("No folder selected");
}

void CreateNewNote(string contentToSave)
{
    if (string.IsNullOrEmpty(notesFolder))
    {
        MessageBox.ErrorQuery("Error", "No notes folder selected yet.", "OK");
        return;
    }

    var nameDialog = new Dialog
    {
        Title = "New Note",
        Width = 50,
        Height = 10
    };

    var label = new Label
    {
        Text = "Optional name (leave blank for just date):",
        X = 1,
        Y = 1
    };

    var nameField = new TextField
    {
        X = 1,
        Y = 3,
        Width = Dim.Fill() - 2
    };

    var ok = new Button
    {
        Text = "Create",
        IsDefault = true
    };
    
    noteCreationSuccess = false;

    ok.Clicked += () =>
    {
        var optional = (nameField.Text ?? ustring.Empty).ToString()!.Trim(); 
        var date = DateTime.Today.ToString("yyyy-MM-dd");
        var slug = string.IsNullOrWhiteSpace(optional) ? "" : $"-{optional}";
        var filename = $"{date}{slug}.md";
        var fullPath = Path.Combine(notesFolder!, filename);

        var counter = 1;
        while (File.Exists(fullPath))
        {
            filename = $"{date}{slug}-{counter++}.md";
            fullPath = Path.Combine(notesFolder!, filename);
        }

        File.WriteAllText(fullPath, contentToSave);
        RefreshNoteList();

        var index = noteFiles.IndexOf(filename);
        if (index >= 0)
        {
            listView.SelectedItem = index;
            textView.Text = contentToSave;
            currentFilePath = fullPath;
            isNoteDirty = false;
            textView.SetFocus();
        }

        noteCreationSuccess = true;
        Application.RequestStop();
    };
    
    var cancelButton = new Button { Text = "Cancel" };
    cancelButton.Clicked += () =>
    {
        textView.Text = contentToSave;
        noteCreationSuccess = false;
        Application.RequestStop();
    };

    nameDialog.Add(label, nameField);
    nameDialog.AddButton(ok);
    nameDialog.AddButton(cancelButton);

    Application.Run(nameDialog);
}

void MenuCreateNewNote()
{
    CreateNewNote("");
}

void SaveNoteIfDirty()
{
    if (isNoteDirty && !string.IsNullOrEmpty(currentFilePath) && !string.IsNullOrEmpty(notesFolder))
    {
        try
        {
            File.WriteAllText(currentFilePath, textView.Text!.ToString());
            isNoteDirty = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-Save Failed: {ex.Message}");
        }
    }
}

void SaveCurrentNote()
{
    var unsavedContent = textView.Text.ToString();
    
    if (string.IsNullOrEmpty(currentFilePath) || string.IsNullOrEmpty(notesFolder))
    {
        if (string.IsNullOrEmpty(unsavedContent))
        {
            MessageBox.Query("NoteD", "Nothing to save.", "OK");
            return;
        }
        
        CreateNewNote(unsavedContent);

        if (!noteCreationSuccess)
            return;
    }

    try
    {
        File.WriteAllText(currentFilePath!, unsavedContent);
        isNoteDirty = false; // Reset dirty flag after manual save
        MessageBox.Query("Saved", $"Saved to {Path.GetFileName(currentFilePath)}", "OK");
    }
    catch (Exception ex)
    {
        MessageBox.ErrorQuery("Error", $"Could not save:\n{ex.Message}", "OK");
    }
}

void DeleteSelectedNote()
{
    if (string.IsNullOrEmpty(currentFilePath))
    {
        MessageBox.ErrorQuery("Error", "No note selected.", "OK");
        return;
    }

    var res = MessageBox.Query("Delete?", $"Delete {Path.GetFileName(currentFilePath)}?", "Yes", "No");
    if (res != 0)
    {
        return;
    }

    try
    {
        File.Delete(currentFilePath);
        RefreshNoteList();
        textView.Text = "";
        currentFilePath = null;
        isNoteDirty = false;
    }
    catch (Exception ex)
    {
        MessageBox.ErrorQuery("Error", ex.Message, "OK");
    }
    
}

string GetContent(string title)
{
    try
    {
        return File.ReadAllText(title).Replace("\r\n", "\n");
    }
    catch (Exception ex)
    {
        MessageBox.ErrorQuery("Error", ex.Message, "OK");
    }
    return "";
}

void ShowSearchBar()
{
    var searchDialog = new Dialog
    {
        Title = "Fuzzy Search",
        X = Pos.Center(),
        Y = 1,
        Width = Dim.Percent(50),
        Height = 10,
        Modal = true
    };

    var textField = new TextField
    {
        X = 1,
        Y = 2,
        Width = Dim.Fill() - 2,
        Height = 1,
        CanFocus = true
    };

    var searchListView = new ListView
    {
        X = 1,
        Y = 4,
        Width = Dim.Fill() - 2,
        Height = Dim.Fill() - 1
    };

    var searchResults = new List<SearchResult>();
    searchListView.SetSource(searchResults);

    void RunSearch(string query)
    {
        try
        {
            searchResults.Clear();
            if (string.IsNullOrWhiteSpace(query)) return;

            foreach (var fileName in noteFiles)
            {
                var nameScore = Fuzz.PartialRatio(query.ToLower(), fileName.ToLower());
                if (nameScore > 70)
                    searchResults.Add(new SearchResult(fileName, "Match in file name"));

                var content = GetContent(Path.Combine(notesFolder!, fileName));
                if (string.IsNullOrEmpty(content)) return;
                
                var contentScore = Fuzz.PartialRatio(query.ToLower(), content.ToLower());
                if (contentScore > 70)
                {
                    var snippet = ExtractSnippet(content, query);
                    if (!searchResults.Any(r => r.FileName == fileName && r.Snippet == snippet))
                        searchResults.Add(new SearchResult(fileName, snippet));
                }
            }
            
            searchListView.SetSource(searchResults.ToList());
            searchListView.SetNeedsDisplay();

            if (searchResults.Count > 0)
                searchListView.SelectedItem = 0;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error running search", ex.Message, "OK");
        }
    }

    string ExtractSnippet(string text, string query)
    {
        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index == -1) return "Match found ...";
        
        var words = text.Split([ ' ', '\n', '\r', '\t' ], StringSplitOptions.RemoveEmptyEntries);
        var wordIndex = -1;
        var currentPos = 0;

        for (var i = 0; i < words.Length; i++)
        {
            var foundAt = text.IndexOf(words[i], currentPos, StringComparison.Ordinal);
            if (foundAt >= index)
            {
                wordIndex = i;
                break;
            }
            currentPos = foundAt + words[i].Length;
        }
        
        if (wordIndex == -1) return "..." + query + "...";
        
        var start = Math.Max(0, wordIndex - 1);
        var count = Math.Min(words.Length - start, 3);
        
        var contextWords = words.Skip(start).Take(count);
        return "..." + string.Join(" ", contextWords) + "...";
    }
    
    var debounceTimer = new System.Timers.Timer(250) { AutoReset = false };

    textField.TextChanged += _ =>
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    };

    debounceTimer.Elapsed += (_, _) =>
    {
        Application.MainLoop.Invoke(() =>
        {
            RunSearch(textField.Text.ToString()!);
        });
    };

    searchListView.OpenSelectedItem += args =>
    {
        var result = searchResults[args.Item];
        var originalIndex = noteFiles.IndexOf(result.FileName);
        if (originalIndex >= 0)
            listView.SelectedItem = originalIndex;

        Application.Top.Remove(searchDialog);
    };
    searchDialog.Add(textField, searchListView);
    Application.Run(searchDialog);
}

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

    private static void SaveSettings()
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
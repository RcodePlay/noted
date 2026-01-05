using Terminal.Gui;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using FuzzySharp;
using NStack;
using System.Diagnostics;

namespace NoteD;

public record CommandHandlerContext(
    ListView ListView,
    TextView TextView,
    StatusBar StatusBar,
    ObservableCollection<string> NoteFiles,
    string? NotesFolder,
    string? CurrentFilePath,
    bool IsNoteDirty,
    bool NoteCreationSuccess,
    string ExternalEditor
)
{
    public ObservableCollection<string> NoteFiles { get; set; } = NoteFiles;
    public string? NotesFolder { get; set; } = NotesFolder;
    public string? CurrentFilePath { get; set; } = CurrentFilePath;
    public bool IsNoteDirty { get; set; } = IsNoteDirty;
    public bool NoteCreationSuccess { get; set; } = NoteCreationSuccess;
}

public class CommandHandler(CommandHandlerContext context)
{
    
    public void RefreshNoteList()
    {
        if (!Directory.Exists(context.NotesFolder))
        {
            context.NoteFiles.Clear();
            context.ListView.SetSource(context.NoteFiles);
            context.TextView.Text = "";
            context.CurrentFilePath = null;
            return;
        }

        if (context.NotesFolder == null)
        {
            context.NoteFiles.Clear();
            context.ListView.SetSource(context.NoteFiles);
            return;
        }

        var files = Directory.EnumerateFiles(context.NotesFolder, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(context.NotesFolder, f))
            .OrderByDescending(f => f)
            .ToList();
    
    
        context.NoteFiles.Clear();
        foreach (var f in files) context.NoteFiles.Add(f);
        context.ListView.SetSource(context.NoteFiles);

        if (context.NoteFiles.Count > 0 && (context.ListView.SelectedItem < 0 || context.ListView.SelectedItem >= context.NoteFiles.Count))
        {
            context.ListView.SelectedItem = 0;
        }
    }
    public void ChooseNotesFolder()
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
                    context.NotesFolder = path;
                    RefreshNoteList();
                    MessageBox.Query("Success", $"Notes folder set to:\n{context.NotesFolder}", "OK");
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

    public string LoadOrChooseNotesFolder()
    {
        var defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NoteD"
        );

        if (Directory.Exists(defaultFolder))
            return defaultFolder;

        ChooseNotesFolder();
        return context.NotesFolder ?? throw new Exception("No folder selected");
    }

    private void CreateNewNote(string contentToSave)
    {
        if (string.IsNullOrEmpty(context.NotesFolder))
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
    
        context.NoteCreationSuccess = false;

        ok.Clicked += () =>
        {
            var optional = (nameField.Text ?? ustring.Empty).ToString()!.Trim(); 
            var date = DateTime.Today.ToString("yyyy-MM-dd");
            var slug = string.IsNullOrWhiteSpace(optional) ? "" : $"-{optional}";
            var filename = $"{date}{slug}.md";
            var fullPath = Path.Combine(context.NotesFolder!, filename);

            var counter = 1;
            while (File.Exists(fullPath))
            {
                filename = $"{date}{slug}-{counter++}.md";
                fullPath = Path.Combine(context.NotesFolder!, filename);
            }

            File.WriteAllText(fullPath, contentToSave);
            RefreshNoteList();

            var index = context.NoteFiles.IndexOf(filename);
            if (index >= 0)
            {
                context.ListView.SelectedItem = index;
                context.TextView.Text = contentToSave;
                context.CurrentFilePath = fullPath;
                context.IsNoteDirty = false;
                context.TextView.SetFocus();
            }

            context.NoteCreationSuccess = true;
            Application.RequestStop();
        };
    
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Clicked += () =>
        {
            context.TextView.Text = contentToSave;
            context.NoteCreationSuccess = false;
            Application.RequestStop();
        };

        nameDialog.Add(label, nameField);
        nameDialog.AddButton(ok);
        nameDialog.AddButton(cancelButton);

        Application.Run(nameDialog);
    }

    public void MenuCreateNewNote()
    {
        CreateNewNote("");
    }

    public void SaveNoteIfDirty()
    {
        if (context.IsNoteDirty && !string.IsNullOrEmpty(context.CurrentFilePath) && !string.IsNullOrEmpty(context.NotesFolder))
        {
            try
            {
                File.WriteAllText(context.CurrentFilePath, context.TextView.Text!.ToString());
                context.IsNoteDirty = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-Save Failed: {ex.Message}");
            }
        }
    }

    public void SaveCurrentNote()
    {
        var unsavedContent = context.TextView.Text.ToString();
    
        if (string.IsNullOrEmpty(context.CurrentFilePath) || string.IsNullOrEmpty(context.NotesFolder))
        {
            if (string.IsNullOrEmpty(unsavedContent))
            {
                MessageBox.Query("NoteD", "Nothing to save.", "OK");
                return;
            }
        
            CreateNewNote(unsavedContent);

            if (!context.NoteCreationSuccess)
                return;
        }

        try
        {
            File.WriteAllText(context.CurrentFilePath!, unsavedContent);
            context.IsNoteDirty = false; // Reset dirty flag after manual save
            MessageBox.Query("Saved", $"Saved to {Path.GetFileName(context.CurrentFilePath)}", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Could not save:\n{ex.Message}", "OK");
        }
    }

    public void DeleteSelectedNote()
    {
        if (string.IsNullOrEmpty(context.CurrentFilePath))
        {
            MessageBox.ErrorQuery("Error", "No note selected.", "OK");
            return;
        }

        var res = MessageBox.Query("Delete?", $"Delete {Path.GetFileName(context.CurrentFilePath)}?", "Yes", "No");
        if (res != 0)
        {
            return;
        }

        try
        {
            File.Delete(context.CurrentFilePath);
            RefreshNoteList();
            context.TextView.Text = "";
            context.CurrentFilePath = null;
            context.IsNoteDirty = false;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", ex.Message, "OK");
        }
    
    }
    
    private (int chars, int words) GetTextStats(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (0, 0);

        var charCount = text.Length;
        var wordCount = text.Split([' ', '\r', '\n', '\t'], 
            StringSplitOptions.RemoveEmptyEntries).Length;

        return (charCount, wordCount);
    }

    public void UpdateStatusBar()
    {
        var cursorInfo = $"Line: {context.TextView.CurrentRow + 1}, Col: {context.TextView.CurrentColumn + 1}";
        var stats = GetTextStats(context.TextView.Text.ToString()!);
        var displayStats = $", Chars: {stats.chars}, Words: {stats.words}";
        
        context.StatusBar.Items[0].Title = cursorInfo; 
        context.StatusBar.Items[1].Title = displayStats;
        context.StatusBar.SetNeedsDisplay();
    }

    private string GetContent(string title)
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

    public void ShowSearchBar()
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
                
                var isTagSearch = query.StartsWith("#");
                var cleanQuery = query.ToLower().Trim();

                foreach (var fileName in context.NoteFiles)
                {
                    var fullPath = Path.Combine(context.NotesFolder!, fileName);
                    var content = GetContent(fullPath);
                    if (string.IsNullOrEmpty(content)) continue;

                    if (isTagSearch)
                    {
                        if (content.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase))
                        {
                            searchResults.Add(new SearchResult(fileName, $"Tag match: {cleanQuery}"));
                        }
                    }
                    else
                    {
                        var nameScore = Fuzz.PartialRatio(query.ToLower(), fileName.ToLower());
                        if (nameScore > 70)
                            searchResults.Add(new SearchResult(fileName, "Match in file name"));
                    
                        if (string.IsNullOrEmpty(content)) return;
                
                        var contentScore = Fuzz.PartialRatio(query.ToLower(), content.ToLower());
                        if (contentScore > 70)
                        {
                            var snippet = ExtractSnippet(content, query);
                            if (!searchResults.Any(r => r.FileName == fileName && r.Snippet == snippet))
                                searchResults.Add(new SearchResult(fileName, snippet));
                        }
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
            var originalIndex = context.NoteFiles.IndexOf(result.FileName);
            if (originalIndex >= 0)
                context.ListView.SelectedItem = originalIndex;

            Application.Top.Remove(searchDialog);
        };
        searchDialog.Add(textField, searchListView);
        Application.Run(searchDialog);
    }

    public void MoveFileToFolder()
    {
        var moveDialog = new Dialog
        {
            Title = "Move note to folder",
            X = Pos.Center(),
            Y = 1,
            Width = 60,
            Height = 20,
            Modal = true
        };

        var textField = new TextField("")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = 1,
            CanFocus = true
        };

        var folderListView = new ListView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1),
            Height = Dim.Fill(6),
            AllowsMarking = false,
            CanFocus = true
        };

        var allFolders = Directory.EnumerateDirectories(context.NotesFolder!)
            .Select(d => new {
                FullPath = d,
                Name = Path.GetFileName(d)
            })
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        allFolders.Add(new { FullPath = context.NotesFolder!, Name = "/" });
    
        var currentFolders = allFolders.ToList();
    
        void RefreshList()
        {
            var displayNames = currentFolders.Select(f => f.Name).ToList();
            folderListView.SetSource(displayNames);
            folderListView.SelectedItem = displayNames.Count > 0 ? 0 : -1;
        }

        RefreshList();
    
        if (string.IsNullOrWhiteSpace(context.CurrentFilePath))
        {
            MessageBox.Query("No selected file", "Please select a file to move first.", "OK");
            return;
        }
    
        var fileToMove = Path.GetFileName(context.CurrentFilePath);
    
        textField.TextChanged += _ =>
        {
            var query = textField.Text.ToString() ?? "";
    
            if (string.IsNullOrWhiteSpace(query))
                currentFolders = allFolders.ToList();
            else
                currentFolders = allFolders
                    .Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
    
            RefreshList();
        };
    
        var okButton = new Button("OK", is_default: true);
        var cancelButton = new Button("Cancel");
    
        okButton.Clicked += () =>
        {
            if (folderListView.SelectedItem >= 0 && folderListView.SelectedItem < currentFolders.Count)
            {
                var selected = currentFolders[folderListView.SelectedItem];
                var targetPath = Path.Combine(selected.FullPath, fileToMove);
    
                try
                {
                    File.Move(context.CurrentFilePath, targetPath);
                    MessageBox.Query("Success", $"Moved to {selected.Name}", "OK");
                    context.CurrentFilePath = targetPath;
                    RefreshNoteList();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Error", $"Could not move file:\n{ex.Message}", "OK");
                }
            }
    
            Application.RequestStop();
        };
    
        cancelButton.Clicked += () => Application.RequestStop();

        moveDialog.Add(textField, folderListView, okButton, cancelButton);
    
        okButton.X = Pos.Center() - 10;
        okButton.Y = Pos.Bottom(moveDialog) - 3;
        cancelButton.X = Pos.Center() + 5;
        cancelButton.Y = Pos.Bottom(moveDialog) - 3;
    
        moveDialog.FocusFirst();
    
        Application.Run(moveDialog);
    }

    public void OpenCommandPalette()
    {
        var commandPaletteDialog = new Dialog
        {
            Title = "Command Palette",
            X = Pos.Center(),
            Y = 1,
            Width = Dim.Percent(50),
            Height = Dim.Percent(50),
            Modal = true
        };

        var input = new TextField
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = 1,
            CanFocus = true
        };

        var commandList = new ListView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
            AllowsMarking = false,
            CanFocus = true
        };

        var allCommands = CommandMap.Keys.ToList();
        var currentCommands = new List<string>();
        currentCommands.AddRange(allCommands);
        commandList.SetSource(currentCommands);

        input.TextChanged += _ =>
        {
            var query = input.Text.ToString() ?? "";
        
            if (string.IsNullOrWhiteSpace(query))
                currentCommands = allCommands.ToList();
            else
                currentCommands = allCommands
                    .Where(c => c.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        
            commandList.SetSource(currentCommands);
        };
        
        commandList.OpenSelectedItem += args =>
        {
            CommandMap[currentCommands[args.Item]].Invoke();
            Application.RequestStop();
        };
        
        commandPaletteDialog.Add(input, commandList);
        Application.Run(commandPaletteDialog);
    }

    public void OpenThemeSwitcher()
    {
        var themeSwitcherDialog = new Dialog
        {
            Title = "Theme Switcher",
            X = Pos.Center(),
            Y = 1,
            Width = Dim.Percent(50),
            Height = Dim.Percent(50),
            Modal = true
        };
        
        var themes = ThemeManager.Themes.Keys.ToList();

        var listview = new ListView(themes)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1)
        };
        themeSwitcherDialog.Add(listview);

        listview.SetSource(themes);
        
        listview.OpenSelectedItem += (args) =>
        {
            ThemeManager.ApplyTheme(themes[args.Item]);
            SettingsManager.Settings.Theme = themes[args.Item];
            SettingsManager.SaveSettings();
            Application.RequestStop();
        };

        Application.Run(themeSwitcherDialog);
    }

    public void ShowRecentNotes()
    {
        var recentsDialog = new Dialog
        {
            Title = "Recent Notes",
            X = Pos.Center(),
            Y = 1,
            Width = Dim.Percent(50),
            Height = Dim.Percent(50),
            Modal = true
        };

        var recentsList = new ListView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1)
        };
        
        recentsDialog.Add(recentsList);
        
        var recentFiles = context.NoteFiles
            .Select(f => new {
                FileName = f,
                FullPath = Path.Combine(context.NotesFolder!, f),
                LastModified = File.GetLastWriteTime(Path.Combine(context.NotesFolder!, f))
            })
            .OrderByDescending(f => f.LastModified)
            .Take(6)
            .ToList();
        
        var recentFileNames = recentFiles.Select(f => f.FileName).ToList();
        
        recentsList.SetSource(recentFileNames);

        recentsList.OpenSelectedItem += (args) =>
        {
            var selected = recentFiles[args.Item];
            context.CurrentFilePath = selected.FullPath;
            context.TextView.Text = GetContent(selected.FullPath);
            context.ListView.SelectedItem = context.NoteFiles.IndexOf(selected.FileName);
            
            Application.RequestStop();
        };
        
        Application.Run(recentsDialog);
    }

    private void OpenInEditor(string filePath)
    {
        var editor = context.ExternalEditor;

        if (string.IsNullOrWhiteSpace(editor)) return;
        
        var startInfo = new ProcessStartInfo
        {
           FileName = editor,
           Arguments = $"\"{filePath}\"",
           UseShellExecute = true, 
           CreateNoWindow = false
        };

        try
        {
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.WriteLine($"Error: Could not find '{editor}'. " +
                              "Ensure it is in your PATH or the full path is correct.");
        }
    }

    public void OpenInEditorWrapper()
    {
        var file = context.CurrentFilePath;
        if (file != null) OpenInEditor(file);
    }

    private Dictionary<string, Action> CommandMap => new()
    {
        { "Create New Note", MenuCreateNewNote },
        { "Save Current Note", SaveCurrentNote },
        { "Delete Selected Note", DeleteSelectedNote },
        { "Fuzzy Search Notes", ShowSearchBar },
        { "Show Recent Notes", ShowRecentNotes },
        { "Open in External Editor", OpenInEditorWrapper },
        { "Move Note to Folder", MoveFileToFolder },
        { "Choose Notes Folder", ChooseNotesFolder },
        { "Open Theme Switcher", OpenThemeSwitcher }
    };
}
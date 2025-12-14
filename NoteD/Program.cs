using Terminal.Gui;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using NStack;


Application.Init();

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

string? notesFolder = null;
string? currentFilePath = null;

var noteFiles = new ObservableCollection<string>();

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
            new MenuItem("_New Note", "", CreateNewNote),
            new MenuItem("_Save Current Note", "", SaveCurrentNote),
            new MenuItem("_Delete Selected Note", "", DeleteSelectedNote),
            new MenuItem("_Quit", "", () => Application.RequestStop())
        ])
    ]
};

win.Add(listView, textView);

Application.Top.Add(menu, win);

RefreshNoteList();

listView.SelectedItemChanged += (args) =>
{
    var sel = args.Item;
    if (noteFiles.Count == 0 || sel < 0 || sel >= noteFiles.Count) return;

    var selectedFile = Path.Combine(notesFolder!, noteFiles[sel]);

    if (File.Exists(selectedFile))
    {
        textView.Text = File.ReadAllText(selectedFile);
        currentFilePath = selectedFile;
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

    var files = Directory.GetFiles(notesFolder!, "*.md")
        .Select(Path.GetFileName)
        .OrderByDescending(f => f)
        .ToList();

    // Update backing collection
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
        var path = (textField.Text ?? ustring.Empty).ToString().Trim();
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

void CreateNewNote()
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

    ok.Clicked += () =>
    {
        var optional = (nameField.Text ?? ustring.Empty).ToString().Trim();
        var date = DateTime.Today.ToString("yyyy-MM-dd");
        var slug = string.IsNullOrWhiteSpace(optional) ? "" : $"-{optional}";
        var filename = $"{date}{slug}.md";
        var fullPath = Path.Combine(notesFolder!, filename);

        int counter = 1;
        while (File.Exists(fullPath))
        {
            filename = $"{date}{slug}-{counter++}.md";
            fullPath = Path.Combine(notesFolder!, filename);
        }

        File.WriteAllText(fullPath, "");
        RefreshNoteList();

        var index = noteFiles.IndexOf(filename);
        if (index >= 0)
        {
            listView.SelectedItem = index;
            textView.Text = "";
            currentFilePath = fullPath;
            textView.SetFocus();
        }

        Application.RequestStop();
    };

    nameDialog.Add(label, nameField);
    nameDialog.AddButton(ok);

    Application.Run(nameDialog);
}

void SaveCurrentNote()
{
    if (string.IsNullOrEmpty(currentFilePath) || string.IsNullOrEmpty(notesFolder))
    {
        MessageBox.ErrorQuery("Error", "No note open to save.", "OK");
        return;
    }

    try
    {
        File.WriteAllText(currentFilePath, textView.Text!.ToString());
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
    if (res == 0)
    {
        try
        {
            File.Delete(currentFilePath);
            RefreshNoteList();
            textView.Text = "";
            currentFilePath = null;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", ex.Message, "OK");
        }
    }
}

Application.Run();
Application.Shutdown();
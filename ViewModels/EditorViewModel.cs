using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using CrosshairApp.Models;
using CrosshairApp.Services;

namespace CrosshairApp.ViewModels;
public sealed class RelayCommand : ICommand
{
    private readonly Action action;
    public RelayCommand(Action action) => this.action = action;
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => action();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
public sealed class EditorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<Profile>? SelectionChanged;
    public event Action? PreferencesChanged;
    public ObservableCollection<Profile> Profiles { get; }
    public ObservableCollection<Profile> Presets { get; } = new(Storage.Presets());
    public Settings Settings { get; }
    public EditHistory History { get; }
    private readonly bool persist;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private Profile selected;
    private string status = "";
    public string Status { get => status; set { status = value; PropertyChanged?.Invoke(this, new(nameof(Status))); } }
    public Profile Selected
    {
        get => selected;
        set
        {
            if (value == null || ReferenceEquals(value, selected)) return;
            selected.PropertyChanged -= Changed; selected = value; selected.PropertyChanged += Changed; History.Select(value);
            PropertyChanged?.Invoke(this, new(nameof(Selected))); SelectionChanged?.Invoke(value); ScheduleSave();
        }
    }
    public ICommand SaveCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }
    public EditorViewModel(Settings settings, bool persist = true)
    {
        this.persist = persist; Settings = settings; Profiles = new(settings.Profiles);
        selected = Profiles.Count == 0 ? Presets[0].Clone() : Profiles[Math.Clamp(settings.SelectedIndex, 0, Profiles.Count - 1)];
        History = new EditHistory(selected); selected.PropertyChanged += Changed;
        Status = Appearance.Text("Ready");
        SaveCommand = new RelayCommand(Save);
        DuplicateCommand = new RelayCommand(() => { var p = Selected.Clone(); p.Name = p.Name.Length > 74 ? p.Name[..74] + " (2)" : p.Name + " (2)"; Add(p); });
        DeleteCommand = new RelayCommand(() => { var old = Selected; Profiles.Remove(old); Selected = Profiles.FirstOrDefault() ?? Presets[0].Clone(); ScheduleSave(); });
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Persist(); };
    }
    private void Changed(object? s, PropertyChangedEventArgs e) => ScheduleSave();
    public void ScheduleSave() { if (!persist) return; saveTimer.Stop(); saveTimer.Start(); }
    public void NotifyPreferences() { ScheduleSave(); PreferencesChanged?.Invoke(); }
    public void Add(Profile p)
    {
        if (Profiles.Count >= 100) { Status = Appearance.Text("ProfileLimit"); return; }
        Profiles.Add(p); Selected = p; ScheduleSave();
    }
    public void ApplyPreset(Profile p) => Selected = p.Clone();
    public void Next(int delta) { if (Profiles.Count == 0) return; int i = Math.Max(0, Profiles.IndexOf(Selected)); Selected = Profiles[(i + delta + Profiles.Count) % Profiles.Count]; }
    public void Save()
    {
        try { Selected.Validate(); if (!Profiles.Contains(Selected)) { if (Profiles.Count >= 100) throw new InvalidOperationException(Appearance.Text("ProfileLimit")); Profiles.Add(Selected); } Persist(); }
        catch (Exception ex) { Status = Appearance.Text("NotSaved") + ex.Message; }
    }
    private void Persist()
    {
        try
        {
            foreach (var p in Profiles) p.Validate();
            Settings.Profiles = Profiles.ToList(); Settings.SelectedIndex = Math.Max(0, Profiles.IndexOf(Selected));
            if (persist) Storage.Save(Settings); Status = Appearance.Text(Profiles.Contains(Selected) ? "Saved" : "Draft");
        }
        catch (Exception ex) { Status = Appearance.Text("NotSaved") + ex.Message; }
    }
    public void Stop() { saveTimer.Stop(); Persist(); History.Dispose(); }
}


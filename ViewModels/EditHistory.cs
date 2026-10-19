using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CrosshairApp.Models;

namespace CrosshairApp.ViewModels;

// Mouse gestures become one undo entry. Snapshots never replace the bound Profile instance.
public sealed class EditHistory : IDisposable
{
    private Profile profile, last;
    private readonly List<Profile> undo = new(), redo = new();
    private int depth;
    private bool restoring;
    private Profile? transaction;
    public Profile Baseline { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public event Action? Changed;
    public EditHistory(Profile p) { profile = p; last = p.Clone(); Baseline = p.Clone(); p.PropertyChanged += OnChanged; }
    public void Select(Profile p) { profile.PropertyChanged -= OnChanged; profile = p; last = p.Clone(); Baseline = p.Clone(); undo.Clear(); redo.Clear(); depth = 0; transaction = null; p.PropertyChanged += OnChanged; Changed?.Invoke(); }
    public void Begin() { if (depth++ == 0) transaction = profile.Clone(); }
    public void End()
    {
        if (depth == 0 || --depth != 0) return;
        if (transaction != null && Different(transaction, profile)) Push(transaction);
        transaction = null; last = profile.Clone(); Changed?.Invoke();
    }
    private static bool Different(Profile a, Profile b) => JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b);
    private void Push(Profile p) { undo.Add(p); if (undo.Count > 100) undo.RemoveAt(0); redo.Clear(); }
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (restoring) return;
        if (depth == 0) Push(last);
        last = profile.Clone(); Changed?.Invoke();
    }
    public void Undo() { while (depth > 0) End(); if (!CanUndo) return; redo.Add(profile.Clone()); Restore(undo[^1]); undo.RemoveAt(undo.Count - 1); Changed?.Invoke(); }
    public void Redo() { if (!CanRedo) return; undo.Add(profile.Clone()); Restore(redo[^1]); redo.RemoveAt(redo.Count - 1); Changed?.Invoke(); }
    private void Restore(Profile p)
    {
        restoring = true;
        try { foreach (var property in typeof(Profile).GetProperties().Where(x => x.CanWrite)) property.SetValue(profile, property.GetValue(p)); last = profile.Clone(); }
        finally { restoring = false; }
    }
    public void Dispose() => profile.PropertyChanged -= OnChanged;
}

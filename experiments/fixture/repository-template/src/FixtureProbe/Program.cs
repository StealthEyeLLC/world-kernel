var statePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixture", "state.txt"));
Console.WriteLine(File.Exists(statePath) ? File.ReadAllText(statePath).Trim() : "fixture-state-unavailable");


import os
import subprocess

def run(cmd):
    subprocess.run(cmd, shell=True, check=True)

run("git branch backup-commits")
run("git checkout -b split-commits a3520a3")

# Modify ShipWatcher.NET.csproj
with open("ShipWatcher.NET/ShipWatcher.NET.csproj", "r") as f:
    csproj = f.read()
csproj = csproj.replace('<ItemGroup>', '<ItemGroup>\n<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.7" />')
with open("ShipWatcher.NET/ShipWatcher.NET.csproj", "w") as f:
    f.write(csproj)

# Modify Program.cs
program = """using Microsoft.Extensions.DependencyInjection;
using ShipWatcher.NET;
using ShipWatcher.NET.Sources;
using ShipWatcher.NET.Views;

var apiKey = Environment.GetEnvironmentVariable("BMA_API_KEY") ?? "";

var latMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MIN"), out var val) ? val : 50.0;
var latMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MAX"), out var val2) ? val2 : 75.0;
var lonMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MIN"), out var val3) ? val3 : -10.0;
var lonMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MAX"), out var val4) ? val4 : 35.0;

double[][][] bbox = [[[latMin, lonMin], [latMax, lonMax]]];

var services = new ServiceCollection();

// --- Data Sources ---
services.AddSingleton<IAisDataSource>(sp => new AisClient(apiKey, bbox));
services.AddSingleton<IAisDataSource, KystverketAisClient>();

// --- Views ---
services.AddTransient<IShipWatcherView, MapShipView>();
services.AddTransient<IShipWatcherView, TableShipView>();

// --- App Shell ---
int defaultSource = string.IsNullOrWhiteSpace(apiKey) ? 1 : 0;
services.AddSingleton(sp => new AppShell(sp, defaultSource));

var serviceProvider = services.BuildServiceProvider();

var app = serviceProvider.GetRequiredService<AppShell>();
app.Run();

return 0;
"""
with open("ShipWatcher.NET/Program.cs", "w") as f:
    f.write(program)

# Modify AppShell.cs
with open("ShipWatcher.NET/AppShell.cs", "r") as f:
    appshell = f.read()

appshell = appshell.replace('using Terminal.Gui;', 'using Microsoft.Extensions.DependencyInjection;\nusing Terminal.Gui;')
appshell = appshell.replace('private readonly List<Func<IAisDataSource>> _sourceFactories;\n    private readonly List<Func<IShipWatcherView>> _viewFactories;', 'private readonly IServiceProvider _serviceProvider;')
appshell = appshell.replace('private List<IShipWatcherView> _views = [];', 'private List<IShipWatcherView> _views = [];\n    private List<IAisDataSource> _sources = [];')
appshell = appshell.replace('''    public AppShell(
        List<Func<IAisDataSource>> sourceFactories,
        List<Func<IShipWatcherView>> viewFactories,
        int defaultSourceIndex = 0)
    {
        _sourceFactories = sourceFactories;
        _viewFactories = viewFactories;
        _currentSourceIndex = defaultSourceIndex;
    }''', '''    public AppShell(
        IServiceProvider serviceProvider,
        int defaultSourceIndex = 0)
    {
        _serviceProvider = serviceProvider;
        _currentSourceIndex = defaultSourceIndex;
    }''')
appshell = appshell.replace('_activeSource = _sourceFactories[_currentSourceIndex]();', '_sources = _serviceProvider.GetServices<IAisDataSource>().ToList();\n        _activeSource = _sources[_currentSourceIndex];')
appshell = appshell.replace('_views = _viewFactories.Select(f => f()).ToList();', '_views = _serviceProvider.GetServices<IShipWatcherView>().ToList();')

show_source_old = '''        // Build source descriptors from factories (create temp instances to read metadata)
        var descriptors = new List<ISourceDescriptor>();
        var tempSources = new List<IAisDataSource>();

        for (int i = 0; i < _sourceFactories.Count; i++)
        {
            if (i == _currentSourceIndex && _activeSource is ISourceDescriptor activeDesc)
            {
                descriptors.Add(activeDesc);
            }
            else
            {
                var temp = _sourceFactories[i]();
                tempSources.Add(temp);
                descriptors.Add((ISourceDescriptor)temp);
            }
        }'''
show_source_new = '''        // Use the already resolved sources
        var descriptors = _sources.OfType<ISourceDescriptor>().ToList();'''
appshell = appshell.replace(show_source_old, show_source_new)

old_reconnect_1 = '''                _activeSource?.Dispose();
                _currentSourceIndex = selected;
                _activeSource = _sourceFactories[_currentSourceIndex]();

                // Apply config to the fresh instance
                if (_activeSource is ISourceDescriptor newDesc)
                    newDesc.ApplyConfig(configValues);

                _ = Task.Run(async () => await _activeSource.ConnectAsync(_cts.Token));'''
new_reconnect_1 = '''                _currentSourceIndex = selected;
                _activeSource = _sources[_currentSourceIndex];
                if (_activeSource != null)
                {
                    _ = Task.Run(async () => await _activeSource.ConnectAsync(_cts.Token));
                }'''
appshell = appshell.replace(old_reconnect_1, new_reconnect_1)

old_reconnect_2 = '''                _activeSource?.Dispose();
                _activeSource = _sourceFactories[_currentSourceIndex]();

                if (_activeSource is ISourceDescriptor newDesc)
                    newDesc.ApplyConfig(configValues);

                _ = Task.Run(async () => await _activeSource.ConnectAsync(_cts.Token));'''
new_reconnect_2 = '''                if (_activeSource != null)
                {
                    _ = Task.Run(async () => await _activeSource.ConnectAsync(_cts.Token));
                }'''
appshell = appshell.replace(old_reconnect_2, new_reconnect_2)

old_dispose = '''        // Dispose temp instances
        foreach (var temp in tempSources)
            temp.Dispose();'''
appshell = appshell.replace(old_dispose, '')

with open("ShipWatcher.NET/AppShell.cs", "w") as f:
    f.write(appshell)

run("git add .")
run("git commit -m 'chore(deps): introduce Microsoft.Extensions.DependencyInjection'")

# --- Commit 2: State Management ---
run("git checkout backup-commits~1 -- .")
run("git add .")
run("git commit -m 'refactor(state): use VesselStore as single source of truth'")

# --- Commit 3: Map Focus Fix ---
with open("ShipWatcher.NET/Views/MapShipView.cs", "r") as f:
    map_view = f.read()
map_view = map_view.replace('Visible = true,\n        };', 'Visible = true,\n            CanFocus = true,\n        };')
with open("ShipWatcher.NET/Views/MapShipView.cs", "w") as f:
    f.write(map_view)
run("git add ShipWatcher.NET/Views/MapShipView.cs")
run("git commit -m 'fix(map): restore map view focus behavior'")

# --- Commit 4: C# 12 Refactor ---
run("git checkout backup-commits -- .")
run("git restore --staged .")
run("git add ShipWatcher.NET")
run("git commit -m 'refactor: upgrade to C# 12 primary constructors and records'")

# --- Commit 5: Tooling ---
run("git add .gemini")
run("git commit -m 'chore: add gemini workspace settings'")

# Reset main to split-commits
run("git checkout main")
run("git reset --hard split-commits")
run("git branch -d split-commits")

print("Done splitting commits.")

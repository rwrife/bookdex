using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Bookdex.App.Services;
using Bookdex.App.ViewModels;
using Bookdex.App.Views;
using Bookdex.Core.Catalog;

namespace Bookdex.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = Path.Combine(GetDataRoot(), "catalog.db");
            var store = new SqliteLibraryStore(databasePath);
            store.Initialize();

            var searchService = new LibraryStoreSearchService(store);
            var scanService = new LibraryScanService(store);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new LibraryShellViewModel(searchService, scanService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string GetDataRoot()
    {
        string basePath;
        if (OperatingSystem.IsWindows())
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(basePath, "bookdex");
        }

        if (OperatingSystem.IsMacOS())
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(basePath, "Library", "Application Support", "bookdex");
        }

        basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "bookdex");
    }
}

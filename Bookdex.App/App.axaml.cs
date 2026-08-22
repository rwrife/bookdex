using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Bookdex.App.Services;
using Bookdex.App.ViewModels;
using Bookdex.App.Views;
using Bookdex.Core.Catalog;
using Bookdex.Core.Storage;

namespace Bookdex.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataRoot = BookdexDataDirectory.CreateDefault();
            var databasePath = Path.Combine(dataRoot, "catalog.db");
            var store = new SqliteLibraryStore(databasePath);
            store.Initialize();

            var searchService = new LibraryStoreSearchService(store);
            var scanService = new LibraryScanService(store);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new LibraryShellViewModel(searchService, scanService, store),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

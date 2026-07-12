using PS.APP;
using PS.APP.Localization;
using PS.APP.Settings;
using PS_SZC;
using PS_SZC.Startup;

var localizer = FileLocalizer.CreateMerged();
var settings = new SettingsManager(Path.Combine(AppContext.BaseDirectory, "settings.json"));
var startupProjectPath = StartupProjectPath.TryParse(args);
var app = new Application(localizer, settings);
app.Execute(new SchoolPaymentsForm(startupProjectPath));

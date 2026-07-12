using PS.APP;
using PS.APP.Localization;
using PS.APP.Settings;
using PS_SZC;

var localizer = FileLocalizer.CreateMerged();
var settings = new SettingsManager(Path.Combine(AppContext.BaseDirectory, "settings.json"));
var app = new Application(localizer, settings);
app.Execute(new SchoolPaymentsForm());

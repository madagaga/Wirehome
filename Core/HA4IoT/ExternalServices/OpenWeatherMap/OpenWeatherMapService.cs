using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using HA4IoT.Contracts.Api;
using HA4IoT.Contracts.Core;
using HA4IoT.Contracts.Environment;
using HA4IoT.Contracts.Logging;
using HA4IoT.Contracts.Scheduling;
using HA4IoT.Contracts.Services;
using HA4IoT.Contracts.Settings;
using HA4IoT.Contracts.Storage;
using Newtonsoft.Json.Linq;

namespace HA4IoT.ExternalServices.OpenWeatherMap
{
    [ApiServiceClass(typeof(OpenWeatherMapService))]
    public class OpenWeatherMapService : ServiceBase
    {
        private readonly IOutdoorService _outdoorService;
        private readonly IDaylightService _daylightService;
        private readonly IDateTimeService _dateTimeService;
        private readonly ISystemInformationService _systemInformationService;
        private readonly ILogger _log;

        public float Temperature { get; private set; }
        public float Humidity { get; private set; }
        public TimeSpan Sunrise { get; private set; }
        public TimeSpan Sunset { get; private set; }
        public WeatherCondition Condition { get; private set; }

        public OpenWeatherMapService(
            IOutdoorService outdoorService,
            IDaylightService daylightService,
            IDateTimeService dateTimeService,
            ISchedulerService schedulerService,
            ISystemInformationService systemInformationService,
            ISettingsService settingsService,
            IStorageService storageService,
            ILogService logService)
        {
            if (schedulerService == null) throw new ArgumentNullException(nameof(schedulerService));
            if (settingsService == null) throw new ArgumentNullException(nameof(settingsService));
            _outdoorService = outdoorService ?? throw new ArgumentNullException(nameof(outdoorService));
            _daylightService = daylightService ?? throw new ArgumentNullException(nameof(daylightService));
            _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
            _systemInformationService = systemInformationService ?? throw new ArgumentNullException(nameof(systemInformationService));

            _log = logService?.CreatePublisher(nameof(OpenWeatherMapService)) ?? throw new ArgumentNullException(nameof(logService));

            settingsService.CreateSettingsMonitor<OpenWeatherMapServiceSettings>(s => Settings = s.NewSettings);

            schedulerService.Register("OpenWeatherMapServiceUpdater", TimeSpan.FromMinutes(5), RefreshAsync);
        }

        public OpenWeatherMapServiceSettings Settings { get; private set; }

        [ApiMethod]
        public void Status(IApiCall apiCall)
        {
            apiCall.Result = JObject.FromObject(this);
        }

        [ApiMethod]
        public void Refresh(IApiCall apiCall)
        {
            RefreshAsync().Wait();
        }

        private async Task RefreshAsync()
        {
            if (!Settings.IsEnabled)
            {
                _log.Verbose("OpenWeatherMapService is disabled.");
                return;
            }

            _log.Verbose("Fetching OpenWeatherMap data.");

            var response = await FetchWeatherDataAsync();
            if (TryParseWeatherData(response))
            {
                PushData();
            }

            _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/Timestamp", _dateTimeService.Now);
        }

        private void PushData()
        {
            if (Settings.UseTemperature)
            {
                _outdoorService.UpdateTemperature(Temperature);
            }

            if (Settings.UseHumidity)
            {
                _outdoorService.UpdateHumidity(Humidity);
            }

            if (Settings.UseSunriseSunset)
            {
                _daylightService.Update(Sunrise, Sunset);
            }

            if (Settings.UseWeather)
            {
                _outdoorService.UpdateCondition(Condition);
            }
        }

        private async Task<string> FetchWeatherDataAsync()
        {
            var uri = $"http://api.openweathermap.org/data/2.5/weather?lat={Settings.Latitude}&lon={Settings.Longitude}&APPID={Settings.AppId}&units=metric";
            _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/Uri", uri);

            string response = null;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var rootFilter = new HttpBaseProtocolFilter();
                rootFilter.CacheControl.ReadBehavior = HttpCacheReadBehavior.NoCache;
                rootFilter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;

                using (var httpClient = new HttpClient(rootFilter))
                {
                    httpClient.DefaultRequestHeaders.CacheControl.MaxAge = TimeSpan.Zero;
                    using (var result = await httpClient.GetAsync(new Uri(uri)))
                    {
                        response = await result.Content.ReadAsStringAsync();
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastFetchDuration", stopwatch.ElapsedMilliseconds);
                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastResponse", response);
            }

            return response;
        }

        private bool TryParseWeatherData(string source)
        {
            try
            {
                var parser = new OpenWeatherMapResponseParser();
                parser.Parse(source);

                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastConditionCode", parser.ConditionCode);
                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastCondition", parser.Condition.ToString);
                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastTemperature", parser.Temperature);
                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastHumidity", parser.Humidity);
                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastSunrise", parser.Sunrise);
                _systemInformationService.Set($"{nameof(OpenWeatherMapService)}/LastSunset", parser.Sunset);

                Condition = parser.Condition;
                Temperature = parser.Temperature;
                Humidity = parser.Humidity;
                Sunrise = parser.Sunrise;
                Sunset = parser.Sunset;

                return true;
            }
            catch (Exception exception)
            {
                _log.Warning(exception, $"Error while parsing Open Weather Map response ({source}).");
                return false;
            }
        }
    }
}
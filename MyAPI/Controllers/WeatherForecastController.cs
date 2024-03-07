using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace MyAPI.Controllers
{
    public class WeatherData
    {
        public double TemperatureCelsius { get; set; }
        public double TemperatureFahrenheit => 32 + (int)(TemperatureCelsius / 0.5566);
        public string? Cloudiness { get; set; }
        public string? Humidity { get; set; }
        public string? Precipitation { get; set; }
        public string? WindDirection { get; set; }
        public string? WindSpeed { get; set; }

        public void ReplaceNullValues()
        {
            Cloudiness      ??= "Данных нет";
            Humidity        ??= "Данных нет";
            Precipitation   ??= "Данных нет";
            WindDirection   ??= "Данных нет";
            WindSpeed       ??= "Данных нет";
        }
    }

    public abstract class BaseSPBWeatherService
    {
        public string _ApiKey { get; private set; }
        public const string latitude = "59.9343";
        public const string longitude = "30.3351";
        protected RestClient _client;
        public abstract WeatherData GetWeatherData();

        protected BaseSPBWeatherService(string apiKey)
        {
            _ApiKey = apiKey;
        }
    }

    public class TommorowioSPBWeatherService : BaseSPBWeatherService
    {
        public TommorowioSPBWeatherService(string apiKey) : base(apiKey) { 
            _client = new RestClient("https://api.tomorrow.io/v4/");
        }

        override public WeatherData GetWeatherData()
        {
            WeatherData weatherData = new WeatherData();

            var request = new RestRequest("weather/forecast", Method.Get);
            request.AddQueryParameter("apikey", _ApiKey);
            request.AddQueryParameter("location", $"{latitude},{longitude}");
            request.AddQueryParameter("fields", "temperature,cloudCover,humidity,precipitationProbability,windSpeed,windDirection");

            var response = _client.Execute(request);
            if (response.IsSuccessful)
            {
                JsonDocument doc = JsonDocument.Parse(response.Content);
                JsonElement root = doc.RootElement;
                JsonElement minutelyWeather = root.GetProperty("timelines").GetProperty("minutely");
                JsonElement currentWeather = minutelyWeather[0];

                int minutesNumber = minutelyWeather.GetArrayLength();
                for (int i = 0; i < minutesNumber; i++)
                {
                    JsonElement minuteWeather = minutelyWeather[i];

                    var utcTime = minuteWeather.GetProperty("time").GetRawText();
                    var minute = Int32.Parse(utcTime.Substring(15, 2));
                    if (minute == DateTime.UtcNow.Minute)
                    {
                        currentWeather = minuteWeather;
                        break;
                    }
                }

                currentWeather = currentWeather.GetProperty("values");

                weatherData.TemperatureCelsius = currentWeather.GetProperty("temperature").GetDouble();
                weatherData.Cloudiness = currentWeather.GetProperty("cloudCover").GetRawText();
                weatherData.Humidity = currentWeather.GetProperty("humidity").GetRawText();
                weatherData.Precipitation = currentWeather.GetProperty("precipitationProbability").GetRawText();
                weatherData.WindSpeed = currentWeather.GetProperty("windSpeed").GetRawText();
                weatherData.WindDirection = currentWeather.GetProperty("windDirection").GetRawText();
            }
            else
            {
                throw new Exception($"Failed to retrieve weather data: {response.ErrorMessage}");
            }

            weatherData.ReplaceNullValues();
            return weatherData;
        }
    }

    public class StormglassSPBWeatherService : BaseSPBWeatherService
    {
        public StormglassSPBWeatherService(string apiKey) : base(apiKey) {
            _client = new RestClient("https://api.stormglass.io/v2/");
        }

        override public WeatherData GetWeatherData()
        {
            WeatherData weatherData = new WeatherData();

            var request = new RestRequest("weather/point", Method.Get);
            request.AddHeader("Authorization", _ApiKey);
            request.AddQueryParameter("lat", $"{latitude}");
            request.AddQueryParameter("lng", $"{longitude}");
            request.AddQueryParameter("params", "airTemperature,cloudCover,humidity,gust,windWaveDirection");

            var response = _client.Execute(request);
            if (response.IsSuccessful)
            {
                JsonDocument doc = JsonDocument.Parse(response.Content);
                JsonElement root = doc.RootElement;
                JsonElement currentWeather = root.GetProperty("hours")[0]; 

                int hoursNumber = root.GetProperty("hours").GetArrayLength();
                for (int i = 0; i < hoursNumber; i++)
                {
                    JsonElement hourWeather = root.GetProperty("hours")[i];

                    var utcTime = hourWeather.GetProperty("time").GetRawText();
                    var hour = Int32.Parse(utcTime.Substring(12, 2));
                    if (hour == DateTime.UtcNow.Hour)
                    {
                        currentWeather = hourWeather;
                        break;
                    }
                }

                weatherData.TemperatureCelsius = currentWeather.GetProperty("airTemperature").GetProperty("noaa").GetDouble();
                weatherData.Cloudiness = currentWeather.GetProperty("cloudCover").GetProperty("noaa").GetRawText();
                weatherData.Humidity = currentWeather.GetProperty("humidity").GetProperty("noaa").GetRawText();
                weatherData.WindSpeed = currentWeather.GetProperty("windWaveDirection").GetProperty("dwd").GetRawText();
                weatherData.WindDirection = currentWeather.GetProperty("gust").GetProperty("noaa").GetRawText();
            }
            else
            {
                throw new Exception($"Failed to retrieve weather data: {response.ErrorMessage}");
            }

            weatherData.ReplaceNullValues();
            return weatherData;
        }
    }

    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly TommorowioSPBWeatherService _serviceTommorowio;
        private readonly StormglassSPBWeatherService _serviceStormglass;

        public WeatherForecastController()
        {
            _serviceTommorowio = new TommorowioSPBWeatherService(Environment.GetEnvironmentVariable("APIKEY_TOMORROWIO"));
            _serviceStormglass = new StormglassSPBWeatherService(Environment.GetEnvironmentVariable("APIKEY_STORMGLASS"));
        }

        private WeatherData GetWeatherDataFromTommorowio()
        {
            try
            {
                WeatherData weatherData = _serviceTommorowio.GetWeatherData();
                return weatherData;
            }
            catch (Exception ex)
            {
                throw new Exception($"Tommorowio error: {ex.Message}");
            }
        }

        private WeatherData GetWeatherDataFromStormglass()
        {
            try
            {
                WeatherData weatherData = _serviceStormglass.GetWeatherData();
                return weatherData;
            }
            catch (Exception ex)
            {
                throw new Exception($"Stormglass error: {ex.Message}");
            }
        }

        private Dictionary<string, WeatherData> GetAggregatedWeatherData()
        {
            Dictionary<string, WeatherData> dict = new Dictionary<string, WeatherData>();
            dict["tomorrowio"] = GetWeatherDataFromTommorowio();
            dict["stormglass"] = GetWeatherDataFromStormglass();
            return dict;
        }

        [HttpGet("weather")]
        public IActionResult GetWeather()
        {
            try
            {
                var aggWeatherData = GetAggregatedWeatherData();
                return Ok(aggWeatherData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
         
    }
}

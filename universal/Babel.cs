//#define DEBUG

using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Babel", "Wulf/lukespragg", "1.0.2", ResourceId = 1963)]
    [Description("Plugin API for translating messages using free or paid translation services")]
    public class Babel : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "API key (if required)")]
            public string ApiKey;

            [JsonProperty(PropertyName = "Translation service")]
            public string Service;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    ApiKey = "",
                    Service = "google"
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.Service == null) LoadDefaultConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Initialization

        private static readonly Regex googleRegex = new Regex(@"\[\[\[""((?:\s|.)+?)"",""(?:\s|.)+?""");
        private static readonly Regex microsoftRegex = new Regex("\"(.*)\"");

        private void Init()
        {
            if (string.IsNullOrEmpty(config.ApiKey) && config.Service.ToLower() != "google")
                LogWarning("Invalid API key, please check that it is set and valid");
        }

        #endregion

        #region Translation API

        /// <summary>
        /// Translates text from one language to another language
        /// </summary>
        /// <param name="text"></param>
        /// <param name="to"></param>
        /// <param name="from"></param>
        /// <param name="callback"></param>
        private void Translate(string text, string to, string from = "auto", Action<string> callback = null)
        {
            var apiKey = config.ApiKey;
            var service = config.Service.ToLower();

            if (string.IsNullOrEmpty(config.ApiKey) && service != "google")
            {
                LogWarning("Invalid API key, please check that it is set and valid");
                return;
            }

            switch (service)
            {
                case "google":
                    {
                        // Reference: https://cloud.google.com/translate/v2/quickstart
                        var url = string.IsNullOrEmpty(apiKey) 
                            ? $"https://translate.googleapis.com/translate_a/single?client=gtx&tl={to}&sl={from}&dt=t&q={Uri.EscapeUriString(text)}"
                            : $"https://www.googleapis.com/language/translate/v2?key={apiKey}&target={to}&source={from}&q={Uri.EscapeUriString(text)}";
                        webrequest.EnqueuePost(url, null, (code, response) =>
                        {
                            if (code != 200 || response == null || response.Equals("[null,null,\"\"]"))
                            {
                                LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                return;
                            }

                            Callback(code, response, text, callback);
                        }, this);
                        break;
                    }

                case "bing":
                case "microsoft":
                    {
                        // Reference: https://www.microsoft.com/en-us/translator/getstarted.aspx
                        // Supported language codes: https://msdn.microsoft.com/en-us/library/hh456380.aspx
                        // TODO: Implement the new access token method for Bing/Microsoft
                        webrequest.EnqueueGet($"http://api.microsofttranslator.com/V2/Ajax.svc/Detect?appId={apiKey}&text={Uri.EscapeUriString(text)}", (c, r) =>
                        {
                            if (r == null || r.Contains("<html>"))
                            {
                                LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                return;
                            }

                            if (r.Contains("ArgumentException: Invalid appId"))
                            {
                                LogWarning("Invalid API key, please check that it is valid and try again");
                                return;
                            }

                            if (r.Contains("ArgumentOutOfRangeException: 'to' must be a valid language"))
                            {
                                LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                return;
                            }

                            var url = $"http://api.microsofttranslator.com/V2/Ajax.svc/Translate?appId={apiKey}&to={to}&from={r}&text={Uri.EscapeUriString(text)}";
                            webrequest.EnqueuePost(url, null, (code, response) => 
                            {
                                if (r == null || r.Contains("<html>"))
                                {
                                    LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                    return;
                                }

                                if (r.Contains("ArgumentOutOfRangeException: 'from' must be a valid language"))
                                {
                                    LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                    return;
                                }

                                Callback(code, response, text, callback);
                            }, this);
                        }, this);
                        break;
                    }

                case "yandex":
                    {
                        // Reference: https://tech.yandex.com/keys/get/?service=trnsl
                        webrequest.EnqueueGet($"https://translate.yandex.net/api/v1.5/tr.json/detect?key={apiKey}&hint={from}&text={Uri.EscapeUriString(text)}", (c, r) =>
                        {
                            if (r == null)
	                        {
                                LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                return;
	                        }

                            if (c == 502 || r.Contains("Invalid parameter: hint"))
                            {
                                LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                return;
                            }

                            from = (string)JObject.Parse(r).GetValue("lang");
                            var url = $"https://translate.yandex.net/api/v1.5/tr.json/translate?key={apiKey}&lang={from}-{to}&text={Uri.EscapeUriString(text)}";
                            webrequest.EnqueuePost(url, null, (code, response) =>
                            {
	                            if (c == 501 || c == 502 || r.Contains("The specified translation direction is not supported") || r.Contains("Invalid parameter: lang"))
	                            {
                                    LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
	                                return;
	                            }

                                Callback(code, response, text, callback);
	                        }, this);
                        }, this);
                        break;
                    }

                default:
                    LogWarning($"Translation service '{service}' is not a valid setting");
                    break;
            }
        }

        private void Callback(int code, string response, string text, Action<string> callback = null)
        {
            if (code != 200 || response == null)
            {
                LogWarning($"Translation failed! {config.Service.Humanize()} responded with: {response} ({code})");
                return;
            }

            string translated = null;
            var service = config.Service.ToLower();
            if (service == "google" && string.IsNullOrEmpty(config.ApiKey))
                translated = googleRegex.Match(response).Groups[1].ToString();
            else if (service == "google" && !string.IsNullOrEmpty(config.ApiKey))
                translated = (string)JObject.Parse(response)["data"]["translations"]["translatedText"];
            else if (service == "microsoft" || service.ToLower() == "bing")
                translated = microsoftRegex.Match(response).Groups[1].ToString();
            else if (service == "yandex")
                translated = (string)JObject.Parse(response).GetValue("text").First;
#if DEBUG
            LogWarning($"Original: {text}");
            LogWarning($"Translated: {translated}");
            if (translated == text) LogWarning("Translated text is the same as original text");
#endif
            callback?.Invoke(string.IsNullOrEmpty(translated) ? text : Regex.Unescape(translated));
        }

        #endregion
    }
}

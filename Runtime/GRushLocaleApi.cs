using System;
using System.Threading.Tasks;

namespace GRushSdk
{
    public sealed class GRushLocale
    {
        public string Locale;
        public string Source;
        public string[] Languages;
    }

    public interface IGRushLocaleBackend
    {
        string LocaleCurrentJson();
        void SetLocaleChangedHandler(Action<string> handler);
    }

    [Serializable]
    internal class GRushLocaleWire
    {
        public string locale;
        public string source;
        public string[] languages;
    }

    public sealed class GRushLocaleApi
    {
        private Action<GRushLocale> changed;
        private bool subscribed;

        public event Action<GRushLocale> Changed
        {
            add
            {
                changed += value;
                Subscribe();
            }
            remove { changed -= value; }
        }

        public GRushLocale Current
        {
            get
            {
                var backend = GRush.Backend as IGRushLocaleBackend;
                if (!GRush.IsLocaleAvailable || backend == null)
                {
                    return null;
                }
                return Parse(backend.LocaleCurrentJson());
            }
        }

        public async Task<GRushResult<GRushLocale>> GetAsync()
        {
            if (!GRush.IsLocaleAvailable)
            {
                return GRushResult<GRushLocale>.Unsupported();
            }
            var response = await GRush.CallAsync("locale.get", null);
            if (!response.Ok)
            {
                return GRushResult<GRushLocale>.Failure(response.Code, response.Message);
            }
            var locale = Parse(response.Value);
            if (locale == null)
            {
                return GRushResult<GRushLocale>.Failure(
                    GRushErrorCode.Internal,
                    "GameRush returned an unreadable locale."
                );
            }
            return GRushResult<GRushLocale>.Success(locale);
        }

        internal static GRushLocale Parse(string json)
        {
            var wire = GRushWire.Parse<GRushLocaleWire>(json);
            if (wire == null || string.IsNullOrEmpty(wire.locale))
            {
                return null;
            }
            return new GRushLocale
            {
                Locale = wire.locale,
                Source = wire.source ?? string.Empty,
                Languages = wire.languages ?? new string[0],
            };
        }

        private void Subscribe()
        {
            var backend = GRush.Backend as IGRushLocaleBackend;
            if (subscribed || !GRush.IsLocaleAvailable || backend == null)
            {
                return;
            }
            subscribed = true;
            backend.SetLocaleChangedHandler(OnChanged);
        }

        private void OnChanged(string json)
        {
            var locale = Parse(json);
            var handler = changed;
            if (locale != null && handler != null)
            {
                handler(locale);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GRushSdk
{
    /// <summary>
    /// 起動パラメータ。GameRush の URL に <c>gp.</c> を付けて載せたキーと値
    /// （<c>?gp.item=sword</c> なら <c>item</c> が <c>sword</c>）。
    /// キーは英数字と <c>_-</c> で 32 文字まで（<c>__proto__</c> は使えない）、値は英数字と <c>._~:,-</c> で 128 文字まで、8 個まで。
    /// 形の合わないものは GameRush が落とす。
    ///
    /// 値は誰でも作れるリンクから来る。存在するか・持っているか・解放済みかをゲームの側で確かめてから使い、
    /// 確かめられなければ無視する。
    /// </summary>
    public sealed class GRushLaunch
    {
        private static readonly IReadOnlyDictionary<string, string> Empty =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        internal GRushLaunch(IDictionary<string, string> source)
        {
            Params = source == null || source.Count == 0
                ? Empty
                : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(source));
        }

        /// <summary>起動パラメータの全部。パラメータの無い起動では空。</summary>
        public IReadOnlyDictionary<string, string> Params { get; private set; }

        /// <summary><paramref name="key"/>（<c>gp.</c> を除いた名前）の値を読む。無ければ false。</summary>
        public bool TryGet(string key, out string value)
        {
            if (key == null)
            {
                value = null;
                return false;
            }
            return Params.TryGetValue(key, out value);
        }
    }

    public interface IGRushLaunchBackend
    {
        string LaunchCurrentJson();
    }

    // JsonUtility は辞書を読めないので、Web の層とモックは params を entries の配列に直して渡す。
    [Serializable]
    internal class GRushLaunchWire
    {
        public GRushLaunchEntryWire[] entries;
    }

    [Serializable]
    internal class GRushLaunchEntryWire
    {
        public string key;
        public string value;
    }

    /// <summary>
    /// 起動パラメータを読む。渡るのは、プレイヤーが最初に開いたスライドのゲームだけ。
    /// 別のゲームへスワイプして戻ったときは空。フィードではページを読み直すと空になるが、
    /// 単体表示や埋め込みでは読み直すと同じ値がまた渡ることがあるので、何度渡っても困らないように書く。
    /// <see cref="GRush.IsLaunchAvailable"/> が false の古いランタイムと GameRush の外では、
    /// <see cref="GetAsync"/> が Unsupported、<see cref="Current"/> が null。
    /// </summary>
    public sealed class GRushLaunchApi
    {
        /// <summary>届いていれば起動パラメータ、まだなら null。</summary>
        public GRushLaunch Current
        {
            get
            {
                var backend = GRush.Backend as IGRushLaunchBackend;
                if (!GRush.IsLaunchAvailable || backend == null)
                {
                    return null;
                }
                return Parse(backend.LaunchCurrentJson());
            }
        }

        /// <summary>起動パラメータが届くまで待って返す。パラメータの無い起動では Params が空。</summary>
        public async Task<GRushResult<GRushLaunch>> GetAsync()
        {
            if (!GRush.IsLaunchAvailable)
            {
                return GRushResult<GRushLaunch>.Unsupported();
            }
            var response = await GRush.CallAsync("launch.get", null);
            if (!response.Ok)
            {
                return GRushResult<GRushLaunch>.Failure(response.Code, response.Message);
            }
            var launch = Parse(response.Value);
            if (launch == null)
            {
                return GRushResult<GRushLaunch>.Failure(
                    GRushErrorCode.Internal,
                    "GameRush returned unreadable launch parameters."
                );
            }
            return GRushResult<GRushLaunch>.Success(launch);
        }

        internal static GRushLaunch Parse(string json)
        {
            var wire = GRushWire.Parse<GRushLaunchWire>(json);
            if (wire == null || wire.entries == null)
            {
                return null;
            }
            var source = new Dictionary<string, string>();
            foreach (var entry in wire.entries)
            {
                if (entry != null && entry.key != null && entry.value != null)
                {
                    source[entry.key] = entry.value;
                }
            }
            return new GRushLaunch(source);
        }
    }
}

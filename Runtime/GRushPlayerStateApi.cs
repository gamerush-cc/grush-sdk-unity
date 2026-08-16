using System.Threading.Tasks;

namespace GRushSdk
{
    /// <summary>
    /// 公開プレイヤー状態（他プレイヤーへ見せる進捗）。
    ///
    /// **これは他プレイヤーへ見えるユーザー生成コンテンツである。** 通報と
    /// モデレーションの対象になるため、payload は人間が読める構造化 JSON に
    /// 限られる。4KB を超えるものや base64 のような読めない文字列を含むものは
    /// サーバが弾く。
    ///
    /// クラウドセーブ（本人だけが読む非公開データ）とは別の入れ物であり、
    /// この経路からクラウドセーブは一切読めない。
    ///
    /// payload は任意形状の JSON で <c>UnityEngine.JsonUtility</c> では型に
    /// 落とせないため、**応答の生 JSON を <c>RawJson</c> として渡し、パースは
    /// ゲーム側の任意のパーサへ委ねる**（ランキングの metadata と同じ理由）。
    /// </summary>
    public sealed class GRushPlayerStateApi
    {
        /// <summary>自分の状態を読む。運営が伏せていても本人には返る。</summary>
        public async Task<GRushResult<GRushPlayerStatePage>> GetMineAsync()
        {
            if (!GRush.IsPlayerStateAvailable)
            {
                return GRushResult<GRushPlayerStatePage>.Unsupported();
            }
            var response = await GRush.CallAsync("playerState.getMine", null);
            return ToSingle(response);
        }

        public Task<GRushResult<GRushPlayerStatePage>> SetMineAsync(string payloadJson)
        {
            return SetMineAsync(payloadJson, -1);
        }

        /// <summary>
        /// 自分の状態を書く。<paramref name="payloadJson"/> は JSON オブジェクトの
        /// 生文字列（4KB 以下）。<paramref name="baseRevision"/> に 0 以上を渡すと
        /// 楽観ロックになり、他所で更新されていれば <c>InvalidParams</c> で戻る。
        /// </summary>
        public async Task<GRushResult<GRushPlayerStatePage>> SetMineAsync(
            string payloadJson,
            int baseRevision
        )
        {
            if (!GRush.IsPlayerStateAvailable)
            {
                return GRushResult<GRushPlayerStatePage>.Unsupported();
            }
            var response = await GRush.CallAsync(
                "playerState.setMine",
                GRushWire.PlayerStateParams(payloadJson, baseRevision)
            );
            return ToSingle(response);
        }

        /// <summary>
        /// 他プレイヤーの状態を疑似IDで引く（1回 50 件まで）。
        /// **運営が伏せたものは返らないので、要求した数だけ返る前提で書かないこと。**
        /// </summary>
        public async Task<GRushResult<GRushPlayerStatePage>> GetAsync(string[] pseudoIds)
        {
            if (!GRush.IsPlayerStateAvailable)
            {
                return GRushResult<GRushPlayerStatePage>.Unsupported();
            }
            var response = await GRush.CallAsync(
                "playerState.get",
                GRushWire.PlayerStateQuery(pseudoIds)
            );
            if (!response.Ok)
            {
                return GRushResult<GRushPlayerStatePage>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushPlayerStateListEnvelope>(response.Value);
            var source = wire == null || wire.states == null
                ? new GRushPlayerStateWire[0]
                : wire.states;
            var states = new GRushPlayerState[source.Length];
            for (var index = 0; index < states.Length; index++)
            {
                states[index] = GRushWire.ToPlayerState(source[index]);
            }
            return GRushResult<GRushPlayerStatePage>.Success(
                new GRushPlayerStatePage
                {
                    States = states,
                    Exists = states.Length > 0,
                    RawJson = response.Value,
                }
            );
        }

        /// <summary>
        /// 他プレイヤーの状態を通報する。**ゲームは通報 UI を描けない。**
        /// 確認ダイアログは親（信頼済み UI）が出し、プレイヤーが承諾したときだけ
        /// 通報が送られる。戻り値は「通報が送られたか」。
        /// </summary>
        public async Task<GRushResult<bool>> ReportAsync(string pseudoId)
        {
            if (!GRush.IsPlayerStateAvailable)
            {
                return GRushResult<bool>.Unsupported();
            }
            var response = await GRush.CallAsync(
                "playerState.report",
                GRushWire.PlayerStateReportParams(pseudoId)
            );
            if (!response.Ok)
            {
                return GRushResult<bool>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushReportResultWire>(response.Value);
            return GRushResult<bool>.Success(wire != null && wire.reported);
        }

        private static GRushResult<GRushPlayerStatePage> ToSingle(GRushResult<string> response)
        {
            if (!response.Ok)
            {
                return GRushResult<GRushPlayerStatePage>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushPlayerStateEnvelope>(response.Value);
            if (wire == null || wire.state == null || string.IsNullOrEmpty(wire.state.pseudoId))
            {
                // まだ書いていない場合はここへ来る（state: null）。
                return GRushResult<GRushPlayerStatePage>.Success(
                    new GRushPlayerStatePage
                    {
                        States = new GRushPlayerState[0],
                        Exists = false,
                        RawJson = response.Value,
                    }
                );
            }
            return GRushResult<GRushPlayerStatePage>.Success(
                new GRushPlayerStatePage
                {
                    States = new[] { GRushWire.ToPlayerState(wire.state) },
                    Exists = true,
                    RawJson = response.Value,
                }
            );
        }
    }
}

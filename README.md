# GameRush SDK for Unity

GameRush の GameAPI を Unity から呼ぶための UPM パッケージ。ビルドの書き出し方は [対応エンジンと書き出しガイド](https://gamerush.jp/engines)。使い方の正（ランキング・公開プレイヤー状態・投稿が弾かれる条件とエラーコード・API トークン）は [SDK ガイド](https://gamerush.jp/sdk)。

## 導入

Package Manager の `Add package from git URL...` に次を入れる。

```text
https://github.com/gamerush-cc/grush-sdk-unity.git#v1.5.0
```

Unity 2021.3 以降。ビルドターゲットは WebGL。

## 使い方

```csharp
using GRushSdk;

var self = await GRush.Player.GetSelfAsync();
if (self.Ok)
{
    Debug.Log(self.Value.PseudoId);
}

var joined = await GRush.Net.JoinAsync("duel");
if (joined.Ok)
{
    var room = joined.Value;
    room.Message += message => Debug.Log(message.From);
    room.Send(payload, GRushChannel.Unreliable, GRushRoom.Everyone);
}
```

`GRush` は例外を投げない。GameRush の外で動かした場合も `GRushResult<T>.Ok` が `false`、`Code` が `GRushErrorCode.Unsupported` になるだけで、ゲームは止まらない。

### 相手探し

```csharp
await GRush.PlayerState.SetMineAsync("{\"deck\":\"fire\",\"level\":12}", -1, 1520);
var found = await GRush.PlayerState.SampleAsync(5, 1400, 1600);
if (found.Ok)
{
    foreach (var rival in found.Value.States) Debug.Log(rival.PseudoId + " " + rival.Rating);
}
```

`SampleAsync` は同じゲームの他プレイヤーの公開状態をランダムに返す（件数は 1〜20、既定 10）。自分と運営が伏せた状態は返らず、名前も返らない。範囲を渡すと、rating の無い状態は返らない。payload は `RawJson`（応答そのもの）から読む。`SetMineAsync` の第3引数の rating は -1e9 より大きく 1e9 より小さい数値で、`null` で消し、2引数の版では今の値のまま。`GRushPlayerState.HasRating` が false なら rating は書かれていない。`GRush.IsMatchmakingAvailable` が `false`（`protocolVersion` 7 未満）では `SampleAsync` が `Unsupported`、`SetMineAsync` は rating を送らずに payload だけを書く。

### 受信箱

```csharp
await GRush.Inbox.SendAsync(rivalPseudoId, "battleResult", "{\"stage\":3,\"win\":true}");
var inbox = await GRush.Inbox.ListAsync(true);
if (inbox.Ok)
{
    foreach (var message in inbox.Value.Messages)
    {
        var result = JsonUtility.FromJson<BattleResult>(message.PayloadJson);
    }
}
```

同じゲームの疑似 ID 宛てに、小さい構造化メッセージ（非同期対戦の結果の通知など）を送る。ゲストも送受信できる。**payload で文章や画像は送れない。** payload は JSON オブジェクトで、文字列の値とキーは識別子（`^[A-Za-z0-9_.:-]{1,32}$`）だけ、ほかは数値・真偽値・null。入れ子は最上位を 1 段目として 4 段まで、値は合計 64 個まで、詰めた JSON で 1KB まで。`kind` は `^[A-Za-z0-9._:-]{1,32}$`。合わなければ `InvalidParams`。利用者どうしの文章や画像のやりとりには使わない。

`ListAsync(unreadOnly, limit, cursor)` は新しい順（`limit` は 1〜50、0 なら 20）。続きは `NextCursor` を渡して読む。`MarkReadAsync` / `RemoveAsync` は 1 回 50 件まで。同じゲームの疑似 ID でない宛先は `InvalidParams`、送りすぎ（毎時・毎日の上限）は `RateLimited` で、時間が経てば回復する。`ReportAsync(messageId)` は GameRush が確認を出し、承諾されたら `true`。通報すると送り手をブロックし、その相手のメッセージと出品は見えなくなる。ゲストは `SignInRequired`。`GRush.IsInboxAvailable` が `false`（`protocolVersion` 7 未満）ではどれも `Unsupported`。

### 交換所

```csharp
// 1. 出すアイテムをセーブで仮置きにし、2. operationId と一緒に保存してから出品する。
var created = await GRush.Trade.CreateAsync(
    "{\"item\":\"sword\",\"count\":1}",
    "{\"item\":\"gem\",\"count\":3}",
    save.EscrowOperationId,
    new GRushTradeOptions { OfferKey = "sword", WantKey = "gem" }
);

// 4. 終わった取引をセーブへ反映し、保存してから Ack する。5. 取引 ID で二度反映しない。
var mine = await GRush.Trade.MineAsync();
```

プレイヤーがアイテムを出品し、同じゲームの別のプレイヤーが `AcceptAsync(tradeId, giveJson)` で受ける。ゲストも出品・受諾できる。GameRush はアイテムの正当性を確かめず、交換が片方だけ成立しないことと、終わった取引を取りこぼさないことだけを保証する。

取りこぼさないために、次の順で使う。

1. 出品の前に、出すアイテムを手元のセーブで仮置き（使えない状態）にする
2. `operationId`（`^[A-Za-z0-9._:-]{1,64}$`）をセーブに保存してから `CreateAsync` する
3. 応答が失われたら、同じ `operationId` で呼び直す。同じ `operationId` の再送は、中身によらず先に作った出品を返す
4. `MineAsync` の `Pending` をセーブに反映し、クラウドセーブに保存してから `AckAsync` する
5. 反映済みの取引 ID をセーブに持ち、同じ取引を二度反映しない

`Pending` の `Action` は `ReceiveGive`（出品者が `GiveJson` を受け取る）・`ReturnOffer`（出品者が `OfferJson` を戻す。取り下げ・期限切れ・運営の非表示）・`ReceiveOffer`（受けた側が `OfferJson` を受け取る）。`Unknown` は反映せず、Ack もしない。

受付中の出品が 10 件あるときの `CreateAsync` は `InvalidParams` で、待っても直らない（取り下げるか、成立・期限切れを待つ）。毎日 30 件の出品と毎時 30 件の受諾の上限は `RateLimited` で、時間が経てば回復する。

**offer・want・give で文章や画像は送れない。** 形は受信箱の payload と同じで、大きさは詰めた JSON で offer 2KB・want 1KB・give 2KB まで。`OfferJson` などはそのまま `JsonUtility.FromJson` でゲームの型に読める。利用者どうしの文章や画像のやりとりには使わない。

`AcceptAsync` は先に取られた・期限切れ・取り下げ・見えない出品をエラーにせず、`Outcome`（`Matched` / `Taken` / `Expired` / `Cancelled` / `NotFound`）で返す。`Trade` は `Matched` のときだけ入る（`HasTrade`）。同じ取引への再送は `Matched`。`GRushTradeOptions.To` に疑似 ID を入れると指名の出品になり、`ExpiresInSec` は 600〜604800（0 なら 3 日）。受付中の出品は 1 人 10 件まで。`ListAsync` は他人の公開の出品と自分宛ての指名の出品のうち、受付中のもの（新しい順）。`ReportAsync(tradeId)` は GameRush が確認を出し、承諾されたら `true`。通報すると出品者をブロックする。`GRush.IsTradeAvailable` が `false`（`protocolVersion` 7 未満）ではどれも `Unsupported`。

### 共有

```csharp
if (await GRush.Share.IsAvailableAsync())
{
    var shared = await GRush.Share.ShareScreenAsync("ステージ3をクリア");
}
```

GameRush の確認シートが出て、プレイヤーが送り先を押したときに共有が開く。返るのは `GRushShareStatus.Opened` / `Cancelled` だけ。`ShareScreenAsync` はゲームの canvas のスクショを送る（画面に DOM で重ねた文字は写らない）。自前の画像を送るなら `ShareAsync(text, texture.EncodeToPNG())`。共有は 5 秒に 1 回までなので、1 回のボタン操作で呼ぶのはどちらか一方にする。共有したことを条件に報酬を出さない。本文は 100 文字までで、URL と @メンションを含むと `InvalidParams`。古い GameRush（`protocolVersion` 3 未満）では `Unsupported`。

### 表示言語

```csharp
GRush.Locale.Changed += locale => Debug.Log(locale.Locale);
var result = await GRush.Locale.GetAsync();
if (result.Ok)
{
    var tag = result.Value.Locale;
}
```

`GRushLocale` は `Locale`（BCP 47 のタグ。`ja` や `zh-Hans`）、`Source`（`user` / `system` / `device`。今後増えても動くように書く）、`Languages`（優先順）を持つ。`GRush.Locale.Current` は取得済みなら同期で読め、無ければ `null`。`Changed` は次の `Update` で配られるので、取得の途中の変更を取りこぼさないよう `GetAsync` より先に購読する。`GRush.IsLocaleAvailable` が `false`（`protocolVersion` 4 未満）では `GetAsync` が `Unsupported` を返す。

## エディタでの動作確認

WebGL 以外（エディタ・スタンドアロン）では自動的に `GRushMockBackend` が使われる。`GRushMock` で挙動を切り替える。

```csharp
GRushMock.SignedIn = true;
GRushMock.DisplayName = "Editor Player";
GRushMock.GrantProfileConsent = false;
GRushMock.UnreliableDropRate = 0.1;
GRushMock.ShareStatus = GRushShareStatus.Cancelled;

var opponent = GRushMock.AddPeer("Sparring Partner");
opponent.Received += message => opponent.Send(reply, GRushChannel.Unreliable, GRushRoom.Everyone);
```

`GRushMock.AddPeer` で作った相手は同じプロセス内の2人目の peer として部屋に入り、送受信が実際に往復する。

表示言語のモックは `GRushMock.SetLocale("en")`（`Source` は `user`）で切り替わり、`Changed` が飛ぶ。未設定なら `Application.systemLanguage` から写した値を `Source = "device"` で返す。

共有のモックは確認シートを出さず、`GRushMock.ShareStatus`（既定 `Opened`）を返す。`GRushMock.ShareAvailable = false` で共有できない環境を試せる。本文と画像の検査はしない。

相手探し・受信箱・交換所のモックは、`GRushMock.Rivals` の 3 人の相手を持つ。相手はそれぞれ rating 付きの公開プレイヤー状態を持ち、届いたメッセージを同じ kind と payload で送り返す。はじめと `GRushMock.Reset()` の後は、最初の相手からのメッセージ1通と、相手の受付中の出品を2件持つ。payload・offer・want・give の形、`operationId` での冪等、受付中の出品の上限、受諾の `Outcome`、`Pending` と Ack は GameRush と同じ規則で動く。通報を承諾すると（`GRushMock.ConfirmInboxReport` / `ConfirmTradeReport`、既定 `true`）相手をブロックし、その相手のメッセージと出品は見えなくなる。

```csharp
GRushMockInbox.Receive(GRushMock.Rivals[0], "battleResult", "{\"stage\":3,\"win\":false}");
var listingId = GRushMockTrades.OfferToSelf(GRushMock.Rivals[1], "{\"item\":\"bow\"}", "{\"item\":\"gem\"}");
GRushMockTrades.Accept(myTradeId, "{\"item\":\"gem\"}"); // 相手が受けて、自分の Pending に ReceiveGive が載る
GRushMockTrades.Expire(listingId);
GRushMockPlayerStates.Define("custom-rival", "{\"level\":3}", false, 1300);
```

**`UnreliableDropRate` は既定 0 だが、出荷前に必ず 0 より大きくして試すこと。** WebSocket 中継では `unreliable` も落ちずに届くため、パケットが落ちる前提で書けているかを確認できる場所はエディタのモックだけになる。

### grush.json をモックで使う

ランキングの定義は、プロジェクトの直下（`Assets/` の隣）の `grush.json` に書ける。書式は [SDK ガイドのランキング](https://gamerush.jp/sdk#leaderboards)。

- エディタのモックは、最初のランキングの呼び出しの前にこのファイルを読み、その枠を宣言する。ファイルが無ければ何もしない。書式の誤りはアップロードと同じ文言で Console に警告し、その中身は使わない
- WebGL でビルドすると、このファイルがビルドの最上位（`index.html` の隣）へ写る。ファイルが無いのに出力先に `grush.json` が残っていれば、消さずに警告を出す（残ったままアップロードすると、その中身で定義を書き換える）。GameRush はアップロードされたビルドの最上位の `grush.json` だけを読むので、`Assets/` や `StreamingAssets/` に置いても定義にならない
- ファイルを自動で読むときは、`GRushMockLeaderboards.Define` で同じ key を宣言してあればそちらを使う。`GRushMockLeaderboards.LoadManifest(json)` に渡した定義は、`Define` の宣言を置き換える。`LoadManifest` を呼ぶと、ファイルは自動では読まない。`LoadManifest` は誤りを `InvalidParams` で返す
- `enabled: false` の枠は一覧に出ず、投稿と取得は宣言していない key と同じく `InvalidParams` になる。grush.json で `minValue` / `maxValue` を書かなかった枠は、GameRush と同じく投稿を受けない
- `period` が `daily` / `weekly` / `monthly` の枠は、GameRush と同じ期間キー（UTC）を返し、期間が変わると空からやり直す
- `GRushMockLeaderboards.Reset()` の後は、次のランキングの呼び出しでファイルを読み直す

## 推奨設定でビルド

メニューの `GameRush/GameRush ウィンドウ` を開くと、先頭に「推奨設定でビルド」が出る。ログインしなくても使える。WebGL の設定ごとに、いまの値・推奨値・合っているか（✓ / ✗）を一覧にする。

- **推奨設定を適用**: ✗ の項目だけを推奨値へ変える。変えた設定は元に戻さない（`GameRush/推奨設定を適用` からも同じ）。
- **推奨設定でビルド**: 設定を適用してから、Build Settings で有効なシーンを WebGL でビルドする（`GameRush/推奨設定でビルド` からも同じ）。出力先は既定で `<プロジェクト>/Build/GameRush`。**出力先のフォルダはビルドのたびに消して作り直す**ので、プロジェクトのフォルダそのものや `Assets/` `Packages/` `ProjectSettings/` `Library/` の中は選べない。既にあるフォルダは、中身が `Build/` `TemplateData/` `StreamingAssets/` `index.html` `grush.json` だけか空のときに限って消す。
- プロジェクトの直下に `grush.json` があれば、WebGL のビルドのたびに出力先の最上位へ写す（このウィンドウ以外からの WebGL ビルドでも写す）。
- ビルドが通ると、その出力先が「ビルドをアップロード」に入った状態になる。ログインしていればそのまま上げられる。
- 合計サイズの目安は 15 MB 以下。30 MB 以上は読み込みが遅く、スマホで落ちることもあるので危険。50 MB を超えるとアップロードできない。

主な推奨値は Brotli 圧縮・Decompression Fallback オフ・Threads Support オフ（GameRush の配信は COOP/COEP を付けない）・Data Caching オフ（GameRush が自前で先読みする）・WebGL 2 のみ・Development Build オフ。

CI などからはコマンドラインで同じことができる。

```text
Unity -batchmode -quit -projectPath <プロジェクト> -buildTarget WebGL -executeMethod GRushSdk.Editor.GRushBuild.BuildRecommended -grushOutput Build/GameRush
```

`-grushOutput` の相対パスはプロジェクトのフォルダから数える。ログの行は `[grush-build] ` で始まり、変えた設定は `set <項目>: <前> -> <後>` の形で出る。

| 終了コード | 意味 |
|---|---|
| 0 | ビルドでき、GameRush へ上げられる出力になった |
| 1 | ビルドに失敗した |
| 2 | 引数が無い・出力先が危ない・GameRush のビルド以外のファイルがある出力先・WebGL モジュールが無い・有効なシーンが無い |
| 3 | 想定外の例外 |
| 4 | ビルドはできたが、GameRush のアップロード規則に合わない出力になった |

## サンプル

Package Manager の Samples から取り込む。どちらもシーンを含まないので、空のシーンに GameObject を1つ作ってスクリプトを付ける。

| サンプル | 内容 |
|---|---|
| Score Attack | 疑似IDの取得と表示名の同意要求 |
| Duel | 2人対戦。エディタではモックの対戦相手が動く |

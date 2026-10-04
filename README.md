# GameRush SDK for Unity

GameRush の GameAPI を Unity から呼ぶための UPM パッケージ。ビルドの書き出し方は [対応エンジンと書き出しガイド](https://gamerush.jp/engines)。使い方の正（ランキング・公開プレイヤー状態・投稿が弾かれる条件とエラーコード・API トークン）は [SDK ガイド](https://gamerush.jp/sdk)。

## 導入

Package Manager の `Add package from git URL...` に次を入れる。

```text
https://github.com/gamerush-cc/grush-sdk-unity.git#v1.2.2
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

**`UnreliableDropRate` は既定 0 だが、出荷前に必ず 0 より大きくして試すこと。** WebSocket 中継では `unreliable` も落ちずに届くため、パケットが落ちる前提で書けているかを確認できる場所はエディタのモックだけになる。

## 推奨設定でビルド

メニューの `GameRush/GameRush ウィンドウ` を開くと、先頭に「推奨設定でビルド」が出る。ログインしなくても使える。WebGL の設定ごとに、いまの値・推奨値・合っているか（✓ / ✗）を一覧にする。

- **推奨設定を適用**: ✗ の項目だけを推奨値へ変える。変えた設定は元に戻さない（`GameRush/推奨設定を適用` からも同じ）。
- **推奨設定でビルド**: 設定を適用してから、Build Settings で有効なシーンを WebGL でビルドする（`GameRush/推奨設定でビルド` からも同じ）。出力先は既定で `<プロジェクト>/Build/GameRush`。**出力先のフォルダはビルドのたびに消して作り直す**ので、プロジェクトのフォルダそのものや `Assets/` `Packages/` `ProjectSettings/` `Library/` の中は選べない。既にあるフォルダは、中身が `Build/` `TemplateData/` `StreamingAssets/` `index.html` だけか空のときに限って消す。
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

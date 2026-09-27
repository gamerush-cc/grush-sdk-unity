# GameRush SDK for Unity

GameRush の GameAPI を Unity から呼ぶための UPM パッケージ。ビルドの書き出し方は [対応エンジンと書き出しガイド](https://gamerush.cc/engines)。使い方の正（ランキング・公開プレイヤー状態・投稿が弾かれる条件とエラーコード・API トークン）は [SDK ガイド](https://gamerush.cc/sdk)。

## 導入

Package Manager の `Add package from git URL...` に次を入れる。

```text
https://github.com/gamerush-cc/grush-sdk-unity.git
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

## エディタでの動作確認

WebGL 以外（エディタ・スタンドアロン）では自動的に `GRushMockBackend` が使われる。`GRushMock` で挙動を切り替える。

```csharp
GRushMock.SignedIn = true;
GRushMock.DisplayName = "Editor Player";
GRushMock.GrantProfileConsent = false;
GRushMock.UnreliableDropRate = 0.1;

var opponent = GRushMock.AddPeer("Sparring Partner");
opponent.Received += message => opponent.Send(reply, GRushChannel.Unreliable, GRushRoom.Everyone);
```

`GRushMock.AddPeer` で作った相手は同じプロセス内の2人目の peer として部屋に入り、送受信が実際に往復する。

**`UnreliableDropRate` は既定 0 だが、出荷前に必ず 0 より大きくして試すこと。** WebSocket 中継では `unreliable` も落ちずに届くため、パケットが落ちる前提で書けているかを確認できる場所はエディタのモックだけになる。

## 推奨設定でビルド

メニューの `GameRush/GameRush ウィンドウ` を開くと、先頭に「推奨設定でビルド」が出る。ログインしなくても使える。WebGL の設定ごとに、いまの値・推奨値・合っているか（✓ / ✗）を一覧にする。

- **推奨設定を適用**: ✗ の項目だけを推奨値へ変える。変えた設定は元に戻さない（`GameRush/推奨設定を適用` からも同じ）。
- **推奨設定でビルド**: 設定を適用してから、Build Settings で有効なシーンを WebGL でビルドする（`GameRush/推奨設定でビルド` からも同じ）。出力先は既定で `<プロジェクト>/Build/GameRush`。**出力先のフォルダはビルドのたびに消して作り直す**ので、プロジェクトのフォルダそのものや `Assets/` `Packages/` `ProjectSettings/` `Library/` の中は選べない。既にあるフォルダは、中身が `Build/` `TemplateData/` `StreamingAssets/` `index.html` だけか空のときに限って消す。
- ビルドが通ると、その出力先が「ビルドをアップロード」に入った状態になる。ログインしていればそのまま上げられる。
- 合計サイズの目安は 30 MB 以下。300 MB を超えるとアップロードできない。

主な推奨値は Brotli 圧縮・Decompression Fallback オフ・Threads Support オフ（GameRush の配信は COOP/COEP を付けない）・Data Caching オフ（GameRush が自前で先読みする）・WebGL 2 のみ・テンプレート Minimal・Development Build オフ。

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

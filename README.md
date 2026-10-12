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

`GRush` は例外を投げない。GameRush の外で動かした場合も `GRushResult<T>.Ok` が `false`、`Code` が `GRushErrorCode.Unsupported` になるだけで、ゲームは止まらない（アナリティクスは `bool` を返す。下記）。

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

### アナリティクス

```csharp
using System.Collections.Generic;
using GRushSdk;

GRush.Analytics.LogOnboardingStep(1, "tutorial_start");
GRush.Analytics.LogFunnelStep("shop", "visit-1", 2, "item_selected");
GRush.Analytics.LogCustomEvent(
    "boss_defeated",
    1,
    new Dictionary<string, string> { { "stage", "3" } }
);
GRush.Analytics.LogEconomyEvent(
    GRushEconomyFlowType.Sink,
    "coin",
    50,
    120,
    GRushEconomyTransactionType.Shop,
    itemSku: "sword"
);
```

作者のアナリティクスへ、オンボーディングとファネルのステップ・カスタムイベント・通貨の入手と消費を送る。

- **ほかの API と違い、どれも同期で `bool` を返し、例外を投げない。** `await` せずにどこからでも呼べる。`true` は GameRush へ渡したことで、保存されたことではない
- 形が不正なとき（下の制限）は `false`。古い GameRush（`protocolVersion` 6 未満。`GRush.IsAnalyticsAvailable` が `false`）と GameRush の外では `false`。`GRush.Analytics.IsAvailable` は GameRush の中で送れるかを返す
- `step` は 1〜100。`value` は省略で 1、絶対値 1e9 まで。`amount` は正の値で 1e12 まで、`endingBalance`（その後の残高）は絶対値 1e15 まで
- `transactionType` の標準の値は `GRushEconomyTransactionType` の定数で、ほかの文字列も受ける
- イベント名・ファネル名・`funnelSessionId` は英数字と `._:-` で 64 文字まで、通貨と `transactionType` は 32 文字まで
- `stepName`・`itemSku`・`fields` の値は1行のテキストに直してから受ける。改行・制御文字・双方向の上書きと isolate は空白に直し、前後の空白を落としてから 40 文字（コードポイント）まで数える。直した結果が空（空白や見えない文字だけ）の `stepName` と `itemSku` は省略と同じに扱い、`fields` の値は拒む。`fields` は3つまで、キーは英数字と `_` で 32 文字まで
- オンボーディングとファネルの同じステップは、1セッションで最初の1回だけが残る。セッションはページを読み直しても続く。同じファネルを何度も通るなら、通るたびに別の `funnelSessionId` を渡す
- 名前・値・`fields` に、プレイヤーを識別できる情報を入れない
- GameRush の中でも、ゲームが前面に出るまでの分は溜めてから送る。溜められるのは 30 件までで、超えた分は `false`
- `true` を返しても、次の分は保存されない
  - 前面に出た後に GameRush 本体が掛ける上限を超えた分。本体は 500ms に1件、まとめて最大 30 件まで受け、送る前の列は合計 200 件まで
  - セッションあたりの保存の上限を超えた分。オンボーディングを含むファネルのステップ 200 件・カスタムイベント 500 件・エコノミー 500 件
  - 送れなかった分（送り直さない）

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

アナリティクスのモックは、GameRush と同じ検査をして、通ったイベントを1件ずつ Console に出し、直近の 200 件を `GRushMock.AnalyticsEvents`（GameRush へ渡すのと同じ形の JSON の配列）で返す（`GRushMock.Reset()` で空になる）。弾いたイベントは、GameRush と同じく理由ごとに1回だけ警告する（`GRushMock.Reset()` でまた出る）。どこへも送らない。モックが名乗る `protocolVersion` は 6。前面に出るまでの 30 件の溜め・本体の上限・同じステップを最初の1回だけ残す扱い・セッションあたりの上限は再現しないので、モックでは `true` の分がすべて記録に残る。

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

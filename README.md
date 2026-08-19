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

## サンプル

Package Manager の Samples から取り込む。どちらもシーンを含まないので、空のシーンに GameObject を1つ作ってスクリプトを付ける。

| サンプル | 内容 |
|---|---|
| Score Attack | 疑似IDの取得と表示名の同意要求 |
| Duel | 2人対戦。エディタではモックの対戦相手が動く |

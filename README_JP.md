# UITK VNode

Runtime UI Virtual DOM for Unity (Design Draft)

> **Status:** Draft / 0.1.0  
> **Target:** Unity 2022+ / UI Toolkit (runtime)  
> **Scope:** 論理UIを C# 側で持ち、UIToolkit と VFX / カスタム描画を橋渡しする仕組みの設計書

このドキュメントは、**UI Toolkit を「論理・イベント・レイアウトの層」として使い、実際のリッチ描画は別レイヤー（uGUI / VFX Graph / Shader）に任せる**ことを目的とした設計のたたき台です。  
最終的には Unity Package として配布・再利用できる構成を想定しています。

---

## 1. コンセプト

1. **Virtual DOM を C# で持つ**  
   - 実際の UI 構造（ボタン・ラベル・コンテナなど）を C# のオブジェクトツリー（VNode）として表現する。
   - ランタイムで要素の追加・削除・並び替えができる。
   - これを「正」とみなす。

2. **UIToolkit はあくまで“表示エンジン”として使う**  
   - VNode の内容を UIToolkit の `VisualElement` ツリーに“差分適用”する Reconciler を用意する。
   - UIToolkit に直接ベタ書きしないことで、ロジックと表示を分離する。

3. **描画レイヤーは VNode/UITK から“観測した結果”だけを受け取る**  
   - UI要素の最終的な `x, y, width, height` や dataset 情報を C# 側でまとめ、GPU が読みやすい形式（配列 / GraphicsBuffer）にして渡す。
   - VFX Graph やカスタムシェーダーは VNode を直接参照しない。

4. **dataset で柔軟にメタデータを流す**  
   - HTMLの `data-*` に近い仕組みを VNode に持たせる。
   - VFX 側で「state=running の UI だけ光らせる」などができる。

---

## 2. 想定する使用シナリオ

- ゲームのUIが動的に増減する（ショップ、スコアボード、ロビー、プレイヤーリスト…）
- UIが画面上で動いたり表示非表示になったりすると、それに合わせて**背景のVFXやポストエフェクトがダイナミックに反応する**必要がある
- UIデザイナは UIToolkit / USS で構造と最低限の見た目を書くが、最終的な“すごい見た目”はアート／テックアート側で C# + VFX で作りたい

---

## 3. パッケージ構成（案）

```text
RuntimeUIVDOM/
  Runtime/
    VDom/
      VNode.cs
      VNodeQuery.cs
      VNodeFactory.cs
      UiReconciler.cs
    Sync/
      UiToolkitSnapshot.cs
      UiToVfxBridge.cs
    Util/
      IdUtility.cs
  Editor/
    (optional) VNode inspector / debug viewer
  README.md
  package.json
```

- `Runtime/VDom` … Virtual DOM周り。今回の中核。
- `Runtime/Sync` … UITKから“今どうなってるか”を拾って描画レイヤーに投げる部分。
- `Runtime/Util` … ハッシュやID生成など補助的なもの。

---

## 4. VNode の仕様（論理UIの表現）

```csharp
public class VNode
{
    public string Id;   // 安定ID。必須。例: "lobby/player-list"
    public string Type; // "container", "label", "button", "image", "scroll", ...
    public Dictionary<string, string> Props = new();
    public Dictionary<string, string> Dataset = new();  // ← data-* 相当
    public List<VNode> Children = new();
}
```

### 4.1 Id
- できるだけ**安定した文字列**を使う（ランダムGUIDでもよいが、再生成されないこと）
- Reconciler はこの `Id` をキーにして差分を取る
- 同じ `Id` を持つノードは兄弟の中に2つ以上あってはならない

### 4.2 Type
- UIの種類を表す文字列
- 実体化時に `switch (Type)` で `VisualElement` の種類を決める
- まずは最小セットでOK: `"container" | "label" | "button" | "image"`

### 4.3 Props
- 見た目・レイアウトに関する“意図”をここに書く
  - 例: `["text"] = "Play"`, `["class"] = "primary-button"`, `["width"]="200"`
- Reconciler が VisualElement に適用する

### 4.4 Dataset
- アプリケーション／VFX／外部システム向けのメタデータ
- 例: `["state"]="running"`, `["role"]="enemy"`, `["index"]="3"`
- 字句の自由度を優先し、まずは `string -> string` にしておく
- 後段の C# → GPU 橋渡しで数値にマッピングする

---

## 5. Reconciler の仕様（VNode → UIToolkit）

### 5.1 目的
- VNode ツリーを **UIToolkit の VisualElement ツリーに反映**する
- 既にある要素は**使い回す**
- VNodeに存在しない実要素は**削除する**
- 子の**並び順も** VNode に合わせる

### 5.2 アルゴリズム（簡易版）

1. 親の VNode と VisualElement を受け取る
2. 親VEの子を名前（=Id）で辞書化
3. 親VNodeの子リストを先頭から順に見る
   - 同じIdのVEがあれば Props を更新し、必要なら並び替え
   - なければ新規に VisualElement を生成して挿入
   - 再帰して子も同期
   - 処理済みのVEは辞書から消す
4. 辞書に残ったVEは VNode に存在しないので削除

### 5.3 生成の決定
- `VNodeFactory.Create(VNode node)` で実際の `VisualElement` を生成する
- Factoryを差し替えれば独自のカスタム要素も使える

---

## 6. QuerySelector 相当の機能

VNode を**コードから操作しやすくするため**に、簡易セレクタを用意する。

### 6.1 サポートする書式（初期案）

- `#id` … Id一致
- `.class` … Props["class"] に含まれる
- `type` … Type一致
- `[key=value]` … Props または Dataset のキーと値が一致

```csharp
var node = rootVNode.QuerySelector("#play-button");
var labels = rootVNode.QuerySelectorAll("label");
var running = rootVNode.QuerySelectorAll("[state=running]");
```

※複雑なCSSセレクタまではやらない。必要になったら`,` と子孫セレクタを拡張。

---

## 7. UIToolkit → “スナップショット” 取得

UIToolkit はレイアウト計算後でないと座標・サイズが確定しないため、  
**実ツリー側の変化を観測するコンポーネント**を用意する。

### 7.1 取得するイベント
- `AttachToPanelEvent` … 要素がパネルに入ったとき（=生成・追加）
- `DetachFromPanelEvent` … 要素がパネルから出たとき（=削除）
- `GeometryChangedEvent` … 座標・サイズが変わったとき

### 7.2 スナップショット構造体（実UIの状態）
```csharp
public struct UiNodeSnapshot
{
    public string Id;      // VE.name
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public bool Visible;
    public string ParentId;
    public int OrderInParent;
    // 将来的に: short state; short role; を詰めてもよい
}
```

- これは **“今の見た目”** であり、VNode とは別物
- 描画レイヤーはこれを読む

---

## 8. C# → VFX / GPU 橋渡し

### 8.1 方針
- **VFX が VNode を直接参照することはしない**
- C# で **「GPUが読める構造体」に詰め替えてから** VFX に渡す

### 8.2 GPU向け構造体（例）

```csharp
struct UIElementData
{
    float4 rect;     // x, y, w, h
    uint id;         // VNode.Id をハッシュしたもの
    uint dataset0;   // 例: state
    uint dataset1;   // 例: role
};
```

- 長さ `N` の配列として `GraphicsBuffer` / `ComputeBuffer` に詰めて渡す
- `VFX.SetGraphicsBuffer("UIElements", buffer);` でバインド

### 8.3 dataset のマッピング
- VNode.Dataset の中でも **VFX側で参照したいキーだけ** を決め打ちする
  - 例: `"state"`, `"role"`, `"index"`
- それぞれ C# で数値に変換し、対応するフィールドに入れる
- VFX側は「dataset0 == 1 なら active」などで解釈

---

## 9. データの流れ（全体）

```text
[1] ゲームロジック
     ↓ (VNodeを生成・変更)
[2] VNodeツリー
     ↓ (Reconciler)
[3] UIToolkitの実VisualTree
     ↓ (Attach/Geometryを監視)
[4] スナップショット（UiNodeSnapshot List）
     ↓ (C#でパック/Encode)
[5] GPU/VFX が読むバッファ
     ↓
[6] リッチなUI背景・アニメ
```

**常に一方向**に流すのがポイント。  
これにより「UIが動くと描画も動く」が作りやすくなる。

---

## 10. 今後の拡張ポイント

1. **差分アップデート**  
   - 今は「全部詰め直す」前提だが、更新された要素だけGPUに流すようにすると大規模UIでも安定する

2. **型付きProps / Dataset**  
   - 今は `string` 前提だが、後で `int/float/bool` も混ぜたい場合は `struct VNodeValue` を定義してもよい

3. **Editor拡張**  
   - 現在の VNode ツリーをインスペクタで見たい
   - QuerySelector のテストUIを作る

4. **複数パネル対応**  
   - 複数の `UIDocument` / Panel を管理するマネージャを用意する

---

## 11. 想定される制約・注意点

- UIToolkit のレイアウトタイミングによっては、`GeometryChangedEvent` が複数回飛ぶことがある → スナップショット側に dirty フラグを持っておくとよい
- VFX に渡す最大件数はあらかじめ決めておくこと（256, 512など） → 超えた分は描画しない
- `Id` が安定していないと Reconciler が毎回作り直してしまいパフォーマンスに影響する
- dataset は文字列なので、**GPU向けには必ずC#でエンコードする層を挟むこと**

---

## 12. まとめ

- **Virtual DOM (VNode)** を“論理UIの真実”として持つ
- **Reconciler** で UIToolkit に反映する
- **実UIを観測**して、描画レイヤーが読みやすいスナップショットを作る
- **C#でGPU用にパック**してから VFX / Shader に渡す
- VFX は VNode を知らなくてよい（疎結合）

この形にしておくと、あとから
- WebSocketで外部からUIを差し替える
- ScriptableObjectでUI定義を配る
- 同じUI定義を別プロジェクトに持っていく
といった要求にも応えやすくなります。

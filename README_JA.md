# Peach Cat for Warudo

[한국어](README_KO.md) · 日本語 · [English](README.md)

Grok のキャラクターモーションから着想を得た、Warudo 向けの猫の小物です。独立したプロジェクトです。

![Peach Cat のモーションプレビュー](media/peach-cat.gif)

## 機能

- ARKit52 の顔トラッキングに合わせて、笑顔、しょんぼり顔、怒り顔に変わります。笑顔が続くと左右に弾みます。
- 頭と目の向きを追います。待機中は周囲を見回し、まばたき、伸縮、横回転、宙返り、丸い目などの動作を行います。
- チャットイベントを受け取ると、下を向いて左右に目を通すような動きをします。
- 本体・頬・目の色、追従感度、反応のタイミング、待機動作の頻度、チャットを読む動きを設定できます。

## インストール

1. [Releases](https://github.com/chomiles/peach-cat-warudo/releases) から **PeachCat-for-Warudo-1.5.0.zip** をダウンロードします。
2. Warudo の **Menu → Open data folder** でデータフォルダーを開き、展開した ZIP の **Props** と **Playground** をそのフォルダーにコピーします。
3. **Prop** を追加し、**Source** から **PeachCat** を選んでシーンを保存します。スケールの初期値は 1 が目安です。
4. **Settings → Peach Cat Reactions** に各種設定とプレビューボタンがあります。

## 顔トラッキングとアバターの変更

Warudo の [iFacialMocap Receiver](https://docs.warudo.app/docs/mocap/ifacialmocap) を設定し、**Face Tracking - iFacialMocap** ブループリントを有効にします。有効な受信機の ARKit 係数と頭・目の回転を直接使うため、アバターを変更してもメッシュやブレンドシェイプの再設定は不要です。このバージョンは Warudo の iFacialMocap 受信機に対応します。ほかの ARKit52 トラッカーにはアダプターが必要な場合があります。トラッキングがないときも待機動作は再生されます。

## チャットの接続

- **ブループリント** · 対応するチャット受信ノードのフロー出力を **Peach Cat Read Chat → Enter** に接続します。**React to Chat Events** をオンにし、**Use Local Overlay Feed** をオフにします。ホットキーノードや **Preview Chat Reading** でも動作を確認できます。
- **HTTPS SSE** · 自分の中継サーバーが `"platform":"chzzk"` を含む JSON を `event: chat` として送る場合は、データフォルダー直下の `PeachCatChatUrl.txt` に HTTPS の `/events` URL を 1 行で記入します。**Use Local Overlay Feed** と **React to Chat Events** をオンにします。URL の変更後はフィードをオフにしてから再度オンにしてください。URL ファイルは公開しないでください。
- **CHZZK 公式 API** · 別途受信ツールを用意し、配信者アカウントで OAuth 同意を行い、チャットメッセージ読み取り権限を取得します。Socket.IO セッションで `CHAT` を購読し、上記のいずれかの入力に接続します。この配布物は Warudo 側の連携を提供します。ログインと公式 API の受信部分は別途設定してください。 [CHZZK Session API](https://chzzk.gitbook.io/chzzk/chzzk-api/session)

## モデルの編集

`Data/PeachCatEditable.unitypackage` にはプレハブ、マテリアル、待機アニメーションが入っています。lilToon を導入した Built-in Render Pipeline の Unity プロジェクトにインポートして、`Assets/PeachCat/Prop.prefab` を開きます。制作環境は Unity 2022.3.22f1、lilToon 2.3.4、Warudo 対応の小物エクスポーターです。

`Data/PeachCatBody.obj` は Blender で編集できる顔と耳のメッシュです。`Data/Editor` の生成スクリプトを Unity プロジェクトの `Assets/Editor` にコピーすると、寸法を変更して **Tools → Peach Cat → Generate Editable Prefab** から再生成できます。

編集後は Warudo SDK で `Prop` プレハブを再出力します。スクリプトが参照する `PeachCat.warudo` のファイル名と `Bounce/Gaze/Eyes`、`Face and ears` の階層を維持してください。顔の形を大きく変えた場合は、目の位置と頬の投影範囲も調整してください。

## ライセンス

オリジナルのコード、モデル、モーションは [MIT ライセンス](LICENSE)で公開します。lilToon とその構成要素には[元のライセンスと通知](Licenses/)が適用されます。

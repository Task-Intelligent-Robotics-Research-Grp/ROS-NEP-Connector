ROS-NEP-Connector
==================================================

## 概要
本ソフトウェアは，通信ミドルウェアとして[NEP+](https://nep.plus)を用い，`ROS`と連携するUnityアプリケーションを開発するためのUnityパッケージである．

## インストール手順
### NEP+のインストール
[NEP+のドキュメント](https://coronadoenrique.gitbook.io/nep+/developer-tools/nep-cli)に従い，`NEP CLI`をインストールする．

### 本ソフトウェアのUnityプロジェクトへの取り込み
開発するUnityアプリケーションのプロジェクトに，以下の手順で本ソフトウェアを取り込む．
- "Window" -> "Package Manager"メニューから`Package Manager`を選択し，"+"ボタンを押して"Add package from fit URL..."から以下のURLを入力することにより[UniTask](https://github.com/Cysharp/UniTask)をインストールする．
```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```
- 同様に以下のURLを入力して[Newtonsoft.Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)をインストールする．
```
com.unity.nuget.newtonsoft-json
```
- 同様に以下のURLを入力して本ソフトウェアを取り込む．
```
git@github.com:Automation-Research-Team/ROS-NEP-Connector.git?path=Assets/ROSNEPConnector
```
以上で，アプリケーションが`NEP+`を介して`ROS`と通信するための機能が利用できるようになる．
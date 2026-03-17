README_UnityChan.TXT

『ユニティちゃん 3Dモデルデータ』Ver 1.4.0

2023/02/17 Unity Technologies Japan

【修正履歴】
Ver. 1.4.0　2023/02/17　シェーダーをUnity Toon Shaderに置き換え、設定し直しました。


【配布ライセンス】
本デジタルアセットデータは「ユニティちゃんライセンス条項（UCL）」（配布時点での最新版は、UCL 2.02）に基づき公開されるデジタルアセットデータです。
最新版の「ユニティちゃんライセンス条項」は以下をご確認ください。

ユニティちゃんライセンス条項・要約
https://unity-chan.com/contents/guideline/

ユニティちゃんライセンス条項・正文
https://unity-chan.com/contents/license_jp/

キャラクター利用のガイドライン（FAQ）
https://unity-chan.com/contents/faq/

特にFAQでは、本ライセンスの元で、クリエイターの皆さんが「できること」「できないこと」を詳しく具体例をあげて説明しています。
使用前に必ずご一読していただけますよう、よろしくお願いします。


【利用環境】
Unity Toon Shaderは、Built-In、URP、HARPのいずれの環境でも利用することができます。
詳しくは、以下のマニュアルをご確認ください。インストール方法もマニュアル中に記述されています。
https://docs.unity3d.com/Packages/com.unity.toonshader@latest

本デジタルアセットデータの元データは、Unity 5.2.3 f1で作成しました。
Unity Toon Shaderへの換装作業は、Unity 2020.3.44f1のBuiltIn環境で行いました。


【使い方】
1. Unity 2020.3.x f1 以降のUnityで新規プロジェクトを作成し、Unity Toon Shaderをパッケージマネージャから前もってインストールしておきます。
   Unity Toon Shaderのインストール方法は、上のマニュアルのリンクからInstallationの項目を探し、確認してください。

2. プロジェクトウィンドウに本 Unitypackage をD&Dします。
   正常に解凍され、Assetsフォルダ以下にUnityChanフォルダが新規作成されることをご確認ください。

3. Unity Toon Shaderは、BuiltIn、URP、HDRPの全てのUnityのレンダリングパイプラインに対応しています。
   サンプルシーンを開くと、BuiltIn、URPなら、そのままキャラクターが表示されています。
   HDRPの場合、サンプルシーンを開いた直後、モデルが真っ黒に表示されることがあります。
   その場合、サンプルシーン内のDirectional Lightを一度選択してください。
   キャラが正常に表示されたら、シーンをセーブします。以後は、サンプルシーンを開いた直後から、キャラクターが正しく表示されます。


【サンプルシーン】
\Assets\UnityChan\Scenes　以下にサンプルシーンがあります。

【サンプルシーンのキャラクターにアタッチされているコンポーネント】
各シーンには、ユニティちゃんのキャラクターモデルが必ず１つあります。
キャラクタ－モデルにアタッチされている主なコンポーネントは以下のようになっています。

●Animatorコンポーネント
　Mecanim/Humanoid形式のAnimatorコンポーネントです。
　AnimatorコントローラはMecanim/HumanoidもしくはMecanim/Gerneric毎に異なります。

●Idle Changerコンポーネント
　アニメーションを切り替えるコンポーネントです。

●Face Updateコンポーネント
　フェイスを切り替えるコンポーネントです。

●Auto Blinkコンポーネント
　自動目パチ（オートブリンク）をするコンポーネントです。

●Spring Managerコンポーネント
　揺れもの（ダイナミクス）制御をするコンポーネントです。

●Random Windコンポーネント
　モデルが静止している時にも、揺れものを風に吹かれているように揺らすコンポーネントです。
　初期状態は非アクティブです。


【お問い合わせ先】

ユニティ・テクノロジーズ・ジャパン合同会社
unity-chan@unity3d.com

ユニティちゃんオフィシャルサイト
https://unity-chan.com/


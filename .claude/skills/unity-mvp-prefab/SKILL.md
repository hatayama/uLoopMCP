---
name: unity-mvp-prefab
description: >
  Unity UI プレハブ（オーバーレイ、ゲーム内 UI、パネル）を MVP パターンで構築する。
  View は子要素のみ参照し、Presenter は View を SerializeField で参照して制御する。
  新しい MonoBehaviour + プレハブで UI を表示する場面すべてで使用すること。
  オーバーレイ、HUD、ダイアログ、ゲーム内パネルの作成時にトリガーされる。
---

# Unity MVP プレハブパターン

Unity UI プレハブ作成時は **View**（表示）と **Presenter**（ロジック）を分離する。

## 基本原則

### SerializeField を優先する

コンポーネントへの参照は基本的に `[SerializeField]` で取得する。`GetComponent` は SerializeField では対応できない場合のみ使う。

### 子要素のカスタムコンポーネントを貫通しない

子要素にカスタムコンポーネントがある場合、そのコンポーネントの**さらに子の要素に直接アクセスしてはならない**。必ずカスタムコンポーネントの public メソッドを経由して司令を出す。

この原則により転送メソッド（中継するだけのメソッド）が増えるが、それを敢えて推奨する。各コンポーネントが自分の子要素だけを知っている状態を保つことで、プレハブの構造変更に強くなる。

## View と Presenter の役割

### View — 子要素のみ参照する受動的な表示装置

- `[SerializeField]` は **子要素のみ** を指す — 親や兄弟 GameObject への参照は禁止
- ビジネスロジックなし — データソースを直接読まない
- public メソッドで「何を表示するか」を受け取る（例: `SetText(string)`）

### Presenter — View を制御するロジック担当

- View を `[SerializeField]` で参照する
- MonoBehaviour でも plain C# クラスでもよい
- データソースを読み取り、View の public メソッドを呼んで表示を更新する

## View の実装例

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    public class ExampleOverlayView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _messageText;
        [SerializeField] private GameObject _contentGroup;

        private void Awake()
        {
            Debug.Assert(_canvasGroup != null, "_canvasGroup must be assigned in prefab");

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            Hide();
        }

        public void ShowContent(string message)
        {
            _contentGroup.SetActive(true);
            _messageText.text = message;
            _canvasGroup.alpha = 1f;
        }

        public void SetAlpha(float alpha)
        {
            _canvasGroup.alpha = alpha;
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            _contentGroup.SetActive(false);
        }
    }
}
```

## Presenter の実装例（MonoBehaviour）

```csharp
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public class ExampleOverlayPresenter : MonoBehaviour
    {
        private const float FADE_OUT_DURATION = 0.5f;

        [SerializeField] private ExampleOverlayView _view;

        private bool _wasActive;
        private float _deactivatedTime;

        private void Awake()
        {
            Debug.Assert(_view != null, "_view must be assigned in prefab");
        }

        private void LateUpdate()
        {
            if (ExampleOverlayState.IsActive)
            {
                _wasActive = true;
                _deactivatedTime = 0f;
                _view.ShowContent(ExampleOverlayState.Message);
                return;
            }

            if (!_wasActive)
            {
                return;
            }

            if (_deactivatedTime <= 0f)
            {
                _deactivatedTime = Time.realtimeSinceStartup;
                return;
            }

            float elapsed = Time.realtimeSinceStartup - _deactivatedTime;
            if (elapsed > FADE_OUT_DURATION)
            {
                _view.Hide();
                _wasActive = false;
                _deactivatedTime = 0f;
                return;
            }

            float alpha = 1f - (elapsed / FADE_OUT_DURATION);
            _view.SetAlpha(alpha);
        }
    }
}
```

## プレハブ構造

```
ExampleOverlay (ExampleOverlayPresenter)
├── View (ExampleOverlayView + CanvasGroup)
│   ├── ContentGroup (GameObject)
│   │   └── MessageText (Text)
│   └── （必要に応じて他の子グループ）
```

- Presenter はルートまたは任意の位置に配置し、View を SerializeField で参照
- View は自分の子要素のみ参照
- 親キャンバスプレハブに **ネスト Prefab Instance** として追加する

## チェックリスト

新しい UI プレハブ作成時:

1. [ ] View クラスを作成（MonoBehaviour、子のみの `[SerializeField]`、public API メソッド）
2. [ ] Presenter クラスを作成（View を `[SerializeField]` で参照、`LateUpdate` でデータソースを読んで View を制御）
3. [ ] View にデータソースへの参照がないこと
4. [ ] `GetComponent` を使っていないこと（SerializeField で代替できる場合）
5. [ ] 子要素のカスタムコンポーネントの子に直接アクセスしていないこと（必ず中継メソッド経由）
6. [ ] プレハブを正しい子階層で構築
7. [ ] 親キャンバスに適切な `[SerializeField]` 参照 + `Debug.Assert` を追加

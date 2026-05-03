---
description: Link this repository's native Go uloop CLI as the global uloop command
allowed-tools: Bash, Read, Grep
---

# Link Go CLI

この repository の開発中 Go CLI を global な `uloop` コマンドとして使えるようにする。

## 目的

`Packages/src/GoCli~` の native Go CLI をビルド・検証し、現在の checkout の `uloop-dispatcher` を PATH 上の `uloop` として参照させる。

## 重要な前提

- 対象は `Packages/src/GoCli~`。古い `Packages/src/Cli~` や `npm link` は使わない。
- global に置く入口は `uloop-dispatcher`。`uloop-core` は project-local implementation なので直接 link しない。
- 既存の無関係な `uloop` 実体が通常ファイルとして存在する場合は、上書きせずユーザーに確認する。
- 検証なしで link 完了扱いにしない。

## 手順

### Step 1: Repository root を確認

```bash
git rev-parse --show-toplevel
git status --short --branch
```

`Packages/src/GoCli~/go.mod` と `scripts/check-go-cli.sh` が存在することを確認する。

### Step 2: Go CLI をビルド・検証

```bash
scripts/check-go-cli.sh
```

この script は Go CLI の formatting / vet / lint / tests / checked-in dist validation をまとめて確認する。失敗したら link に進まず、原因を報告する。

### Step 3: 現在の platform 用 dispatcher を選ぶ

```bash
uname -s
uname -m
```

macOS の対応:

- `Darwin` + `arm64` または `aarch64` -> `Packages/src/GoCli~/dist/darwin-arm64/uloop-dispatcher`
- `Darwin` + `x86_64` または `amd64` -> `Packages/src/GoCli~/dist/darwin-amd64/uloop-dispatcher`

対象 dispatcher が存在し、実行可能であることを確認する。

### Step 4: global bin directory を選ぶ

優先順位:

1. `ULOOP_GLOBAL_BIN_DIR` が設定されていればそれを使う
2. `command -v uloop` が成功するなら、その `uloop` が置かれている directory を使う
3. `$HOME/.npm-global/bin` が PATH に含まれる、または既存の `uloop` がそこにあるなら使う
4. `$HOME/.local/bin` が PATH に含まれるなら使う

どれも使えない場合は、link を作らず PATH に入っている bin directory を確認するよう報告する。

### Step 5: symlink を作る

選んだ directory に `uloop` symlink を作る。

安全ルール:

- 既存の `uloop` が symlink なら、現在の向き先を表示してから `ln -sfn` で更新する。
- 既存の `uloop` が通常ファイルまたは directory なら、上書きせずユーザーに確認する。
- `mkdir -p` してよいのは選択済みの global bin directory のみ。

実行例:

```bash
ln -sfn "$DISPATCHER_PATH" "$GLOBAL_BIN_DIR/uloop"
```

### Step 6: link 結果を確認

```bash
which uloop
readlink "$(which uloop)"
uloop --version
uloop --help
```

`readlink` の結果が現在の checkout の `Packages/src/GoCli~/dist/.../uloop-dispatcher` を指していることを確認する。

## 完了報告

次を短く報告する。

- `which uloop` の結果
- symlink の向き先
- `uloop --version`
- `scripts/check-go-cli.sh` の成否
- git 差分の有無

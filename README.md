# SpaceRunViewer

SpaceRunViewer は、テキストファイル内の連続した空白部分を検出して、開始桁と文字数を確認するための Windows 用ツールです。

固定長テキストのレイアウト確認で、空白部分を手作業で選択して桁位置や文字数を数える作業を減らすことを目的にしています。

## 主な機能

- テキストファイルを読み込み
- 半角スペース、全角スペース、タブを対象として選択可能
- 連続した空白を1つの空白ブロックとして検出
- 本文上で空白部分を色付き表示
- 桁位置が分かるルーラー表示
- 空白ブロックごとに一覧表示
  - 行番号
  - 空白No
  - 開始桁
  - 終了桁
  - 文字数
  - 種類
  - 前後の文字
- 一覧の行を選択すると、本文側の該当空白を強調
- Shift_JIS / UTF-8 に対応

## 画面イメージ

![SpaceRunViewer screenshot](assets/space-run-viewer.png)

## ダウンロード

最新版は GitHub Releases からダウンロードできます。

[SpaceRunViewer-v0.1.2-win-x64.zip をダウンロード](https://github.com/konikatsu/SpaceRunViewer/releases/download/v0.1.2/SpaceRunViewer-v0.1.2-win-x64.zip)

zip を展開して、`SpaceRunViewer.exe` を起動してください。

この配布版は軽量版です。実行する PC に .NET 8 Desktop Runtime が入っていない場合は、起動時に .NET のダウンロード案内が表示されます。

.NET 8 Desktop Runtime は Microsoft 公式サイトから入手できます。

[.NET 8 Desktop Runtime をダウンロード](https://dotnet.microsoft.com/download/dotnet/8.0)

## 使い方

1. ファイルを選択します。
2. 文字コードを選択します。
3. 対象にする空白種類を選択します。
4. `解析` ボタンを押します。
5. 下段の一覧で、空白の開始桁と文字数を確認します。

## 開発者向け

### ビルド

```powershell
dotnet build .\SpaceRunViewer.csproj
```

### 実行

```powershell
dotnet run --project .\SpaceRunViewer.csproj
```

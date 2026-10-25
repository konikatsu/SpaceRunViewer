using System.Text;

namespace SpaceRunViewer;

public sealed class MainForm : Form
{
    private const string AppVersion = "0.1.4";
    private const int LinePrefixLength = 8;
    private const int PaneMinimumHeight = 40;
    private const int InitialResultGridHeight = 104;

    private readonly TextBox filePathTextBox = new();
    private readonly ComboBox encodingComboBox = new();
    private readonly CheckBox halfSpaceCheckBox = new();
    private readonly CheckBox fullSpaceCheckBox = new();
    private readonly CheckBox tabCheckBox = new();
    private readonly CheckBox fixedLengthCheckBox = new();
    private readonly NumericUpDown recordByteLengthInput = new();
    private readonly Label summaryLabel = new();
    private readonly SyncRichTextBox rulerView = new();
    private readonly SyncRichTextBox textView = new();
    private readonly DataGridView resultGrid = new();
    private readonly RowStyle rulerRowStyle = new(SizeType.Absolute, 42);
    private readonly Color spaceBackColor = Color.FromArgb(255, 241, 170);
    private readonly Color selectedBackColor = Color.FromArgb(255, 197, 120);
    private readonly string[] startupArgs;

    private List<SpaceRun> currentRuns = [];
    private readonly List<int> lineStartPositions = [];
    private Font? gridZoomFont;
    private bool syncingZoom;

    public MainForm(string[]? args = null)
    {
        startupArgs = args ?? [];
        BuildLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyStartupArgs();
        SyncDisplayZoom(textView);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            gridZoomFont?.Dispose();
        }
    }

    private void BuildLayout()
    {
        Text = $"SpaceRunViewer v{AppVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 320);
        ClientSize = new Size(1200, 760);

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            ColumnCount = 7,
            RowCount = 3,
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        ConfigureFilePathTextBox();

        var selectButton = CreateButton("選択");
        selectButton.Click += (_, _) => SelectFile();

        var analyzeButton = CreateButton("解析");
        analyzeButton.Click += (_, _) => AnalyzeFile();

        encodingComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        encodingComboBox.Items.AddRange(["Shift_JIS", "UTF-8"]);
        encodingComboBox.SelectedIndex = 0;
        encodingComboBox.Width = 110;

        halfSpaceCheckBox.Text = "半角";
        halfSpaceCheckBox.Checked = true;
        halfSpaceCheckBox.AutoSize = true;

        fullSpaceCheckBox.Text = "全角";
        fullSpaceCheckBox.Checked = true;
        fullSpaceCheckBox.AutoSize = true;

        tabCheckBox.Text = "タブ";
        tabCheckBox.AutoSize = true;

        topPanel.Controls.Add(new Label { Text = "ファイル", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 7, 8, 4) }, 0, 0);
        topPanel.Controls.Add(filePathTextBox, 1, 0);
        topPanel.SetColumnSpan(filePathTextBox, 5);
        topPanel.Controls.Add(selectButton, 6, 0);
        AddLabeledControl(topPanel, "文字コード", encodingComboBox, 1);
        topPanel.Controls.Add(halfSpaceCheckBox, 2, 1);
        topPanel.Controls.Add(fullSpaceCheckBox, 3, 1);
        topPanel.Controls.Add(tabCheckBox, 4, 1);
        topPanel.Controls.Add(analyzeButton, 6, 1);

        fixedLengthCheckBox.Text = "固定長（改行なし）";
        fixedLengthCheckBox.AutoSize = true;
        fixedLengthCheckBox.Margin = new Padding(4, 7, 12, 4);
        recordByteLengthInput.Minimum = 1;
        recordByteLengthInput.Maximum = 1_000_000;
        recordByteLengthInput.Value = 109;
        recordByteLengthInput.Width = 110;
        recordByteLengthInput.ThousandsSeparator = true;
        var recordSizeOptions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Enabled = false,
        };
        recordSizeOptions.Controls.Add(new Label { Text = "1行のサイズ", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        recordSizeOptions.Controls.Add(recordByteLengthInput);
        recordSizeOptions.Controls.Add(new Label { Text = "バイト", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        fixedLengthCheckBox.CheckedChanged += (_, _) => recordSizeOptions.Enabled = fixedLengthCheckBox.Checked;
        var recordModePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(4, 0, 4, 0),
        };
        recordModePanel.Controls.Add(fixedLengthCheckBox);
        recordModePanel.Controls.Add(recordSizeOptions);
        topPanel.Controls.Add(recordModePanel, 0, 2);
        topPanel.SetColumnSpan(recordModePanel, 7);

        summaryLabel.Dock = DockStyle.Top;
        summaryLabel.AutoSize = false;
        summaryLabel.Height = 28;
        summaryLabel.Padding = new Padding(10, 6, 0, 0);
        summaryLabel.Text = "ファイルを選択して解析してください。";

        ConfigureRulerView();
        ConfigureTextView();
        ConfigureResultGrid();

        var textPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        textPanel.RowStyles.Add(rulerRowStyle);
        textPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        textPanel.Controls.Add(rulerView, 0, 0);
        textPanel.Controls.Add(textView, 0, 1);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Panel1MinSize = PaneMinimumHeight,
            Panel2MinSize = PaneMinimumHeight,
            SplitterWidth = 6,
        };
        splitContainer.Panel1.Controls.Add(textPanel);
        splitContainer.Panel2.Controls.Add(resultGrid);

        Controls.Add(splitContainer);
        Controls.Add(summaryLabel);
        Controls.Add(topPanel);

        Load += (_, _) => SetResultGridHeight(splitContainer, InitialResultGridHeight);
        SyncDisplayZoom(textView);
    }

    private static void SetResultGridHeight(SplitContainer splitContainer, int height)
    {
        var maxDistance = splitContainer.Height - splitContainer.SplitterWidth - splitContainer.Panel2MinSize;
        if (maxDistance < splitContainer.Panel1MinSize)
        {
            return;
        }

        var distance = splitContainer.Height - splitContainer.SplitterWidth - height;
        splitContainer.SplitterDistance = Math.Clamp(distance, splitContainer.Panel1MinSize, maxDistance);
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(4),
    };

    private static void AddLabeledControl(TableLayoutPanel panel, string labelText, Control control, int column)
    {
        var container = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(4, 0, 12, 4),
        };
        container.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        container.Controls.Add(control);
        panel.Controls.Add(container, column, 1);
    }

    private void ConfigureFilePathTextBox()
    {
        filePathTextBox.Dock = DockStyle.Fill;
        filePathTextBox.ReadOnly = true;
        filePathTextBox.MinimumSize = new Size(420, 0);
        filePathTextBox.Margin = new Padding(4);
    }

    private void ConfigureTextView()
    {
        textView.Dock = DockStyle.Fill;
        textView.Font = new Font("Consolas", 10);
        textView.ReadOnly = true;
        textView.WordWrap = false;
        textView.ScrollBars = RichTextBoxScrollBars.Both;
        textView.HideSelection = false;
        textView.DetectUrls = false;
        textView.ScrollChanged += (_, _) => SyncRulerScroll();
        textView.HScroll += (_, _) => SyncRulerScroll();
        textView.ZoomChanged += (_, _) => SyncDisplayZoom(textView);
    }

    private void ConfigureRulerView()
    {
        rulerView.Dock = DockStyle.Fill;
        rulerView.Font = new Font("Consolas", 10);
        rulerView.ReadOnly = true;
        rulerView.WordWrap = false;
        rulerView.ScrollBars = RichTextBoxScrollBars.None;
        rulerView.HideSelection = true;
        rulerView.DetectUrls = false;
        rulerView.BackColor = Color.FromArgb(245, 245, 245);
        rulerView.BorderStyle = BorderStyle.FixedSingle;
        rulerView.TabStop = false;
        rulerView.ZoomChanged += (_, _) => SyncDisplayZoom(rulerView);
    }

    private void ConfigureResultGrid()
    {
        resultGrid.Dock = DockStyle.Fill;
        resultGrid.AllowUserToAddRows = false;
        resultGrid.AllowUserToDeleteRows = false;
        resultGrid.AllowUserToResizeRows = false;
        resultGrid.ReadOnly = true;
        resultGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        resultGrid.MultiSelect = false;
        resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        resultGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
        resultGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        resultGrid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        resultGrid.RowHeadersVisible = false;
        resultGrid.Columns.Add("LineNumber", "行番号");
        resultGrid.Columns.Add("RunIndex", "空白No");
        resultGrid.Columns.Add("StartColumn", "開始桁");
        resultGrid.Columns.Add("EndColumn", "終了桁");
        resultGrid.Columns.Add("Length", "文字数");
        resultGrid.Columns.Add("Kind", "種類");
        resultGrid.Columns.Add("Preview", "前後の文字");
        resultGrid.SelectionChanged += (_, _) => HighlightSelectedRun();
    }

    private void SelectFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "空白を解析するテキストファイルを選択",
            Filter = "Text files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetFilePath(dialog.FileName);
        }
    }

    private void ApplyStartupArgs()
    {
        if (startupArgs.Length == 0)
        {
            return;
        }

        SetFilePath(startupArgs[0]);
        AnalyzeFile();
    }

    private void SetFilePath(string filePath)
    {
        filePathTextBox.Text = filePath;
        filePathTextBox.SelectionStart = filePathTextBox.TextLength;
        filePathTextBox.SelectionLength = 0;
        filePathTextBox.ScrollToCaret();
    }

    private void AnalyzeFile()
    {
        if (string.IsNullOrWhiteSpace(filePathTextBox.Text))
        {
            ShowWarning("ファイルを指定してください。");
            return;
        }

        if (!File.Exists(filePathTextBox.Text))
        {
            ShowWarning("ファイルが存在しません。");
            return;
        }

        if (!halfSpaceCheckBox.Checked && !fullSpaceCheckBox.Checked && !tabCheckBox.Checked)
        {
            ShowWarning("解析対象の空白種類を1つ以上選択してください。");
            return;
        }

        try
        {
            var encoding = GetSelectedEncoding();
            int? recordByteLength = fixedLengthCheckBox.Checked ? (int)recordByteLengthInput.Value : null;
            var result = TextFileReader.Read(filePathTextBox.Text, encoding, recordByteLength);
            var lines = result.Lines;
            currentRuns = AnalyzeLines(lines);
            var zoom = textView.ZoomFactor;
            RenderRuler(lines);
            RenderText(lines);
            // Replacing a RichEdit document can reset its native zoom to 100%.
            if (textView.ZoomFactor != zoom)
            {
                textView.ZoomFactor = zoom;
            }
            SyncDisplayZoom(textView);
            RenderGrid();
            var mode = recordByteLength is { } size ? $"固定長 {size:N0} バイト/行 / " : string.Empty;
            var partial = result.PartialRecordBytes > 0 ? $" / 末尾レコード {result.PartialRecordBytes:N0} バイト（指定サイズ未満）" : string.Empty;
            summaryLabel.Text = $"{mode}{lines.Length:N0} 行 / 空白箇所 {currentRuns.Count:N0} 件 / 空白文字数 {currentRuns.Sum(run => run.Length):N0} 文字{partial}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"読み込みに失敗しました。\n\n{ex.Message}", "SpaceRunViewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Encoding GetSelectedEncoding() =>
        encodingComboBox.SelectedItem?.ToString() == "UTF-8"
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            : Encoding.GetEncoding("shift_jis");

    private List<SpaceRun> AnalyzeLines(string[] lines)
    {
        var runs = new List<SpaceRun>();

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var runIndex = 1;
            var column = 0;

            while (column < line.Length)
            {
                if (!IsTargetSpace(line[column]))
                {
                    column++;
                    continue;
                }

                var start = column;
                var kind = GetSpaceKind(line[column]);
                column++;

                while (column < line.Length && IsTargetSpace(line[column]) && GetSpaceKind(line[column]) == kind)
                {
                    column++;
                }

                var length = column - start;
                runs.Add(new SpaceRun(
                    lineIndex + 1,
                    runIndex,
                    start + 1,
                    start + length,
                    length,
                    kind,
                    BuildPreview(line, start, length)));
                runIndex++;
            }
        }

        return runs;
    }

    private bool IsTargetSpace(char c) =>
        (halfSpaceCheckBox.Checked && c == ' ')
        || (fullSpaceCheckBox.Checked && c == '\u3000')
        || (tabCheckBox.Checked && c == '\t');

    private static string GetSpaceKind(char c) => c switch
    {
        ' ' => "半角",
        '\u3000' => "全角",
        '\t' => "タブ",
        _ => "空白",
    };

    private static string BuildPreview(string line, int start, int length)
    {
        var beforeStart = Math.Max(0, start - 8);
        var before = line.Substring(beforeStart, start - beforeStart);
        var afterStart = start + length;
        var afterLength = Math.Min(8, line.Length - afterStart);
        var after = afterLength > 0 ? line.Substring(afterStart, afterLength) : string.Empty;
        return $"{ToVisibleText(before)}[{length}]{ToVisibleText(after)}";
    }

    private void RenderText(string[] lines)
    {
        textView.SuspendLayout();
        textView.Clear();
        lineStartPositions.Clear();

        for (var i = 0; i < lines.Length; i++)
        {
            lineStartPositions.Add(textView.TextLength);
            textView.AppendText($"L{i + 1:000000} ");
            textView.AppendText(ToVisibleText(lines[i]));
            if (i < lines.Length - 1)
            {
                textView.AppendText(Environment.NewLine);
            }
        }

        foreach (var run in currentRuns)
        {
            SelectRunText(run);
            textView.SelectionBackColor = spaceBackColor;
        }

        textView.SelectionStart = 0;
        textView.SelectionLength = 0;
        textView.SelectionBackColor = textView.BackColor;
        textView.ResumeLayout();
    }

    private void RenderRuler(string[] lines)
    {
        var maxColumns = lines.Length == 0 ? 0 : lines.Max(line => line.Length);
        rulerView.Clear();
        rulerView.AppendText(BuildRulerText(maxColumns));
        rulerView.SelectionStart = 0;
    }

    private static string BuildRulerText(int columns)
    {
        var numberLine = new char[LinePrefixLength + columns];
        Array.Fill(numberLine, ' ');
        "Column".CopyTo(numberLine);

        for (var column = 10; column <= columns; column += 10)
        {
            var number = column.ToString();
            var start = LinePrefixLength + column - number.Length;
            for (var i = 0; i < number.Length && start + i < numberLine.Length; i++)
            {
                numberLine[start + i] = number[i];
            }
        }

        var scaleLine = new StringBuilder(LinePrefixLength + columns);
        scaleLine.Append(' ', LinePrefixLength);
        for (var column = 1; column <= columns; column++)
        {
            scaleLine.Append(column % 10 == 0 ? '0' : (char)('0' + column % 10));
        }

        return new string(numberLine) + Environment.NewLine + scaleLine;
    }

    private static string ToVisibleText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                ' ' => '·',
                '\u3000' => '□',
                '\t' => '→',
                _ => c,
            });
        }

        return builder.ToString();
    }

    private void RenderGrid()
    {
        resultGrid.Rows.Clear();
        foreach (var run in currentRuns)
        {
            var rowIndex = resultGrid.Rows.Add(run.LineNumber, run.RunIndex, run.StartColumn, run.EndColumn, run.Length, run.Kind, run.Preview);
            resultGrid.Rows[rowIndex].Tag = run;
        }
    }

    private void HighlightSelectedRun()
    {
        if (resultGrid.CurrentRow?.Tag is not SpaceRun run)
        {
            return;
        }

        foreach (var item in currentRuns)
        {
            SelectRunText(item);
            textView.SelectionBackColor = spaceBackColor;
        }

        SelectRunText(run);
        textView.SelectionBackColor = selectedBackColor;
        textView.ScrollToCaret();
        textView.SelectionLength = 0;
    }

    private void SelectRunText(SpaceRun run)
    {
        if (run.LineNumber < 1 || run.LineNumber > lineStartPositions.Count)
        {
            return;
        }

        var start = lineStartPositions[run.LineNumber - 1] + LinePrefixLength + run.StartColumn - 1;
        textView.Select(start, run.Length);
    }

    private void SyncDisplayZoom(SyncRichTextBox source)
    {
        if (syncingZoom)
        {
            return;
        }

        syncingZoom = true;
        try
        {
            var zoom = source.ZoomFactor;
            if (textView.ZoomFactor != zoom)
            {
                textView.ZoomFactor = zoom;
            }
            if (rulerView.ZoomFactor != zoom)
            {
                rulerView.ZoomFactor = zoom;
            }

            var gridFontSize = textView.Font.SizeInPoints * zoom;
            if (gridZoomFont is null || Math.Abs(gridZoomFont.SizeInPoints - gridFontSize) > 0.01f)
            {
                var previousFont = gridZoomFont;
                gridZoomFont = new Font(resultGrid.Font.FontFamily, gridFontSize, resultGrid.Font.Style);
                resultGrid.Font = gridZoomFont;
                previousFont?.Dispose();
            }

            // Keep both ruler lines visible as the native RichEdit zoom changes.
            var dpiScale = DeviceDpi / 96f;
            var lineHeight = (float)Math.Ceiling(rulerView.Font.GetHeight(DeviceDpi) * zoom);
            var verticalChrome = rulerView.Margin.Vertical + rulerView.Height - rulerView.ClientSize.Height;
            rulerRowStyle.Height = Math.Max(42 * dpiScale,
                2 * lineHeight + verticalChrome + (float)Math.Ceiling(4 * dpiScale));
            SyncRulerScroll();
        }
        finally
        {
            syncingZoom = false;
        }
    }

    private void SyncRulerScroll()
    {
        var position = textView.GetScrollPosition();
        position.Y = 0;
        rulerView.SetScrollPosition(position);
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "SpaceRunViewer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

public sealed record SpaceRun(
    int LineNumber,
    int RunIndex,
    int StartColumn,
    int EndColumn,
    int Length,
    string Kind,
    string Preview);

public sealed class SyncRichTextBox : RichTextBox
{
    private const int WmVScroll = 0x0115;
    private const int WmHScroll = 0x0114;
    private const int WmMouseWheel = 0x020A;
    private const int EmGetScrollPos = 0x04DD;
    private const int EmSetScrollPos = 0x04DE;
    private const int EmSetZoom = 0x04E1;
    private float lastZoomFactor = 1f;

    public event EventHandler? ScrollChanged;
    public event EventHandler? ZoomChanged;

    public Point GetScrollPosition()
    {
        var point = new Point();
        SendMessage(Handle, EmGetScrollPos, IntPtr.Zero, ref point);
        return point;
    }

    public void SetScrollPosition(Point point)
    {
        SendMessage(Handle, EmSetScrollPos, IntPtr.Zero, ref point);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg is EmSetZoom or WmMouseWheel)
        {
            var zoom = ZoomFactor;
            if (zoom != lastZoomFactor)
            {
                lastZoomFactor = zoom;
                ZoomChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        if (m.Msg is WmVScroll or WmHScroll or WmMouseWheel or EmSetScrollPos)
        {
            ScrollChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);
}

using System.Text;

namespace SpaceRunViewer;

public sealed class MainForm : Form
{
    private const int LinePrefixLength = 8;

    private readonly TextBox filePathTextBox = new();
    private readonly ComboBox encodingComboBox = new();
    private readonly CheckBox halfSpaceCheckBox = new();
    private readonly CheckBox fullSpaceCheckBox = new();
    private readonly CheckBox tabCheckBox = new();
    private readonly Label summaryLabel = new();
    private readonly SyncRichTextBox rulerView = new();
    private readonly SyncRichTextBox textView = new();
    private readonly DataGridView resultGrid = new();
    private readonly Color spaceBackColor = Color.FromArgb(255, 241, 170);
    private readonly Color selectedBackColor = Color.FromArgb(255, 197, 120);
    private readonly string[] startupArgs;

    private List<SpaceRun> currentRuns = [];
    private readonly List<int> lineStartPositions = [];

    public MainForm(string[]? args = null)
    {
        startupArgs = args ?? [];
        BuildLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyStartupArgs();
    }

    private void BuildLayout()
    {
        Text = "SpaceRunViewer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 560);
        ClientSize = new Size(1200, 760);

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            ColumnCount = 7,
            RowCount = 2,
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
        topPanel.Controls.Add(selectButton, 2, 0);
        AddLabeledControl(topPanel, "文字コード", encodingComboBox, 3);
        topPanel.Controls.Add(halfSpaceCheckBox, 4, 0);
        topPanel.Controls.Add(fullSpaceCheckBox, 5, 0);
        topPanel.Controls.Add(tabCheckBox, 6, 0);
        topPanel.Controls.Add(analyzeButton, 6, 1);

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
        textPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        textPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        textPanel.Controls.Add(rulerView, 0, 0);
        textPanel.Controls.Add(textView, 0, 1);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 420,
        };
        splitContainer.Panel1.Controls.Add(textPanel);
        splitContainer.Panel2.Controls.Add(resultGrid);

        Controls.Add(splitContainer);
        Controls.Add(summaryLabel);
        Controls.Add(topPanel);
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
            filePathTextBox.Text = dialog.FileName;
        }
    }

    private void ApplyStartupArgs()
    {
        if (startupArgs.Length == 0)
        {
            return;
        }

        filePathTextBox.Text = startupArgs[0];
        AnalyzeFile();
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
            var lines = File.ReadAllLines(filePathTextBox.Text, encoding);
            currentRuns = AnalyzeLines(lines);
            RenderRuler(lines);
            RenderText(lines);
            RenderGrid();
            summaryLabel.Text = $"{lines.Length:N0} 行 / 空白箇所 {currentRuns.Count:N0} 件 / 空白文字数 {currentRuns.Sum(run => run.Length):N0} 文字";
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

    public event EventHandler? ScrollChanged;

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

        if (m.Msg is WmVScroll or WmHScroll or WmMouseWheel)
        {
            ScrollChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);
}

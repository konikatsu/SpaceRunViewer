using System.Reflection;
using System.Text;
using SpaceRunViewer;

internal static class Program
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private static string testDirectory = string.Empty;

    [STAThread]
    private static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (args.Length == 3 && args[0] == "--preview")
        {
            RenderPreview(args[1], args[2]);
            return 0;
        }
        testDirectory = Path.Combine(Path.GetTempPath(), "SpaceRunViewer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        var tests = new (string Name, Action Run)[]
        {
            ("通常モードはCRLF・LFと空行を保持", NormalLines),
            ("109バイト2件を分離し末尾空白を保持", Fixed109),
            ("空ファイルは0件", EmptyFile),
            ("末尾の不足データを保持", PartialRecord),
            ("UTF-8のBOMはレコード長に含めない", Utf8Bom),
            ("UTF-8の日本語をバイト単位で分割", Utf8Multibyte),
            ("Shift_JISの日本語をバイト単位で分割", ShiftJisMultibyte),
            ("複数バイト文字の途中の境界を拒否", InvalidBoundary),
            ("固定長モードの改行を拒否", FixedNewlines),
            ("0・負のレコード長を拒否", InvalidSize),
            ("画面の固定長指定・行分離・空白選択", FormIntegration),
        };
        var failures = 0;
        try
        {
            foreach (var test in tests)
            {
                try { test.Run(); Console.WriteLine("PASS " + test.Name); }
                catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + test.Name + ": " + ex); }
            }
        }
        finally
        {
            var expectedPrefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "SpaceRunViewer-tests-");
            if (!Path.GetFullPath(testDirectory).StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("テスト用ディレクトリの削除対象が範囲外です。");
            Directory.Delete(testDirectory, true);
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private static string Write(byte[] bytes)
    {
        var path = Path.Combine(testDirectory, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string Write(string text) => Write(Utf8.GetBytes(text));
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; actual {actual}");
    }
    private static void Lines(string[] expected, TextFileReadResult actual)
    {
        if (!expected.SequenceEqual(actual.Lines)) throw new InvalidOperationException("レコード内容が一致しません。");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(typeof(T).Name + " was not thrown.");
    }

    private static void NormalLines() => Lines(["A  B", "", "C "], TextFileReader.Read(Write("A  B\r\n\r\nC \n"), Utf8));
    private static string[] FixedRecords() => [new string('A', 95) + new string(' ', 14), new string(' ', 10) + new string('B', 99)];

    private static void Fixed109()
    {
        var records = FixedRecords();
        var result = TextFileReader.Read(Write(string.Concat(records)), Utf8, 109);
        Lines(records, result);
        Equal(0, result.PartialRecordBytes);
    }

    private static void EmptyFile() => Lines([], TextFileReader.Read(Write(""), Utf8, 109));
    private static void PartialRecord()
    {
        var result = TextFileReader.Read(Write("AB CD"), Utf8, 3);
        Lines(["AB ", "CD"], result);
        Equal(2, result.PartialRecordBytes);
    }

    private static void Utf8Bom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Utf8.GetBytes("A B ")).ToArray();
        Lines(["A ", "B "], TextFileReader.Read(Write(bytes), Utf8, 2));
        Lines([], TextFileReader.Read(Write([0xEF, 0xBB, 0xBF]), Utf8, 2));
    }

    private static void Utf8Multibyte() => Lines(["あ  A", "B  い"], TextFileReader.Read(Write("あ  AB  い"), Utf8, 6));
    private static void ShiftJisMultibyte()
    {
        var encoding = Encoding.GetEncoding("shift_jis");
        Lines(["あ  ", "B い"], TextFileReader.Read(Write(encoding.GetBytes("あ  B い")), encoding, 4));
    }
    private static void InvalidBoundary() => Throws<InvalidDataException>(() => TextFileReader.Read(Write("あ  AB  い"), Utf8, 5));
    private static void FixedNewlines() => Throws<InvalidDataException>(() => TextFileReader.Read(Write("A\r\nB"), Utf8, 4));
    private static void InvalidSize()
    {
        var path = Write("A");
        Throws<ArgumentOutOfRangeException>(() => TextFileReader.Read(path, Utf8, 0));
        Throws<ArgumentOutOfRangeException>(() => TextFileReader.Read(path, Utf8, -1));
    }

    private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
    private static void Analyze(MainForm form) => typeof(MainForm).GetMethod("AnalyzeFile", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);

    private static void FormIntegration()
    {
        using var form = new MainForm();
        form.CreateControl();
        var mode = Field<CheckBox>(form, "fixedLengthCheckBox");
        var size = Field<NumericUpDown>(form, "recordByteLengthInput");
        Equal(false, mode.Checked);
        Equal(false, size.Enabled);
        Equal(109m, size.Value);
        Field<ComboBox>(form, "encodingComboBox").SelectedItem = "UTF-8";
        Field<TextBox>(form, "filePathTextBox").Text = Write(string.Concat(FixedRecords()));
        mode.Checked = true;
        Equal(true, size.Enabled);
        Analyze(form);
        var grid = Field<DataGridView>(form, "resultGrid");
        var body = Field<SyncRichTextBox>(form, "textView");
        Equal(2, grid.Rows.Count);
        Equal(1, (int)grid.Rows[0].Cells["LineNumber"].Value!);
        Equal(96, (int)grid.Rows[0].Cells["StartColumn"].Value!);
        Equal(14, (int)grid.Rows[0].Cells["Length"].Value!);
        Equal(2, (int)grid.Rows[1].Cells["LineNumber"].Value!);
        Equal(1, (int)grid.Rows[1].Cells["StartColumn"].Value!);
        Equal(10, (int)grid.Rows[1].Cells["Length"].Value!);
        if (!body.Text.Contains("L000002 ")) throw new InvalidOperationException("2件目の行がありません。");
        grid.CurrentCell = grid.Rows[1].Cells[0];
        Equal(1, grid.CurrentRow!.Index);
        typeof(MainForm).GetMethod("HighlightSelectedRun", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        var run = (SpaceRun)grid.Rows[1].Tag!;
        typeof(MainForm).GetMethod("SelectRunText", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [run]);
        Equal(new string('·', 10), body.SelectedText);
        Equal(Color.FromArgb(255, 197, 120).ToArgb(), body.SelectionBackColor.ToArgb());
        mode.Checked = false;
        Analyze(form);
        Equal(1, grid.Rows.Count);
        Equal(24, (int)grid.Rows[0].Cells["Length"].Value!);
        mode.Checked = true;
        size.Value = 3;
        Field<TextBox>(form, "filePathTextBox").Text = Write("AB CD");
        Analyze(form);
        if (!Field<Label>(form, "summaryLabel").Text.Contains("末尾レコード 2 バイト")) throw new InvalidOperationException("不足サイズの表示がありません。");
    }

    private static void RenderPreview(string input, string output)
    {
        Application.EnableVisualStyles();
        using var form = new MainForm();
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000);
        form.ShowInTaskbar = false;
        form.Show();
        Field<TextBox>(form, "filePathTextBox").Text = input;
        Field<ComboBox>(form, "encodingComboBox").SelectedItem = "UTF-8";
        Field<CheckBox>(form, "fixedLengthCheckBox").Checked = true;
        Analyze(form);
        Application.DoEvents();
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(output, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
    }
}

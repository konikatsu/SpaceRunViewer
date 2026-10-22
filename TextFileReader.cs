using System.Text;

namespace SpaceRunViewer;

public sealed record TextFileReadResult(string[] Lines, int PartialRecordBytes = 0);

public static class TextFileReader
{
    public static TextFileReadResult Read(string path, Encoding encoding, int? recordByteLength = null)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        if (recordByteLength is null)
        {
            return new TextFileReadResult(File.ReadAllLines(path, encoding));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recordByteLength.Value);
        var bytes = File.ReadAllBytes(path);
        var offset = 0;
        if (encoding.CodePage == Encoding.UTF8.CodePage
            && bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            offset = 3;
        }

        var contentLength = bytes.Length - offset;
        if (contentLength == 0)
        {
            return new TextFileReadResult([]);
        }

        var strictEncoding = (Encoding)encoding.Clone();
        strictEncoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        var recordLength = recordByteLength.Value;
        var lines = new string[(contentLength - 1) / recordLength + 1];
        for (var index = 0; offset < bytes.Length; index++)
        {
            var count = Math.Min(recordLength, bytes.Length - offset);
            try
            {
                lines[index] = strictEncoding.GetString(bytes, offset, count);
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException(
                    $"{index + 1:N0}件目のレコードを読み込めません。文字コードと1行のサイズを確認してください。複数バイトの文字の途中で区切られている可能性があります。", ex);
            }

            if (lines[index].IndexOfAny(['\r', '\n']) >= 0)
            {
                throw new InvalidDataException("固定長モードは改行なしのファイル用です。改行のあるファイルは「固定長（改行なし）」をオフにして解析してください。");
            }

            offset += count;
        }

        return new TextFileReadResult(lines, contentLength % recordLength);
    }
}

using System.Text;

namespace CcmWeb.Helpers;

/// <summary>
/// Sinh nội dung CSV cho ba nút "Xuất CSV" của trang báo cáo. Hàm thuần, không đọc cơ sở dữ liệu,
/// nên kiểm thử được bằng dữ liệu dựng sẵn.
///
/// Hai quy tắc quan trọng:
/// - Một ô có dấu phẩy, dấu nháy kép hoặc xuống dòng phải được bọc trong nháy kép và nhân đôi dấu
///   nháy bên trong, nếu không tệp sẽ lệch cột.
/// - Ô bắt đầu bằng <c>= + - @</c> bị chèn dấu <c>'</c> phía trước: Excel coi đó là công thức, và một
///   tên khách hàng như <c>=HYPERLINK(...)</c> sẽ chạy khi người dùng mở tệp.
/// </summary>
public static class CsvWriter
{
    /// <summary>Ký tự đầu tiên khiến Excel hiểu ô là công thức.</summary>
    private static readonly char[] FormulaLeaders = ['=', '+', '-', '@'];

    /// <summary>Nội dung CSV: từng dòng là các ô, ngăn cách bằng dấu phẩy, xuống dòng CRLF.</summary>
    public static string Build(IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();

        foreach (var row in rows)
        {
            builder.AppendJoin(',', row.Select(Escape));
            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Nội dung CSV dưới dạng UTF-8 có BOM, để Excel trên Windows nhận đúng tiếng Việt. BOM chỉ nằm
    /// ở đầu tệp, không nằm trong chuỗi CSV mà kiểm thử so sánh.
    /// </summary>
    public static byte[] ToUtf8WithBom(string csv)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        return encoding.GetPreamble().Concat(encoding.GetBytes(csv)).ToArray();
    }

    /// <summary>Một ô đã an toàn: chặn công thức trước, sau đó bọc nháy kép nếu cần.</summary>
    public static string Escape(string? value)
    {
        var text = value ?? string.Empty;

        if (text.Length > 0 && FormulaLeaders.Contains(text[0]))
        {
            text = "'" + text;
        }

        var needsQuotes = text.Contains(',') || text.Contains('"')
            || text.Contains('\n') || text.Contains('\r');

        return needsQuotes ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}

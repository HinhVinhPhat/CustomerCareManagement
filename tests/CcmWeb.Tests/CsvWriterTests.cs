using CcmWeb.Helpers;

namespace CcmWeb.Tests;

/// <summary>
/// Sinh nội dung CSV (M05): bọc nháy kép, nhân đôi nháy, chặn công thức Excel.
/// </summary>
public class CsvWriterTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-1", "'-1")]
    [InlineData("@mention", "'@mention")]
    [InlineData(null, "")]
    public void Escape_makes_a_cell_safe(string? input, string expected)
    {
        Assert.Equal(expected, CsvWriter.Escape(input));
    }

    [Fact]
    public void Build_joins_cells_and_ends_each_row_with_crlf()
    {
        var csv = CsvWriter.Build(
        [
            new[] { "a", "b" },
            new[] { "c", "d" }
        ]);

        Assert.Equal("a,b\r\nc,d\r\n", csv);
    }

    [Fact]
    public void Build_handles_null_cells()
    {
        var csv = CsvWriter.Build([[null, "x"]]);

        Assert.Equal(",x\r\n", csv);
    }

    [Fact]
    public void ToUtf8WithBom_starts_with_the_utf8_bom()
    {
        var bytes = CsvWriter.ToUtf8WithBom("abc");

        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }
}
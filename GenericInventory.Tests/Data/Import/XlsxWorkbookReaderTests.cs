using System.IO.Compression;
using GenericInventory.Data.Import;

namespace GenericInventory.Tests.Data.Import;

public class XlsxWorkbookReaderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ReadRows_ShouldReadSharedStringsAndNumericCells()
    {
        var workbook = CreateWorkbook();
        var reader = new XlsxWorkbookReader();

        var rows = reader.ReadRows(workbook, "CadProdutos");

        var row = Assert.Single(rows);
        Assert.Equal("P001", row.Get("CodigoProduto"));
        Assert.Equal("Tinta teste", row.Get("DescricaoProduto"));
        Assert.Equal("4", row.Get("EstoqueAtual"));
    }

    private static byte[] CreateWorkbook()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="CadProdutos" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);
            Write(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            Write(archive, "xl/sharedStrings.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <si><t>CodigoProduto</t></si>
                  <si><t>DescricaoProduto</t></si>
                  <si><t>EstoqueAtual</t></si>
                  <si><t>P001</t></si>
                  <si><t>Tinta teste</t></si>
                </sst>
                """);
            Write(archive, "xl/worksheets/sheet1.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="s"><v>0</v></c>
                      <c r="B1" t="s"><v>1</v></c>
                      <c r="C1" t="s"><v>2</v></c>
                    </row>
                    <row r="2">
                      <c r="A2" t="s"><v>3</v></c>
                      <c r="B2" t="s"><v>4</v></c>
                      <c r="C2"><v>4</v></c>
                    </row>
                  </sheetData>
                </worksheet>
                """);
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}

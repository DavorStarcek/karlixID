using ClosedXML.Excel;
using KarlixID.Web.Models;

namespace KarlixID.Web.Services
{
    public class ExcelExportService
    {
        public byte[] ExportUsers(List<ApplicationUser> users)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Korisnici");

            ws.Cell(1, 1).Value = "Korisničko ime";
            ws.Cell(1, 2).Value = "Email";
            ws.Cell(1, 3).Value = "Tenant ID";

            for (int i = 0; i < users.Count; i++)
            {
                ws.Cell(i + 2, 1).Value = users[i].UserName;
                ws.Cell(i + 2, 2).Value = users[i].Email;
                ws.Cell(i + 2, 3).Value = users[i].TenantId.ToString();
            }

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }
    }
}

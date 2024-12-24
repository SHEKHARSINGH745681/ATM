using ATM.Data;
using ATM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using OfficeOpenXml;

namespace ATM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportExcels : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ImportExcels(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("ImportExcel")]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null)
            {
                return BadRequest("Please upload an Excel file.");
            }

            var allowedExtensions = new[] { ".xls", ".xlsx" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest("Only Excel files (.xls, .xlsx) are allowed.");
            }

            var dataList = new List<ImportExcel>();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var name = worksheet.Cells[row, 1].Text.Trim();
                        var age = worksheet.Cells[row, 2].Text.Trim();
                        var pincode = worksheet.Cells[row, 3].Text.Trim();

                        bool exists = _context.importExcels.Any(e => e.Name == name && e.Age == age && e.PINCODE == pincode);
                        if (!exists)
                        {
                            var entity = new ImportExcel
                            {
                                Name = name,
                                Age = age,
                                PINCODE = pincode
                            };

                            dataList.Add(entity);
                        }
                    }
                }
            }

            if (dataList.Any())
            {
                await _context.AddRangeAsync(dataList);
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = "Data imported successfully.", Count = dataList.Count });
        }
    }
}
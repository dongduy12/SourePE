using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using System.Net.Http;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace API_WEB.Controllers.MaterialSystem
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaterialSystemController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;

        // DTO cho API AddMaterial
        public class AddMaterialRequest
        {
            public string MaAll { get; set; }
            public string MaLieu { get; set; }
            public string NhaCungUng { get; set; }
            public string DateCode { get; set; }
            public string LotCode { get; set; }
            public int Qty { get; set; }
            public string OP { get; set; }
            public string ESD { get; set; }
            public string Location { get; set; }
            public string Remark { get; set; }
        }

        // DTO cho API ExportDefectiveMaterial
        public class ExportDefectiveMaterialRequest
        {
            public string MaLieu { get; set; }
            public string NhaCungUng { get; set; }
            public string DateCode { get; set; }
            public string LotCode { get; set; }
            public int Qty { get; set; }
            public string OP { get; set; }
            public string Remark { get; set; }
        }

        // DTO cho API BorrowMaterial
        public class BorrowMaterialRequest
        {
            public string MaLieu { get; set; }
            public string NhaCungUng { get; set; }
            public string DateCode { get; set; }
            public string LotCode { get; set; }
            public int QtyOK { get; set; }
            public int QtyNG { get; set; }
            public string OP { get; set; }
            public string OPborrow { get; set; }
        }

        // DTO cho API ReturnMaterial
        public class ReturnMaterialRequest
        {
            public string Task { get; set; }
            public string MaLieu { get; set; }
            public string NhaCungUng { get; set; }
            public string DateCode { get; set; }
            public string LotCode { get; set; }
            public int QtyOK { get; set; }
            public int QtyNG { get; set; }
            public string OP { get; set; }
            public string Remark { get; set; }
        }

        public class RequestType
        {
            public string Type { get; set; }
        }
        public class SearchByMaLieuRequest
        {
            public string MaLieu { get; set; }
        }

        public MaterialSystemController(CSDL_NE sqlContext, OracleDbContext oracleContext, IHttpClientFactory httpClientFactory)
        {
            _sqlContext = sqlContext;
        }

        [HttpPost("AddMaterial")]
        public async Task<IActionResult> AddMaterial([FromBody] AddMaterialRequest request)
        {
            try
            {
                var existingInputRecord = await _sqlContext.HistoryMaterials.FirstOrDefaultAsync(m => m.MA_ALL == request.MaAll && m.TYPE == "Input");
                if(existingInputRecord != null)
                {
                    return BadRequest(new { message = "Tem Allpart đã tồn tại. Không thể thêm mới!" });
                }

                // 1. Tạo giá trị cho TASK
                var now = DateTime.Now;
                string datePrefix = now.ToString("yyyyMMdd");

                int taskCount = await _sqlContext.HistoryMaterials
                    .CountAsync(hm => hm.TASK.StartsWith(datePrefix));

                string taskNumber = taskCount.ToString("D3");
                string taskValue = $"{datePrefix}{taskNumber}";

                // 2. Lưu vào bảng HistoryMaterial
                var historyMaterial = new HistoryMaterial
                {
                    MA_ALL = request.MaAll,
                    MA_LIEU = request.MaLieu ?? "",
                    NHA_CUNG_UNG = request.NhaCungUng ?? "",
                    DATE_CODE = request.DateCode ?? "",
                    LOT_CODE = request.LotCode ?? "",
                    QTY1 = request.Qty,
                    OP1 = request.OP ?? "",
                    ESD = request.ESD ?? "",
                    LOCATION = request.Location,
                    REMARK = request.Remark,
                    TASK = taskValue,
                    TYPE = "Input",
                    TIME1 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                _sqlContext.HistoryMaterials.Add(historyMaterial);
                await _sqlContext.SaveChangesAsync();

                // 3. Xử lý bảng SumMaterial
                var existingSumMaterial = await _sqlContext.SumMaterials
                    .FirstOrDefaultAsync(sm =>
                        sm.MA_LIEU == (request.MaLieu ?? "") &&
                        sm.NHA_CUNG_UNG == (request.NhaCungUng ?? "") &&
                        sm.DATE_CODE == (request.DateCode ?? "") &&
                        sm.LOT_CODE == (request.LotCode ?? ""));

                if (existingSumMaterial is null)
                {
                    var newSumMaterial = new SumMaterial
                    {
                        MA_LIEU = request.MaLieu ?? "",
                        NHA_CUNG_UNG = request.NhaCungUng ?? "",
                        DATE_CODE = request.DateCode ?? "",
                        LOT_CODE = request.LotCode ?? "",
                        TONG_LINH = request.Qty,
                        SO_LUONG_OK = request.Qty,
                        SO_LUONG_NG = 0,
                        CHO_MUON = 0,
                        DA_BAO_PHE = 0,
                        ESD = request.ESD ?? "",
                        LOCATION = request.Location,
                        REMARK = request.Remark
                    };

                    _sqlContext.SumMaterials.Add(newSumMaterial);
                }
                else
                {
                    existingSumMaterial.TONG_LINH += request.Qty;
                    existingSumMaterial.SO_LUONG_OK += request.Qty;
                    existingSumMaterial.REMARK = request.Remark;
                }

                await _sqlContext.SaveChangesAsync();

                return Ok(new { message = "Material added successfully.", task = taskValue });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while adding material.", error = ex.Message });
            }
        }

        [HttpPost("ExportDefectiveMaterial")]
        public async Task<IActionResult> ExportDefectiveMaterial([FromBody] ExportDefectiveMaterialRequest request)
        {
            try
            {
                using var transaction = await _sqlContext.Database.BeginTransactionAsync();
                try
                {
                    var now = DateTime.Now;
                    string datePrefix = now.ToString("yyyyMMdd");

                    if (string.IsNullOrEmpty(datePrefix))
                    {
                        return BadRequest(new { message = "Invalid date prefix for TASK generation." });
                    }

                    int taskCount = await _sqlContext.HistoryMaterials
                        .Where(hm => hm.TASK.StartsWith(datePrefix))
                        .CountAsync();

                    string taskNumber = taskCount.ToString("D3");
                    string taskValue = $"{datePrefix}{taskNumber}";

                    var historyMaterial = new HistoryMaterial
                    {
                        MA_LIEU = request.MaLieu ?? "",
                        NHA_CUNG_UNG = request.NhaCungUng ?? "",
                        DATE_CODE = request.DateCode ?? "",
                        LOT_CODE = request.LotCode ?? "",
                        QTY1 = request.Qty,
                        OP1 = request.OP ?? "",
                        REMARK = request.Remark,
                        TASK = taskValue,
                        TYPE = "DefectiveExport",
                        TIME1 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ESD = "N/A"
                    };

                    _sqlContext.HistoryMaterials.Add(historyMaterial);
                    await _sqlContext.SaveChangesAsync();

                    var existingSumMaterial = await _sqlContext.SumMaterials
                        .FirstOrDefaultAsync(sm =>
                            sm.MA_LIEU == (request.MaLieu ?? "") &&
                            sm.NHA_CUNG_UNG == (request.NhaCungUng ?? "") &&
                            sm.DATE_CODE == (request.DateCode ?? "") &&
                            sm.LOT_CODE == (request.LotCode ?? ""));

                    if (existingSumMaterial is null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = "No matching record found in SumMaterial for the given MaLieu, NhaCungUng, DateCode, and LotCode." });
                    }

                    int newSoLuongNG = existingSumMaterial.SO_LUONG_NG - request.Qty;
                    if (newSoLuongNG < 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            message = "Invalid operation: SO_LUONG_NG cannot be negative.",
                            currentSoLuongNG = existingSumMaterial.SO_LUONG_NG,
                            requestedQty = request.Qty
                        });
                    }

                    existingSumMaterial.SO_LUONG_NG = newSoLuongNG;
                    existingSumMaterial.DA_BAO_PHE += request.Qty;

                    await _sqlContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "Defective material exported successfully.", task = taskValue });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new { message = "An error occurred while exporting defective material.", error = ex.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while exporting defective material.", error = ex.Message });
            }
        }

        [HttpPost("BorrowMaterial")]
        public async Task<IActionResult> BorrowMaterial([FromBody] BorrowMaterialRequest request)
        {
            try
            {
                // Kiểm tra đầu vào
                if (request.QtyOK < 0)
                {
                    return BadRequest(new
                    {
                        message = "Invalid operation: QtyOK cannot be negative.",
                        requestedQtyOK = request.QtyOK
                    });
                }

                if (request.QtyNG < 0)
                {
                    return BadRequest(new
                    {
                        message = "Invalid operation: QtyNG cannot be negative.",
                        requestedQtyNG = request.QtyNG
                    });
                }

                if (request.QtyOK + request.QtyNG == 0)
                {
                    return BadRequest(new
                    {
                        message = "Invalid operation: Total borrowed quantity (QtyOK + QtyNG) cannot be zero.",
                        requestedQtyOK = request.QtyOK,
                        requestedQtyNG = request.QtyNG
                    });
                }

                using var transaction = await _sqlContext.Database.BeginTransactionAsync();
                try
                {
                    var now = DateTime.Now;
                    string datePrefix = now.ToString("yyyyMMdd");

                    if (string.IsNullOrEmpty(datePrefix))
                    {
                        return BadRequest(new { message = "Invalid date prefix for TASK generation." });
                    }

                    int taskCount = await _sqlContext.HistoryMaterials
                        .Where(hm => hm.TASK.StartsWith(datePrefix))
                        .CountAsync();

                    string taskNumber = taskCount.ToString("D3");
                    string taskValue = $"{datePrefix}{taskNumber}";

                    var historyMaterial = new HistoryMaterial
                    {
                        MA_LIEU = request.MaLieu ?? "",
                        NHA_CUNG_UNG = request.NhaCungUng ?? "",
                        DATE_CODE = request.DateCode ?? "",
                        LOT_CODE = request.LotCode ?? "",
                        QTY1 = request.QtyOK,
                        QTY2 = request.QtyNG,
                        OP1 = request.OP ?? "",
                        OP2 = request.OPborrow,
                        TASK = taskValue,
                        TYPE = "Borrow",
                        TIME1 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ESD = "N/A"
                    };

                    _sqlContext.HistoryMaterials.Add(historyMaterial);
                    await _sqlContext.SaveChangesAsync();

                    var existingSumMaterial = await _sqlContext.SumMaterials
                        .FirstOrDefaultAsync(sm =>
                            sm.MA_LIEU == (request.MaLieu ?? "") &&
                            sm.NHA_CUNG_UNG == (request.NhaCungUng ?? "") &&
                            sm.DATE_CODE == (request.DateCode ?? "") &&
                            sm.LOT_CODE == (request.LotCode ?? ""));

                    if (existingSumMaterial is null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = "No matching record found in SumMaterial for the given MaLieu, NhaCungUng, DateCode, and LotCode." });
                    }

                    int newSoLuongOK = existingSumMaterial.SO_LUONG_OK - request.QtyOK;
                    int newSoLuongNG = existingSumMaterial.SO_LUONG_NG - request.QtyNG;

                    if (newSoLuongOK < 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            message = "Invalid operation: SO_LUONG_OK cannot be negative.",
                            currentSoLuongOK = existingSumMaterial.SO_LUONG_OK,
                            requestedQtyOK = request.QtyOK
                        });
                    }

                    if (newSoLuongNG < 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            message = "Invalid operation: SO_LUONG_NG cannot be negative.",
                            currentSoLuongNG = existingSumMaterial.SO_LUONG_NG,
                            requestedQtyNG = request.QtyNG
                        });
                    }
                    /*if (newSoLuongOK + newSoLuongNG == 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            message = "Invalid operation: Total remaining quantity (SO_LUONG_OK + SO_LUONG_NG) cannot be zero.",
                            currentSoLuongOK = existingSumMaterial.SO_LUONG_OK,
                            currentSoLuongNG = existingSumMaterial.SO_LUONG_NG,
                            requestedQtyOK = request.QtyOK,
                            requestedQtyNG = request.QtyNG
                        });
                    }*/
                    existingSumMaterial.SO_LUONG_OK = newSoLuongOK;
                    existingSumMaterial.SO_LUONG_NG = newSoLuongNG;
                    existingSumMaterial.CHO_MUON += (request.QtyOK + request.QtyNG);

                    await _sqlContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "Material borrowed successfully.", task = taskValue });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new { message = "An error occurred while borrowing material.", error = ex.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while borrowing material.", error = ex.Message });
            }
        }

        [HttpPost("ReturnMaterial")]
        public async Task<IActionResult> ReturnMaterial([FromBody] ReturnMaterialRequest request)
        {
            try
            {
                using var transaction = await _sqlContext.Database.BeginTransactionAsync();
                try
                {
                    var historyMaterial = await _sqlContext.HistoryMaterials
                        .FirstOrDefaultAsync(hm => hm.TASK == (request.Task ?? ""));

                    if (historyMaterial is null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = "No matching record found in HistoryMaterial for the given Task." });
                    }

                    int? borrowedQtyOK = historyMaterial.QTY1 ?? 0; // Sử dụng QTY3 thay vì QTY1
                    int? borrowedQtyNG = historyMaterial.QTY2 ?? 0; // Sử dụng QTY4 thay vì QTY2
                    int borrowedTotal = borrowedQtyOK.Value + borrowedQtyNG.Value;

                    int returnedTotal = request.QtyOK + request.QtyNG;

                    if (borrowedTotal != returnedTotal)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            message = "Invalid operation: Total returned quantity must match the total borrowed quantity.",
                            borrowedTotal = borrowedTotal,
                            returnedTotal = returnedTotal
                        });
                    }

                    historyMaterial.QTY3 = request.QtyOK; // Gán QtyOK vào QTY3
                    historyMaterial.QTY4 = request.QtyNG; // Gán QtyNG vào QTY4
                    historyMaterial.TIME2 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    historyMaterial.OP2 = request.OP;
                    historyMaterial.REMARK = request.Remark;

                    await _sqlContext.SaveChangesAsync();

                    var existingSumMaterial = await _sqlContext.SumMaterials
                        .FirstOrDefaultAsync(sm =>
                            sm.MA_LIEU == (request.MaLieu ?? "") &&
                            sm.NHA_CUNG_UNG == (request.NhaCungUng ?? "") &&
                            sm.DATE_CODE == (request.DateCode ?? "") &&
                            sm.LOT_CODE == (request.LotCode ?? ""));

                    if (existingSumMaterial is null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = "No matching record found in SumMaterial for the given MaLieu, NhaCungUng, DateCode, and LotCode." });
                    }

                    int newChoMuon = existingSumMaterial.CHO_MUON - (request.QtyOK + request.QtyNG);
                    if (newChoMuon < 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            message = "Invalid operation: CHO_MUON cannot be negative.",
                            currentChoMuon = existingSumMaterial.CHO_MUON,
                            requestedQtyOK = request.QtyOK,
                            requestedQtyNG = request.QtyNG
                        });
                    }

                    existingSumMaterial.SO_LUONG_OK += request.QtyOK;
                    existingSumMaterial.SO_LUONG_NG += request.QtyNG;
                    existingSumMaterial.CHO_MUON = newChoMuon;

                    await _sqlContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "Material returned successfully." });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new { message = "An error occurred while returning material.", error = ex.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while returning material.", error = ex.Message });
            }
        }

        [HttpGet("GetAllSumMaterials")]
        public async Task<IActionResult> GetAllSumMaterials()
        {
            try
            {
                var sumMaterials = await _sqlContext.SumMaterials
                    .ToListAsync();

                return Ok(sumMaterials);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving SumMaterial data.", error = ex.Message });
            }
        }

        [HttpPost("GetHistory")]
        public async Task<IActionResult> getHistory([FromBody] RequestType type)
        {
            try
            {
                var transactions = string.IsNullOrEmpty(type?.Type)
                    ?await _sqlContext.HistoryMaterials.ToListAsync()
                    :await _sqlContext.HistoryMaterials.Where(t => t.TYPE == type.Type).ToListAsync();
                return Ok(transactions);
            }catch(Exception ex)
            {
                return StatusCode(500, new {message = "Error", error = ex.Message});
            }
        }

        [HttpPost("SearchByMaLieu")]
        public async Task<IActionResult> SearchByMaLieu([FromBody] SearchByMaLieuRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.MaLieu))
                {
                    return BadRequest(new { message = "MaLieu is required." });
                }

                var sumMaterials = await _sqlContext.SumMaterials
                    .Where(sm => sm.MA_LIEU == request.MaLieu)
                    .ToListAsync();

                if (!sumMaterials.Any())
                {
                    return NotFound(new { message = "No records found for the given MaLieu." });
                }

                return Ok(sumMaterials);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while searching for materials.", error = ex.Message });
            }
        }
    }
}
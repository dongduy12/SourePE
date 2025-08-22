using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Microsoft.EntityFrameworkCore;
using API_WEB.ModelsOracle;
using API_WEB.ModelsDB;

namespace API_WEB.Controllers.SmartFA
{
    [ApiController]
    [Route("api/[controller]")]
    public class CheckInOutController : ControllerBase
    {
        private readonly OracleDbContext _oracleContext;
        private readonly CSDL_NE _sqlContext;
        public CheckInOutController(OracleDbContext oracleContext, CSDL_NE sqlContext)
        {
            _oracleContext = oracleContext;
            _sqlContext = sqlContext;
        }

        // Use a distinct request model name to avoid Swagger schema clashes
        public class CheckInOutRequest
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        public class CheckInRecord
        {
            public string SERIAL_NUMBER { get; set; } = string.Empty;
            public string MO_NUMBER { get; set; } = string.Empty;
            public string MODEL_NAME { get; set; } = string.Empty;
            public string P_SENDER { get; set; } = string.Empty;
            public string STATION_NAME { get; set; } = string.Empty;
            public string ERROR_CODE { get; set; } = string.Empty;
            public DateTime? IN_DATETIME { get; set; }
            public string ERROR_DESC { get; set; } = string.Empty;
        }

        public class CheckOutRecord
        {
            public string SERIAL_NUMBER { get; set; } = string.Empty;
            public string MODEL_NAME { get; set; } = string.Empty;
            public string PRODUCT_LINE { get; set; } = string.Empty;
            public string P_SENDER { get; set; } = string.Empty;
            public string REPAIRER { get; set; } = string.Empty;
            public string STATION_NAME { get; set; } = string.Empty;
            public DateTime? IN_DATETIME { get; set; }
            public DateTime? OUT_DATETIME { get; set; }
            public string ERROR_CODE { get; set; } = string.Empty;
            public string ERROR_DESC { get; set; } = string.Empty;
            public string CHECKIN_STATUS { get; set; } = string.Empty;
        }

        [HttpPost("GetCheckInOut")]
        public async Task<IActionResult> GetCheckInOut([FromBody] CheckInOutRequest request)
        {
            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            try
            {
                await connection.OpenAsync();

                var checkInQuery = @"SELECT
                        CASE
                        WHEN REGEXP_LIKE(a.MODEL_NAME, '^(900|692|699)')
                                 AND EXISTS (
                                     SELECT 1
                                     FROM SFISM4.R109 d
                                     WHERE d.SERIAL_NUMBER = a.SERIAL_NUMBER
                                       AND d.REASON_CODE LIKE '%B36R%'
                                 )
                            THEN COALESCE(pkp.KEY_PART_SN, a.SERIAL_NUMBER)
                            ELSE a.SERIAL_NUMBER
                        END AS SERIAL_NUMBER,
                        a.MO_NUMBER,
                        a.MODEL_NAME,
                        a.STATION_NAME,
                        a.P_SENDER,
                        b.PRODUCT_LINE,
                        a.REMARK AS ERROR_CODE,
                        a.IN_DATETIME,
                        c.ERROR_DESC
                    FROM SFISM4.R_REPAIR_IN_OUT_T a
                    INNER JOIN SFIS1.C_MODEL_DESC_T b
                        ON a.MODEL_NAME = b.MODEL_NAME
                    INNER JOIN SFIS1.C_ERROR_CODE_T c
                        ON a.REMARK = c.ERROR_CODE
                    LEFT JOIN (
                        SELECT SERIAL_NUMBER, MAX(KEY_PART_SN) AS KEY_PART_SN
                        FROM SFISM4.P_WIP_KEYPARTS_T
                        GROUP BY SERIAL_NUMBER
                    ) pkp
                        ON pkp.SERIAL_NUMBER = a.SERIAL_NUMBER
                    WHERE b.MODEL_SERIAL = 'ADAPTER'
                      AND a.P_SENDER IN ('V0904136', 'V0945375', 'V0928908', 'V3245384', 'V3211693')
                      AND a.STATION_NAME <> 'REPAIR_B36R'
                      AND a.IN_DATETIME BETWEEN :startDate AND :endDate
                      AND (
                            NOT REGEXP_LIKE(a.MODEL_NAME, '^(900|692|699)')
                            OR EXISTS (
                                SELECT 1
                                FROM SFISM4.R109 d
                                WHERE d.SERIAL_NUMBER = a.SERIAL_NUMBER
                                  AND d.REASON_CODE LIKE '%B36R%'
                            )
                          )";
                var checkInList = new List<CheckInRecord>();
                await using (var cmd = new OracleCommand(checkInQuery, connection))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(new OracleParameter(":startDate", OracleDbType.Date) { Value = request.StartDate });
                    cmd.Parameters.Add(new OracleParameter(":endDate", OracleDbType.Date) { Value = request.EndDate });
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        checkInList.Add(new CheckInRecord
                        {
                            SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString() ?? string.Empty,
                            MO_NUMBER = reader["MO_NUMBER"].ToString() ?? string.Empty,
                            P_SENDER = reader["P_SENDER"].ToString() ?? string.Empty,
                            MODEL_NAME = reader["MODEL_NAME"].ToString() ?? string.Empty,
                            STATION_NAME = reader["STATION_NAME"].ToString() ?? string.Empty,
                            ERROR_CODE = reader["ERROR_CODE"].ToString() ?? string.Empty,
                            IN_DATETIME = reader["IN_DATETIME"] as DateTime?,
                            ERROR_DESC = reader["ERROR_DESC"].ToString() ?? string.Empty
                        });
                    }
                }

                var checkOutQuery = @"SELECT a.SERIAL_NUMBER,
                                            a.MODEL_NAME,
                                            b.PRODUCT_LINE,
                                            a.P_SENDER,
                                            a.REPAIRER,
                                            a.IN_DATETIME,
                                            a.OUT_DATETIME,
                                            a.STATION_NAME,
                                            a.REMARK AS ERROR_CODE,
                                            c.ERROR_DESC,
                                            CASE WHEN TRUNC(a.IN_DATETIME) = TRUNC(a.OUT_DATETIME) THEN 'CHECKIN_TRONG_NGAY' ELSE 'CHECKIN_TRUOC_DO' END AS CHECKIN_STATUS
                                       FROM sfism4.r_repair_in_out_t a
                                       INNER JOIN sfis1.c_model_desc_t b ON a.model_name = b.model_name
                                       INNER JOIN sfis1.c_error_code_t c ON a.REMARK = c.ERROR_CODE
                                       WHERE b.MODEL_SERIAL = 'ADAPTER'
                                         AND a.P_SENDER IN ('V0904136', 'V0945375', 'V0928908', 'V3245384', 'V3211693')
                                         AND a.STATION_NAME != 'REPAIR_B36R'
                                         AND a.MODEL_NAME NOT LIKE '900%'
                                         AND a.MODEL_NAME NOT LIKE '692%'
                                         AND a.MODEL_NAME NOT LIKE '699%'
                                         AND a.REPAIRER IS NOT NULL
                                         AND a.OUT_DATETIME BETWEEN :startDate AND :endDate";
                var checkOutList = new List<CheckOutRecord>();
                await using (var cmd = new OracleCommand(checkOutQuery, connection))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(new OracleParameter(":startDate", OracleDbType.Date) { Value = request.StartDate });
                    cmd.Parameters.Add(new OracleParameter(":endDate", OracleDbType.Date) { Value = request.EndDate });
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        checkOutList.Add(new CheckOutRecord
                        {
                            SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString() ?? string.Empty,
                            MODEL_NAME = reader["MODEL_NAME"].ToString() ?? string.Empty,
                            PRODUCT_LINE = reader["PRODUCT_LINE"].ToString() ?? string.Empty,
                            P_SENDER = reader["P_SENDER"].ToString() ?? string.Empty,
                            REPAIRER = reader["REPAIRER"].ToString() ?? string.Empty,
                            STATION_NAME = reader["STATION_NAME"].ToString() ?? string.Empty,
                            IN_DATETIME = reader["IN_DATETIME"] as DateTime?,
                            OUT_DATETIME = reader["OUT_DATETIME"] as DateTime?,
                            ERROR_CODE = reader["ERROR_CODE"].ToString() ?? string.Empty,
                            ERROR_DESC = reader["ERROR_DESC"].ToString() ?? string.Empty,
                            CHECKIN_STATUS = reader["CHECKIN_STATUS"].ToString() ?? string.Empty
                        });
                    }
                }

                var exportRecords = await _sqlContext.Exports
                    .Where(e => e.CheckingB36R > 0 && e.ExportDate >= request.StartDate && e.ExportDate <= request.EndDate)
                    .ToListAsync();

                var checkInSerialsToday = new HashSet<string>(
                    checkInList
                        .Where(ci => ci.IN_DATETIME.HasValue && ci.IN_DATETIME.Value.Date == request.StartDate.Date)
                        .Select(ci => ci.SERIAL_NUMBER));

                foreach (var export in exportRecords)
                {
                    if (checkOutList.Any(c => c.SERIAL_NUMBER == export.SerialNumber))
                    {
                        continue;
                    }

                    var status = checkInSerialsToday.Contains(export.SerialNumber)
                        ? "CHECKIN_TRONG_NGAY"
                        : "CHECKIN_TRUOC_DO";

                    checkOutList.Add(new CheckOutRecord
                    {
                        SERIAL_NUMBER = export.SerialNumber,
                        MODEL_NAME = export.ModelName ?? "N/A",
                        PRODUCT_LINE = export.ProductLine ?? "N/A",
                        P_SENDER = export.EntryPerson ?? "N/A",
                        REPAIRER = export.ExportPerson ?? "N/A",
                        IN_DATETIME = null,
                        OUT_DATETIME = export.ExportDate,
                        ERROR_CODE = "N/A",
                        ERROR_DESC = "N/A",
                        CHECKIN_STATUS = status
                    });
                }

                var checkOutTrongNgay = checkOutList
                    .Where(c => c.CHECKIN_STATUS == "CHECKIN_TRONG_NGAY")
                    .ToList();
                var checkOutNgayTruoc = checkOutList
                    .Where(c => c.CHECKIN_STATUS == "CHECKIN_TRUOC_DO")
                    .ToList();

                var response = new
                {
                    checkIn = new { count = checkInList.Count, data = checkInList },
                    checkOut = new
                    {
                        count = checkOutList.Count,
                        trongNgay = new { count = checkOutTrongNgay.Count, data = checkOutTrongNgay },
                        ngayTruoc = new { count = checkOutNgayTruoc.Count, data = checkOutNgayTruoc }
                    }
                };

                return Ok(response);
            }
            catch (OracleException ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        [HttpGet("GetCheckInOutBeforeKanban")]
        public async Task<IActionResult> GetCheckInOutBeforeKanban(DateTime? startDate, DateTime? endDate)
        {
            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            try
            {
                await connection.OpenAsync();

                var defaultEnd = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 7, 30, 0);
                var end = endDate ?? defaultEnd;
                var start = startDate ?? end.AddDays(-1);

                var checkInQuery = @"SELECT a.SERIAL_NUMBER,
                                            a.MO_NUMBER,
                                            a.MODEL_NAME,
                                            a.STATION_NAME,
                                            a.P_SENDER,
                                            b.PRODUCT_LINE,
                                            a.REMARK AS ERROR_CODE,
                                            a.IN_DATETIME,
                                            c.ERROR_DESC
                                       FROM SFISM4.R_REPAIR_IN_OUT_T a
                                       INNER JOIN SFIS1.C_MODEL_DESC_T b ON a.MODEL_NAME = b.MODEL_NAME
                                       INNER JOIN SFIS1.C_ERROR_CODE_T c ON a.REMARK = c.ERROR_CODE
                                       WHERE b.MODEL_SERIAL = 'ADAPTER'
                                         AND a.P_SENDER IN ('V0904136', 'V0945375', 'V0928908', 'V3245384', 'V3211693')
                                         AND a.IN_DATETIME BETWEEN :startDate AND :endDate
                                         AND NOT REGEXP_LIKE(a.MODEL_NAME, '^(900|692|930)')
                                         AND NOT EXISTS (
                                              SELECT 1
                                              FROM sfism4.z_kanban_tracking_t z
                                              WHERE z.serial_number = a.serial_number
                                         )";

                var checkInList = new List<CheckInRecord>();
                await using (var cmd = new OracleCommand(checkInQuery, connection))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(new OracleParameter(":startDate", OracleDbType.Date) { Value = start });
                    cmd.Parameters.Add(new OracleParameter(":endDate", OracleDbType.Date) { Value = end });
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        checkInList.Add(new CheckInRecord
                        {
                            SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString() ?? string.Empty,
                            MO_NUMBER = reader["MO_NUMBER"].ToString() ?? string.Empty,
                            MODEL_NAME = reader["MODEL_NAME"].ToString() ?? string.Empty,
                            STATION_NAME = reader["STATION_NAME"].ToString() ?? string.Empty,
                            P_SENDER = reader["P_SENDER"].ToString() ?? string.Empty,
                            ERROR_CODE = reader["ERROR_CODE"].ToString() ?? string.Empty,
                            IN_DATETIME = reader["IN_DATETIME"] as DateTime?,
                            ERROR_DESC = reader["ERROR_DESC"].ToString() ?? string.Empty
                        });
                    }
                }

                var checkOutQuery = @"SELECT a.SERIAL_NUMBER,
                                             a.MODEL_NAME,
                                             b.PRODUCT_LINE,
                                             a.P_SENDER,
                                             a.REPAIRER,
                                             a.IN_DATETIME,
                                             a.OUT_DATETIME,
                                             a.STATION_NAME,
                                             a.REMARK AS ERROR_CODE,
                                             c.ERROR_DESC,
                                             CASE WHEN TRUNC(a.IN_DATETIME) = TRUNC(a.OUT_DATETIME) THEN 'CHECKIN_TRONG_NGAY' ELSE 'CHECKIN_TRUOC_DO' END AS CHECKIN_STATUS
                                        FROM sfism4.r_repair_in_out_t a
                                        INNER JOIN sfis1.c_model_desc_t b ON a.model_name = b.model_name
                                        INNER JOIN sfis1.c_error_code_t c ON a.REMARK = c.ERROR_CODE
                                        WHERE b.MODEL_SERIAL = 'ADAPTER'
                                          AND a.P_SENDER IN ('V0904136', 'V0945375', 'V0928908', 'V3245384', 'V3211693')
                                          AND a.REPAIRER IS NOT NULL
                                          AND a.OUT_DATETIME BETWEEN :startDate AND :endDate
                                          AND NOT REGEXP_LIKE(a.MODEL_NAME, '^(900|692|930)')
                                          AND NOT EXISTS (
                                              SELECT 1
                                              FROM sfism4.z_kanban_tracking_t z
                                              WHERE z.serial_number = a.serial_number
                                          )";

                var checkOutList = new List<CheckOutRecord>();
                await using (var cmd = new OracleCommand(checkOutQuery, connection))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(new OracleParameter(":startDate", OracleDbType.Date) { Value = start });
                    cmd.Parameters.Add(new OracleParameter(":endDate", OracleDbType.Date) { Value = end });
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        checkOutList.Add(new CheckOutRecord
                        {
                            SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString() ?? string.Empty,
                            MODEL_NAME = reader["MODEL_NAME"].ToString() ?? string.Empty,
                            PRODUCT_LINE = reader["PRODUCT_LINE"].ToString() ?? string.Empty,
                            P_SENDER = reader["P_SENDER"].ToString() ?? string.Empty,
                            REPAIRER = reader["REPAIRER"].ToString() ?? string.Empty,
                            STATION_NAME = reader["STATION_NAME"].ToString() ?? string.Empty,
                            IN_DATETIME = reader["IN_DATETIME"] as DateTime?,
                            OUT_DATETIME = reader["OUT_DATETIME"] as DateTime?,
                            ERROR_CODE = reader["ERROR_CODE"].ToString() ?? string.Empty,
                            ERROR_DESC = reader["ERROR_DESC"].ToString() ?? string.Empty,
                            CHECKIN_STATUS = reader["CHECKIN_STATUS"].ToString() ?? string.Empty
                        });
                    }
                }

                var checkOutTrongNgay = checkOutList
                    .Where(c => c.CHECKIN_STATUS == "CHECKIN_TRONG_NGAY")
                    .ToList();
                var checkOutTonKhoCu = checkOutList
                    .Where(c => c.CHECKIN_STATUS == "CHECKIN_TRUOC_DO")
                    .ToList();

                var checkOutSerials = new HashSet<string>(checkOutList.Select(co => co.SERIAL_NUMBER));
                var tonKhoTrongNgay = checkInList
                    .Where(ci => !checkOutSerials.Contains(ci.SERIAL_NUMBER))
                    .ToList();

                var response = new
                {
                    checkIn = new { count = checkInList.Count, data = checkInList },
                    checkOut = new
                    {
                        count = checkOutList.Count,
                        trongNgay = new { count = checkOutTrongNgay.Count, data = checkOutTrongNgay },
                        tonKhoCu = new { count = checkOutTonKhoCu.Count, data = checkOutTonKhoCu }
                    },
                    tonKhoTrongNgay = new { count = tonKhoTrongNgay.Count, data = tonKhoTrongNgay }
                };

                return Ok(response);
            }
            catch (OracleException ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}

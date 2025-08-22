using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace API_WEB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportSkipHassController : ControllerBase
    {
        private readonly OracleDbContext _oracleContext;

        public ReportSkipHassController(OracleDbContext oracleContext)
        {
            _oracleContext = oracleContext;
        }

        // Model for the query result
        public class MoData
        {
            public string MoNumber { get; set; }
            public string ModelName { get; set; }
            public DateTime InStationTime { get; set; }
            public int TargetQty { get; set; }
            public string SkipHassBi { get; set; }
            public string SkipReason { get; set; }
            public string Type { get; set; }
        }

        // DTO for the POST request body
        public class DateRangeRequest
        {
            public string StartDate { get; set; }
            public string EndDate { get; set; }
        }

        // POST: api/ReportSkipHass/GetMoData
        [HttpPost("GetMoData")]
        public async Task<ActionResult<IEnumerable<MoData>>> GetMoData([FromBody] DateRangeRequest request)
        {
            try
            {
                // Validate input
                if (request == null || string.IsNullOrEmpty(request.StartDate) || string.IsNullOrEmpty(request.EndDate))
                {
                    return BadRequest("Start date and end date are required.");
                }

                if (!DateTime.TryParse(request.StartDate, out DateTime start) || !DateTime.TryParse(request.EndDate, out DateTime end))
                {
                    return BadRequest("Invalid date format. Use 'YYYY-MM-DD HH:mm:ss'.");
                }

                if (start >= end)
                {
                    return BadRequest("Start date must be earlier than end date.");
                }

                var query = @"
                    SELECT DISTINCT 
                        a.MO_NUMBER, 
                        a.MODEL_NAME, 
                        MIN(a.IN_STATION_TIME) AS IN_STATION_TIME, 
                        c.TARGET_QTY,
                        CASE 
                            WHEN (h.MO_NUMBER IS NOT NULL AND h.TYPE = 'BI_HASS') 
                                 OR EXISTS (
                                     SELECT 1 
                                     FROM sfis1.c_bi_hass_control_t h2 
                                     WHERE h2.MO_NUMBER = a.MODEL_NAME
                                 ) THEN 'N/A'
                            ELSE 
                                CASE 
                                    WHEN c.TARGET_QTY BETWEEN 2 AND 8 THEN '2'
                                    WHEN c.TARGET_QTY BETWEEN 9 AND 15 THEN '3'
                                    WHEN c.TARGET_QTY BETWEEN 16 AND 25 THEN '5'
                                    WHEN c.TARGET_QTY BETWEEN 26 AND 50 THEN '8'
                                    WHEN c.TARGET_QTY BETWEEN 51 AND 90 THEN '13'
                                    WHEN c.TARGET_QTY BETWEEN 91 AND 150 THEN '20'
                                    WHEN c.TARGET_QTY BETWEEN 151 AND 280 THEN '32'
                                    WHEN c.TARGET_QTY BETWEEN 281 AND 500 THEN '50'
                                    WHEN c.TARGET_QTY BETWEEN 501 AND 1200 THEN '80'
                                    WHEN c.TARGET_QTY BETWEEN 1201 AND 3200 THEN '125'
                                    WHEN c.TARGET_QTY BETWEEN 3201 AND 10000 THEN '200'
                                    WHEN c.TARGET_QTY BETWEEN 10001 AND 35000 THEN '315'
                                    WHEN c.TARGET_QTY BETWEEN 35001 AND 150000 THEN '500'
                                    WHEN c.TARGET_QTY BETWEEN 150001 AND 500000 THEN '800'
                                    WHEN c.TARGET_QTY >= 500001 THEN '1250'
                                    ELSE '0'
                                END
                        END AS SKIP_HASS_BI,
                        CASE 
                            WHEN (h.MO_NUMBER IS NOT NULL AND h.TYPE = 'BI_HASS') 
                                 OR EXISTS (
                                     SELECT 1 
                                     FROM sfis1.c_bi_hass_control_t h2 
                                     WHERE h2.MO_NUMBER = a.MODEL_NAME
                                 ) THEN 'CUSTOMER REQUEST SKIP'
                            ELSE NULL
                        END AS SKIP_REASON,
                        'HASS' AS TYPE
                    FROM 
                        sfism4.r117 a
                    INNER JOIN 
                        SFIS1.C_MODEL_DESC_T b
                        ON b.model_name = a.model_name
                    INNER JOIN 
                        sfism4.r105 c
                        ON c.MO_NUMBER = a.MO_NUMBER
                    LEFT JOIN 
                        sfis1.c_bi_hass_control_t h
                        ON h.MO_NUMBER = a.MO_NUMBER
                        AND h.model_name = a.model_name
                        AND h.TYPE = 'BI_HASS'
                    LEFT JOIN 
                        sfis1.c_bi_hass_control_t h2
                        ON h2.MO_NUMBER = a.MODEL_NAME
                    WHERE 
                        a.IN_STATION_TIME BETWEEN TO_DATE(:startDate, 'YYYY-MM-DD HH24:MI:SS') 
                                           AND TO_DATE(:endDate, 'YYYY-MM-DD HH24:MI:SS')
                        AND a.GROUP_NAME = 'SMTLOADING'
                        AND b.model_serial = 'ADAPTER'
                    GROUP BY 
                        a.MO_NUMBER, 
                        a.MODEL_NAME, 
                        c.TARGET_QTY, 
                        h.MO_NUMBER, 
                        h.TYPE, 
                        h.DESCRIPTION, 
                        h2.DESCRIPTION";

                var result = new List<MoData>();

                // Use raw SQL execution with manual mapping
                using (var connection = (OracleConnection)_oracleContext.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("startDate", request.StartDate));
                        command.Parameters.Add(new OracleParameter("endDate", request.EndDate));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var moData = new MoData
                                {
                                    MoNumber = reader["MO_NUMBER"].ToString(),
                                    ModelName = reader["MODEL_NAME"].ToString(),
                                    InStationTime = Convert.ToDateTime(reader["IN_STATION_TIME"]),
                                    TargetQty = Convert.ToInt32(reader["TARGET_QTY"]),
                                    SkipHassBi = reader["SKIP_HASS_BI"].ToString(),
                                    SkipReason = reader["SKIP_REASON"] != DBNull.Value ? reader["SKIP_REASON"].ToString() : null,
                                    Type = reader["TYPE"].ToString()
                                };
                                result.Add(moData);
                            }
                        }
                    }
                }

                if (result == null || !result.Any())
                {
                    return NotFound("No data found for the specified criteria.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpPost("GeMoBI")]
        public async Task<ActionResult<IEnumerable<BIData>>> GetBIData([FromBody] DateRangeRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.StartDate) || string.IsNullOrEmpty(request.EndDate))
                {
                    return BadRequest("Nhap gia tri StartDate va EndDate");
                }
                if (!DateTime.TryParse(request.StartDate, out DateTime start) || !DateTime.TryParse(request.EndDate, out DateTime end))
                {
                    return BadRequest("Invalid date format. Use 'YYYY-MM-DD HH:mm:ss'.");
                }
                if (start >= end)
                {
                    return BadRequest("Start date must be earlier than end date.");
                }

                var query = @"SELECT 
                            a.MO_NUMBER,
                            a.MODEL_NAME,
                            a.GROUP_NAME,
                            a.KEY_PART_NO,
                            a.LOT_SIZE_MIN,
                            a.LOT_SIZE_MAX,
                            a.SAMPLE_SIZE,
                            (SELECT MIN(IN_DATE)
                             FROM SFISM4.R_GET_ATE_SN_T
                             WHERE MO_NUMBER = a.MO_NUMBER 
                               AND TO_GROUP = 'BI'
                               AND IN_DATE BETWEEN TO_DATE(:startDate, 'YYYY-MM-DD HH24:MI:SS') 
                                               AND TO_DATE(:endDate, 'YYYY-MM-DD HH24:MI:SS')) AS EarliestDate,
                            b.TARGET_QTY,
                            CASE 
                                WHEN c.MO_NUMBER IS NOT NULL THEN 'N/A'
                                ELSE TO_CHAR(a.SAMPLE_SIZE)
                            END AS skip_bi_hass,
                            c.DESCRIPTION AS skip_reason
                        FROM 
                            SFISM4.R_SAMPLING_RECORD_T a
                        LEFT JOIN 
                            SFISM4.R105 b
                            ON a.MO_NUMBER = b.MO_NUMBER
                        LEFT JOIN 
                            SFIS1.C_BI_HASS_CONTROL_T c
                            ON (c.MO_NUMBER = a.MO_NUMBER OR c.MO_NUMBER = a.MODEL_NAME)
                        WHERE 
                            a.GROUP_NAME = 'BI'
                            AND a.DESCRIPTION = 'ADAPTER'
                            AND EXISTS (
                                SELECT 1
                                FROM SFISM4.R_GET_ATE_SN_T g
                                WHERE g.MO_NUMBER = a.MO_NUMBER
                                  AND g.TO_GROUP = 'BI'
                                  AND g.IN_DATE BETWEEN TO_DATE(:startDate, 'YYYY-MM-DD HH24:MI:SS') 
                                                    AND TO_DATE(:endDate, 'YYYY-MM-DD HH24:MI:SS')
                            )";
                var result = new List<BIData>();
                using (var connection = (OracleConnection)_oracleContext.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("startDate", request.StartDate));
                        command.Parameters.Add(new OracleParameter("endDate", request.EndDate));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var biData = new BIData
                                {
                                    MoNumber = reader["MO_NUMBER"].ToString(),
                                    ModelName = reader["MODEL_NAME"].ToString(),
                                    InStationTime = reader["EARLIESTDATE"] != DBNull.Value ? Convert.ToDateTime(reader["EARLIESTDATE"]) : default,
                                    TargetQty = reader["TARGET_QTY"] != DBNull.Value ? Convert.ToInt32(reader["TARGET_QTY"]) : 0,
                                    SkipHassBi = reader["SKIP_BI_HASS"].ToString(),
                                    SkipReason = reader["SKIP_REASON"] != DBNull.Value ? reader["SKIP_REASON"].ToString() : null,
                                    Type = reader["GROUP_NAME"].ToString(),
                                    LotSizeMin = reader["LOT_SIZE_MIN"] != DBNull.Value ? Convert.ToInt32(reader["LOT_SIZE_MIN"]) : 0,
                                    LotSizeMax = reader["LOT_SIZE_MAX"] != DBNull.Value ? Convert.ToInt32(reader["LOT_SIZE_MAX"]) : 0,
                                    KeyPartNo = reader["KEY_PART_NO"].ToString()
                                };
                                result.Add(biData);
                            }
                        }
                    }
                }
                if (result == null || !result.Any())
                {
                    return NotFound("No data found for the specified criteria.");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        public class BIData
        {
            public string MoNumber { get; set; }
            public string ModelName { get; set; }
            public DateTime InStationTime { get; set; }
            public int TargetQty { get; set; }
            public string SkipHassBi { get; set; }
            public string SkipReason { get; set; }
            public string Type { get; set; }
            public int LotSizeMax { get; set; }
            public int LotSizeMin { get; set; }
            public string KeyPartNo { get; set; }
        }
    }
}
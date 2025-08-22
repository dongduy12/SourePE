using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API_WEB.Controllers.SFC
{
    [Route("api/[controller]")]
    [ApiController]
    public class SFCController : ControllerBase
    {
        private readonly string _connectionString = "User Id=TE;Password=B05te;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=10.220.130.220)(PORT=1521)))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=vnsfc)))";

        [HttpPost("history-error-by-sn")]
        public async Task<IActionResult> GetErrorHistory([FromBody] HistoryErrorRequest request)
        {
            try
            {
                if (request == null || request.SerialNumbers == null || !request.SerialNumbers.Any() || (request.TypeValue != 1 && request.TypeValue != 2))
                {
                    return BadRequest("Invalid request: SerialNumbers and TypeValue are required. TypeValue must be 1 or 2.");
                }

                // Construct parameter placeholders
                var snPlaceholders = string.Join(", ", request.SerialNumbers.Select((_, i) => $":sn{i}"));
                string query = request.TypeValue == 1 ? GetSqlQueryType1(snPlaceholders) : GetSqlQueryType2(snPlaceholders);

                var results = new List<Dictionary<string, object>>();

                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand(query, connection))
                    {
                        // Add parameters to prevent SQL injection
                        for (int i = 0; i < request.SerialNumbers.Count; i++)
                        {
                            command.Parameters.Add(new OracleParameter($"sn{i}", request.SerialNumbers[i]));
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int j = 0; j < reader.FieldCount; j++)
                                {
                                    row[reader.GetName(j)] = reader.IsDBNull(j) ? null : reader.GetValue(j);
                                }
                                results.Add(row);
                            }
                        }
                    }
                }

                if (!results.Any())
                {
                    return NotFound("No error history found for the provided serial numbers.");
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private string GetSqlQueryType1(string snPlaceholders)
        {
            return $@"
                SELECT * FROM
                (
                    SELECT 
                        r.SERIAL_NUMBER,
                        ref.KEY_PART_SN
                    FROM (
                        SELECT column_value AS KEY_PART_SN
                        FROM TABLE(SYS.ODCIVARCHAR2LIST({snPlaceholders}))
                    ) ref
                    LEFT JOIN sfism4.r_wip_keyparts_t r
                        ON ref.KEY_PART_SN = r.KEY_PART_SN
                    UNION ALL
                    SELECT 
                        p.SERIAL_NUMBER,
                        ref.KEY_PART_SN
                    FROM (
                        SELECT column_value AS KEY_PART_SN
                        FROM TABLE(SYS.ODCIVARCHAR2LIST({snPlaceholders}))
                    ) ref
                    LEFT JOIN sfism4.p_wip_keyparts_t p
                        ON ref.KEY_PART_SN = p.KEY_PART_SN
                    WHERE p.SERIAL_NUMBER IS NOT NULL
                ) a
                LEFT JOIN
                (
                    SELECT 
                        SERIAL_NUMBER,
                        MO_NUMBER,
                        MODEL_NAME,
                        TEST_TIME,
                        TEST_GROUP,
                        TEST_CODE,
                        DATA1,
                        REASON_CODE 
                    FROM SFISM4.R109 
                    WHERE SERIAL_NUMBER IN ({snPlaceholders})
                    AND TEST_GROUP IN ('ICT', 'FT', 'CTO')
                    UNION ALL
                    SELECT 
                        r.SERIAL_NUMBER,
                        r.MO_NUMBER,
                        r.MODEL_NAME,
                        r.TEST_TIME,
                        r.TEST_GROUP,
                        r.TEST_CODE,
                        r.DATA1,
                        r.REASON_CODE
                    FROM SFISM4.R109 r
                    WHERE r.SERIAL_NUMBER IN (
                        SELECT t.SERIAL_NUMBER
                        FROM sfism4.r_wip_keyparts_t t
                        WHERE t.KEY_PART_SN IN ({snPlaceholders})
                        UNION ALL
                        SELECT t.SERIAL_NUMBER
                        FROM sfism4.p_wip_keyparts_t t
                        WHERE t.KEY_PART_SN IN ({snPlaceholders})
                    )
                    AND r.TEST_GROUP IN ('ICT', 'FT', 'CTO')
                ) k
                ON (k.SERIAL_NUMBER = a.SERIAL_NUMBER OR k.SERIAL_NUMBER = a.KEY_PART_SN)
                WHERE TEST_GROUP IS NOT NULL";
        }

        private string GetSqlQueryType2(string snPlaceholders)
        {
            return $@"
                SELECT * FROM 
                (
                    SELECT SERIAL_NUMBER, KEY_PART_SN 
                    FROM SFISM4.R_WIP_KEYPARTS_T 
                    WHERE SERIAL_NUMBER IN ({snPlaceholders}) 
                    AND KEY_PART_NO LIKE 'SFG%'
                    UNION ALL
                    SELECT SERIAL_NUMBER, KEY_PART_SN 
                    FROM SFISM4.P_WIP_KEYPARTS_T 
                    WHERE SERIAL_NUMBER IN ({snPlaceholders}) 
                    AND KEY_PART_NO LIKE 'SFG%'
                ) a
                LEFT JOIN
                (
                    SELECT 
                        SERIAL_NUMBER,
                        MO_NUMBER,
                        MODEL_NAME,
                        TEST_TIME,
                        TEST_GROUP,
                        TEST_CODE,
                        DATA1,
                        REASON_CODE 
                    FROM SFISM4.R109 
                    WHERE SERIAL_NUMBER IN (
                        SELECT SERIAL_NUMBER 
                        FROM SFISM4.R_WIP_KEYPARTS_T 
                        WHERE SERIAL_NUMBER IN ({snPlaceholders}) 
                        AND KEY_PART_NO LIKE 'SFG%'
                        UNION
                        SELECT KEY_PART_SN 
                        FROM SFISM4.R_WIP_KEYPARTS_T 
                        WHERE SERIAL_NUMBER IN ({snPlaceholders}) 
                        AND KEY_PART_NO LIKE 'SFG%'
                        UNION
                        SELECT SERIAL_NUMBER 
                        FROM SFISM4.P_WIP_KEYPARTS_T 
                        WHERE SERIAL_NUMBER IN ({snPlaceholders}) 
                        AND KEY_PART_NO LIKE 'SFG%'
                        UNION
                        SELECT KEY_PART_SN 
                        FROM SFISM4.P_WIP_KEYPARTS_T 
                        WHERE SERIAL_NUMBER IN ({snPlaceholders}) 
                        AND KEY_PART_NO LIKE 'SFG%'
                    ) 
                    AND TEST_GROUP IN ('ICT', 'FT', 'CTO')
                ) b
                ON (a.SERIAL_NUMBER = b.SERIAL_NUMBER OR a.KEY_PART_SN = b.SERIAL_NUMBER)";
        }
    }

    public class HistoryErrorRequest
    {
        public List<string> SerialNumbers { get; set; }
        public int TypeValue { get; set; }
    }
}
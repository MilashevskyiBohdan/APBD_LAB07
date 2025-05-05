using Microsoft.Data.SqlClient;

namespace WebApplication1.Controllers;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;


[ApiController]
[Route("/api/[controller]")]
public class Lab07Controller : ControllerBase
{
    private readonly string _connectionString = "Data Source=db-mssql;Initial Catalog=2019SBD;Integrated Security=True;Trust Server Certificate=True";

        [HttpGet("trips")]
        public async Task<IActionResult> GetTrips()
        {
            var trips = new List<TripDto>();
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
    SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
           STUFF((
               SELECT ', ' + c2.Name
               FROM Country_Trip ct2
               JOIN Country c2 ON c2.IdCountry = ct2.IdCountry
               WHERE ct2.IdTrip = t.IdTrip
               FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS Countries
    FROM Trip t
", con);


            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                trips.Add(new TripDto
                {
                    IdTrip = (int)reader["IdTrip"],
                    Name = reader["Name"].ToString()!,
                    Description = reader["Description"].ToString()!,
                    DateFrom = (DateTime)reader["DateFrom"],
                    DateTo = (DateTime)reader["DateTo"],
                    MaxPeople = (int)reader["MaxPeople"],
                    Countries = reader["Countries"].ToString()!
                });
            }
            return Ok(trips);
        }

        [HttpGet("clients/{id}/trips")]
        public async Task<IActionResult> GetClientTrips(int id)
        {
            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using var checkCmd = new SqlCommand("SELECT COUNT(1) FROM Client WHERE IdClient = @id", con);
            checkCmd.Parameters.AddWithValue("@id", id);
            if ((int)await checkCmd.ExecuteScalarAsync() == 0)
                return NotFound("Client not found.");

            var cmd = new SqlCommand(@"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                       ct.RegisteredAt, ct.PaymentDate
                FROM Trip t
                JOIN Client_Trip ct ON ct.IdTrip = t.IdTrip
                WHERE ct.IdClient = @id", con);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            var trips = new List<object>();
            while (await reader.ReadAsync())
            {
                trips.Add(new
                {
                    IdTrip = reader["IdTrip"],
                    Name = reader["Name"],
                    Description = reader["Description"],
                    DateFrom = reader["DateFrom"],
                    DateTo = reader["DateTo"],
                    MaxPeople = reader["MaxPeople"],
                    RegisteredAt = reader["RegisteredAt"],
                    PaymentDate = reader["PaymentDate"]
                });
            }
            return Ok(trips);
        }

        [HttpPost("clients")]
        public async Task<IActionResult> CreateClient([FromBody] ClientDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) ||
                string.IsNullOrWhiteSpace(dto.LastName) ||
                string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest("Missing required fields.");
            }

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);
                SELECT SCOPE_IDENTITY();", con);
            cmd.Parameters.AddWithValue("@FirstName", dto.FirstName);
            cmd.Parameters.AddWithValue("@LastName", dto.LastName);
            cmd.Parameters.AddWithValue("@Email", dto.Email);
            cmd.Parameters.AddWithValue("@Telephone", (object?)dto.Telephone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pesel", (object?)dto.Pesel ?? DBNull.Value);

            await con.OpenAsync();
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Created($"/api/clients/{id}", new { IdClient = id });
        }

        [HttpPut("clients/{id}/trips/{tripId}")]
        public async Task<IActionResult> RegisterClientToTrip(int id, int tripId)
        {
            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            var checkClient = new SqlCommand("SELECT COUNT(1) FROM Client WHERE IdClient = @id", con);
            checkClient.Parameters.AddWithValue("@id", id);
            if ((int)await checkClient.ExecuteScalarAsync() == 0)
                return NotFound("Client not found.");

            var checkTrip = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @tripId", con);
            checkTrip.Parameters.AddWithValue("@tripId", tripId);
            var maxPeople = (int?)await checkTrip.ExecuteScalarAsync();
            if (maxPeople == null)
                return NotFound("Trip not found.");

            var countCmd = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @tripId", con);
            countCmd.Parameters.AddWithValue("@tripId", tripId);
            var currentCount = (int)await countCmd.ExecuteScalarAsync();
            if (currentCount >= maxPeople)
                return BadRequest("Trip is full.");

            var insert = new SqlCommand("INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt) VALUES (@id, @tripId, GETDATE())", con);
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@tripId", tripId);
            await insert.ExecuteNonQueryAsync();
            return Ok("Client registered to trip.");
        }

        [HttpDelete("clients/{id}/trips/{tripId}")]
        public async Task<IActionResult> DeleteClientTrip(int id, int tripId)
        {
            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            var check = new SqlCommand("SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @id AND IdTrip = @tripId", con);
            check.Parameters.AddWithValue("@id", id);
            check.Parameters.AddWithValue("@tripId", tripId);
            if ((int)await check.ExecuteScalarAsync() == 0)
                return NotFound("Registration not found.");

            var delete = new SqlCommand("DELETE FROM Client_Trip WHERE IdClient = @id AND IdTrip = @tripId", con);
            delete.Parameters.AddWithValue("@id", id);
            delete.Parameters.AddWithValue("@tripId", tripId);
            await delete.ExecuteNonQueryAsync();
            return Ok("Registration deleted.");
        }
}
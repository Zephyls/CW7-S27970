using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TravelAgencyApi.Dtos;

namespace TravelAgencyApi.Controllers
{
    /// <summary>
    /// Handles client creation, registration and deregistration for trips.
    /// </summary>
    [ApiController]
    [Route("api/clients")]
    public class ClientsController : ControllerBase
    {
        private readonly string _connectionString;
        
        /// <summary>
        /// Reads the DefaultConnection string from config.
        /// </summary>
        public ClientsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }


        
        /// <summary>
        /// GET /api/clients/{id}/trips
        /// Returns all trips this client is registered for, including registration/payment dates.
        /// </summary>
        [HttpGet("{id}/trips")]
        public IActionResult GetClientTrips(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Check if client exists
            using var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Client WHERE IdClient = @IdClient", conn);
            checkCmd.Parameters.AddWithValue("@IdClient", id);
            if ((int)checkCmd.ExecuteScalar() == 0)
                return NotFound($"Client with ID {id} not found.");

            var trips = new List<ClientTripDto>();
            
            // Join Client_Trip and Trip to get details plus registration info
            using var cmd = new SqlCommand(@"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, ct.RegisteredAt, ct.PaymentDate
                FROM Client_Trip ct
                JOIN Trip t ON ct.IdTrip = t.IdTrip
                WHERE ct.IdClient = @IdClient", conn);
            cmd.Parameters.AddWithValue("@IdClient", id);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                trips.Add(new ClientTripDto
                {
                    IdTrip = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    RegisteredAt = reader.GetInt32(6),
                    PaymentDate = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                });
            }

            return Ok(trips);
        }

        /// <summary>
        /// POST /api/clients
        /// Creates a new client record and returns its ID.
        /// </summary>
        [HttpPost]
        public IActionResult CreateClient([FromBody] ClientCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(@"
                INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);
                SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@FirstName", dto.FirstName);
            cmd.Parameters.AddWithValue("@LastName", dto.LastName);
            cmd.Parameters.AddWithValue("@Email", dto.Email);
            cmd.Parameters.AddWithValue("@Telephone", (object)dto.Telephone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pesel", (object)dto.Pesel ?? DBNull.Value);

            var newId = Convert.ToInt32(cmd.ExecuteScalar());
            return CreatedAtAction(nameof(GetClientTrips), new { id = newId }, new { IdClient = newId });
        }

        
        /// <summary>
        /// PUT /api/clients/{id}/trips/{tripId}
        /// Registers a client for a specific trip. Validates existence and capacity.
        /// </summary>
        [HttpPut("{id}/trips/{tripId}")]
        public IActionResult RegisterClientToTrip(int id, int tripId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Validate client exists
            using var checkClientCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Client WHERE IdClient = @IdClient", conn);
            checkClientCmd.Parameters.AddWithValue("@IdClient", id);
            if ((int)checkClientCmd.ExecuteScalar() == 0)
                return NotFound($"Client with ID {id} not found.");

            // Validate trip exists and retrieve capacity
            using var checkTripCmd = new SqlCommand(
                "SELECT MaxPeople FROM Trip WHERE IdTrip = @IdTrip", conn);
            checkTripCmd.Parameters.AddWithValue("@IdTrip", tripId);
            var maxObj = checkTripCmd.ExecuteScalar();
            if (maxObj == null)
                return NotFound($"Trip with ID {tripId} not found.");
            int maxPeople = (int)maxObj;

            // Check current registrations
            using var countCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @IdTrip", conn);
            countCmd.Parameters.AddWithValue("@IdTrip", tripId);
            if ((int)countCmd.ExecuteScalar() >= maxPeople)
                return BadRequest("Maximum number of participants reached for this trip.");

            // Insert registration
            int regDate = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            using var insertCmd = new SqlCommand(
                "INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt) VALUES (@IdClient, @IdTrip, @RegisteredAt)", conn);
            insertCmd.Parameters.AddWithValue("@IdClient", id);
            insertCmd.Parameters.AddWithValue("@IdTrip", tripId);
            insertCmd.Parameters.AddWithValue("@RegisteredAt", regDate);
            insertCmd.ExecuteNonQuery();

            return Ok("Client successfully registered for the trip.");
        }

        
        /// <summary>
        /// DELETE /api/clients/{id}/trips/{tripId}
        /// Removes a client’s registration for a trip.
        /// </summary>
        [HttpDelete("{id}/trips/{tripId}")]
        public IActionResult UnregisterClientFromTrip(int id, int tripId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Check registration exists
            using var checkCmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Client_Trip
                WHERE IdClient = @IdClient AND IdTrip = @IdTrip", conn);
            checkCmd.Parameters.AddWithValue("@IdClient", id);
            checkCmd.Parameters.AddWithValue("@IdTrip", tripId);
            if ((int)checkCmd.ExecuteScalar() == 0)
                return NotFound("Registration not found for this client and trip.");

            // Delete registration
            using var delCmd = new SqlCommand(
                "DELETE FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip", conn);
            delCmd.Parameters.AddWithValue("@IdClient", id);
            delCmd.Parameters.AddWithValue("@IdTrip", tripId);
            delCmd.ExecuteNonQuery();

            return NoContent();
        }
    }
}

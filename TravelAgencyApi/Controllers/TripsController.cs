using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TravelAgencyApi.Dtos;

namespace TravelAgencyApi.Controllers
{
    /// <summary>
    /// Manages trip-related operations: listing all trips and their countries.
    /// </summary>
    [ApiController]
    [Route("api/trips")]
    public class TripsController : ControllerBase
    {
        private readonly string _connectionString;
        
        /// <summary>
        /// Constructor injects configuration to get the DB connection string.
        /// </summary>
        public TripsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        /// <summary>
        /// GET /api/trips
        /// Retrieves all available trips with their basic details and list of countries.
        /// </summary>
        [HttpGet]
        public IActionResult GetAllTrips()
        {
            var trips = new List<TripDto>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            
            // Load all trips
            using (var cmd = new SqlCommand(
                "SELECT IdTrip, Name, Description, DateFrom, DateTo, MaxPeople FROM Trip", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    trips.Add(new TripDto
                    {
                        IdTrip = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        DateFrom = reader.GetDateTime(3),
                        DateTo = reader.GetDateTime(4),
                        MaxPeople = reader.GetInt32(5),
                        Countries = new List<string>()
                    });
                }
            }
            
            // For each trip, load associated countries
            foreach (var trip in trips)
            {
                // Parameterized query to prevent SQL injection
                using var countryCmd = new SqlCommand(
                    "SELECT c.Name FROM Country c JOIN Country_Trip ct ON c.IdCountry = ct.IdCountry WHERE ct.IdTrip = @TripId", conn);
                countryCmd.Parameters.AddWithValue("@TripId", trip.IdTrip);
                using var countryReader = countryCmd.ExecuteReader();
                while (countryReader.Read())
                {
                    trip.Countries.Add(countryReader.GetString(0));
                }
            }

            return Ok(trips);
        }
    }
}
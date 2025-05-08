using System.ComponentModel.DataAnnotations;

namespace TravelAgencyApi.Dtos
{
    public class ClientCreateDto
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string Telephone { get; set; }
        public string Pesel { get; set; }
    }
}
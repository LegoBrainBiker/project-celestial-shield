namespace project_celestial_shield.Models
{
    public class UserProfile
    {
        public string? name { get; set; }
        public int? age { get; set; }
        public string? email { get; set; }
        public Address? address { get; set; }
    }

    public class Address
    {
        public string? street { get; set; }
        public string? city { get; set; }
        public string? country { get; set; }
    }
}

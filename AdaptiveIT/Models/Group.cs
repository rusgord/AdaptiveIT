using System.Collections.Generic;

namespace AdaptiveIT.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string InviteCode { get; set; } = "GRP-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        public ICollection<ApplicationUser>? Students { get; set; }
    }
}
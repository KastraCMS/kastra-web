using System.ComponentModel.DataAnnotations;

namespace Kastra.Web.Models.Manage
{
    public class EditProfileViewModel
    {
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [Display(Name = "Displayed name")]
        public string DisplayedName { get; set; }
    }
}
namespace Kastra.Web.Areas.API.Models.SiteConfiguration
{
    public class MailTemplateModel
    {
        public int Id { get; set; }
        public string Keyname { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
    }
}

using Microsoft.AspNetCore.Http;

namespace WedNightFury.Models.ViewModels
{
    public class DeliveredViewModel
    {
        public int OrderId { get; set; }

        public string? Code { get; set; }

        public string? ReceiverName { get; set; }

        public string? ReceiverAddress { get; set; }

        public IFormFile? PodImage { get; set; }

        public string? Note { get; set; }
    }
}

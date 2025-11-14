using Microsoft.AspNetCore.Http;

namespace WedNightFury.Models.ViewModels
{
    public class DeliveredViewModel
    {
        public int OrderId { get; set; }

        public string Code { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;
        public string ReceiverAddress { get; set; } = string.Empty;

        public IFormFile PodImage { get; set; } = default!;

        public string? Note { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WedNightFury.Models
{
    [Table("orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }

        // Khách hàng
        [ForeignKey("User")]
        public int? CustomerId { get; set; }
        public virtual User? User { get; set; }

        // Mã đơn
        [StringLength(50)]
        public string? Code { get; set; }

        // Người gửi
        [StringLength(100)]
        public string? SenderName { get; set; }
        [StringLength(20)]
        public string? SenderPhone { get; set; }
        [StringLength(200)]
        public string? SenderAddress { get; set; }

        // Người nhận
        [StringLength(100)]
        public string? ReceiverName { get; set; }
        [StringLength(20)]
        public string? ReceiverPhone { get; set; }
        [StringLength(200)]
        public string? ReceiverAddress { get; set; }

        // Hàng hóa
        [StringLength(200)]
        public string? ProductName { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Weight { get; set; }

        [Column(TypeName = "decimal(15,2)")]
        public decimal Value { get; set; }

        [StringLength(200)]
        public string? Note { get; set; }

        // Quản lý đơn
        [StringLength(20)]
        public string? Status { get; set; } = "pending";  // pending | assigned | shipping | done | failed

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public string? Province { get; set; }

        // ============================
        // 🚚 TÀI XẾ
        // ============================

        public int? DriverId { get; set; }           // tài xế nhận đơn
        public DateTime? AssignedAt { get; set; }    // thời điểm tài xế nhận đơn

        public DateTime? DeliveryDate { get; set; }  // ngày giao
        public int? Sequence { get; set; }           // thứ tự ghé

        public double? Lat { get; set; }
        public double? Lng { get; set; }

        // POD (giao thành công)
        public string? PodImagePath { get; set; }
        public string? DeliveredNote { get; set; }
        public DateTime? DeliveredAt { get; set; }

        // Failed (giao thất bại)
        public string? FailedReason { get; set; }
        public string? FailedImagePath { get; set; }
        public DateTime? FailedAt { get; set; }
    }
}

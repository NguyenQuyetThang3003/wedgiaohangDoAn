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

        // ============================
        // 👤 KHÁCH HÀNG
        // ============================
        [ForeignKey("User")]
        public int? CustomerId { get; set; }
        public virtual User? User { get; set; }

        // ============================
        // 🔖 MÃ ĐƠN
        // ============================
        [StringLength(50)]
        public string? Code { get; set; }

        // ============================
        // 📦 NGƯỜI GỬI
        // ============================
        [StringLength(100)]
        public string? SenderName { get; set; }

        [StringLength(20)]
        public string? SenderPhone { get; set; }

        [StringLength(200)]
        public string? SenderAddress { get; set; }

        // ============================
        // 🎁 NGƯỜI NHẬN
        // ============================
        [StringLength(100)]
        public string? ReceiverName { get; set; }

        [StringLength(20)]
        public string? ReceiverPhone { get; set; }

        [StringLength(200)]
        public string? ReceiverAddress { get; set; }

        // Tỉnh/thành phố người nhận
        public string? Province { get; set; }

        // ============================
        // 📦 HÀNG HÓA
        // ============================
        [StringLength(200)]
        public string? ProductName { get; set; }

        // Loại hàng (tài liệu, hàng dễ vỡ, điện tử...)
        [StringLength(50)]
        public string? GoodsType { get; set; }

        // Khối lượng (kg)
        [Column(TypeName = "decimal(10,2)")]
        public decimal Weight { get; set; }

        // Giá trị khai báo (VNĐ) – cũng là giá trị hàng để tính COD nếu chọn "thu bằng giá trị hàng"
        [Column(TypeName = "decimal(15,2)")]
        public decimal Value { get; set; }

        [StringLength(200)]
        public string? Note { get; set; }

        // Trường cũ (giữ cho tương thích DB, không bắt buộc phải dùng)
        public decimal Cod { get; set; }

        // ============================
        // ⚙ CẤU HÌNH GIAO HÀNG
        // ============================

        // Khu vực giao nhận: inner / outer
        [StringLength(20)]
        public string? AreaType { get; set; }

        // Phương thức gửi: pickup (nhân viên lấy) / hub (tự mang đến hub)
        [StringLength(20)]
        public string? PickupMethod { get; set; }

        // Hub khách chọn nếu tự mang đến hub
        [StringLength(100)]
        public string? DropoffHub { get; set; }

        // Mức dịch vụ: standard / fast / express
        [StringLength(20)]
        public string? ServiceLevel { get; set; }

        // ============================
        // 🚚 AI TRẢ PHÍ SHIP
        // ============================
        // "sender"  -> người gửi chịu phí ship
        // "receiver"-> người nhận chịu phí ship
        [StringLength(20)]
        public string? ShipPayer { get; set; }

        // ============================
        // 📌 TRẠNG THÁI
        // ============================
        [StringLength(20)]
        public string? Status { get; set; } = "pending";

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        // ============================
        // 🚚 TÀI XẾ
        // ============================
        public int? DriverId { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public int? Sequence { get; set; }

        // ============================
        // MAP – VỊ TRÍ NHẬN HÀNG
        // ============================
        public double? Lat { get; set; }
        public double? Lng { get; set; }

        // ============================
        // 📷 POD – GIAO THÀNH CÔNG
        // ============================
        public string? PodImagePath { get; set; }
        public string? DeliveredNote { get; set; }
        public DateTime? DeliveredAt { get; set; }

        // ============================
        // ❌ GIAO THẤT BẠI
        // ============================
        public string? FailedReason { get; set; }
        public string? FailedImagePath { get; set; }
        public DateTime? FailedAt { get; set; }

        // ============================
        // 🚛 PHÍ VẬN CHUYỂN
        // ============================
        [Column(TypeName = "decimal(15,2)")]
        public decimal ShipFee { get; set; } = 0;

        // ============================
        // 💰 COD – TIỀN THU HỘ
        // ============================
        [Column(TypeName = "decimal(15,2)")]
        public decimal CodAmount { get; set; } = 0;

        public bool IsCodPaid { get; set; } = false;
        public DateTime? CodPaidAt { get; set; }
    }
}

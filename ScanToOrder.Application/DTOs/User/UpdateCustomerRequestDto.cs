using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class UpdateCustomerRequestDto
{
    [JsonPropertyName("name")] 
    [Required(ErrorMessage = "Tên không được để trống")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("dob")]
    [Required(ErrorMessage = "Ngày sinh là bắt buộc")]
    public DateOnly? Dob { get; set; }
}
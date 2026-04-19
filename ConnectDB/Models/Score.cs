using ConnectDB.Models;

public class Score
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SubjectId { get; set; }

    public float KT1 { get; set; }
    public float KT2 { get; set; }
    public float DiemThi { get; set; }

    // Thuộc tính tính toán (Read-only hoặc tính trước khi lưu)
    public float DiemTrungBinh { get; set; }
    public string? KetQua { get; set; }

    public Student Student { get; set; }
    public Subject Subject { get; set; }
}
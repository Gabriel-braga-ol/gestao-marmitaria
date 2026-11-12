namespace GestaoMarmitaria.Api.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public Guid FamiliaId { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public int? SubstituidoPorId { get; set; }
    public RefreshToken? SubstituidoPor { get; set; }
}
